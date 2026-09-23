"""The prune rule against a registry that is played by a dictionary.

Standard library only, so the check needs nothing installed:

    python3 -m unittest discover -s .github/scripts

The script's file name carries a dash, because that is how the workflow calls
it, so it is loaded by path rather than imported by name.
"""

import importlib.util
import io
import json
import os
import pathlib
import tempfile
import unittest
import urllib.error
from datetime import datetime, timedelta, timezone
from unittest import mock

HERE = pathlib.Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("prune_registry", HERE / "prune-registry.py")
prune = importlib.util.module_from_spec(spec)
spec.loader.exec_module(prune)

NOW = datetime(2026, 9, 21, 4, 17, tzinfo=timezone.utc)
OLD = (NOW - timedelta(days=200)).isoformat().replace("+00:00", "Z")
YOUNG = (NOW - timedelta(days=2)).isoformat().replace("+00:00", "Z")

VERSIONS_URL = "https://api.github.com/users/owner/packages/container/planaffe/versions"
TOKEN_URL = "https://ghcr.io/token"
MANIFEST_URL = "https://ghcr.io/v2/owner/planaffe/manifests/"


def version(id_, digest, tags, created_at):
    return {
        "id": id_,
        "name": digest,
        "created_at": created_at,
        "metadata": {"container": {"tags": tags}},
    }


# A release from long ago, the two architectures under it, and an untagged
# version nothing refers to any more. Only the last one may go.
RELEASE = version(1, "sha256:index", ["0.1.0", "0.1"], OLD)
AMD64 = version(2, "sha256:amd64", [], OLD)
ARM64 = version(3, "sha256:arm64", [], OLD)
ORPHAN = version(4, "sha256:orphan", [], OLD)
MAIN = version(5, "sha256:main", ["main"], YOUNG)

MANIFESTS = {
    "sha256:index": {
        "manifests": [{"digest": "sha256:amd64"}, {"digest": "sha256:arm64"}],
    },
    "sha256:main": {"manifests": []},
}


class Registry:
    """Answers `urlopen` the way the two APIs do, with some manifests failing."""

    def __init__(self, failing=None, fail_times=None):
        self.failing = failing or {}
        self.fail_times = fail_times
        self.calls = []

    def __call__(self, request, timeout=None):
        url = request.full_url
        self.calls.append(url)
        if url.startswith(VERSIONS_URL):
            page = int(url.rsplit("page=", 1)[1])
            return self.answer([RELEASE, AMD64, ARM64, ORPHAN, MAIN] if page == 1 else [])
        if url.startswith(TOKEN_URL):
            return self.answer({"token": "pull"})
        if url.startswith(MANIFEST_URL):
            digest = url[len(MANIFEST_URL):]
            if digest in self.failing and self.fail_times != 0:
                if self.fail_times is not None:
                    self.fail_times -= 1
                raise urllib.error.HTTPError(url, self.failing[digest], "no", {}, None)
            return self.answer(MANIFESTS[digest])
        raise AssertionError(f"unexpected request to {url}")

    @staticmethod
    def answer(body):
        return io.BytesIO(json.dumps(body).encode())


class PruneTest(unittest.TestCase):
    def run_prune(self, registry):
        with tempfile.TemporaryDirectory() as directory:
            output = pathlib.Path(directory, "output")
            output.write_text("")
            environ = {
                "OWNER": "owner",
                "PACKAGE": "planaffe",
                "GH_TOKEN": "test",
                "GITHUB_OUTPUT": str(output),
            }
            with mock.patch("urllib.request.urlopen", registry), mock.patch.object(
                prune.time, "sleep"
            ), mock.patch("sys.stdout", new_callable=io.StringIO):
                code = prune.main(environ, now=NOW)
            return code, output.read_text()

    def test_takes_only_what_nothing_refers_to(self):
        code, output = self.run_prune(Registry())
        self.assertEqual(code, 0)
        self.assertIn("ids=4\n", output)
        self.assertIn("count=1\n", output)

    def test_a_registry_error_takes_nothing(self):
        # The release's index cannot be read, so its two architectures look
        # like orphans — which is exactly what must not be concluded.
        code, output = self.run_prune(Registry(failing={"sha256:index": 500}))
        self.assertNotEqual(code, 0)
        self.assertEqual(output, "")

    def test_a_missing_manifest_takes_nothing(self):
        code, output = self.run_prune(Registry(failing={"sha256:index": 404}))
        self.assertNotEqual(code, 0)
        self.assertEqual(output, "")

    def test_a_refused_token_is_not_retried(self):
        registry = Registry(failing={"sha256:index": 401})
        code, _ = self.run_prune(registry)
        self.assertNotEqual(code, 0)
        asked = [url for url in registry.calls if url.endswith("sha256:index")]
        self.assertEqual(len(asked), 1)

    def test_a_busy_registry_is_asked_again(self):
        registry = Registry(failing={"sha256:index": 503}, fail_times=1)
        code, output = self.run_prune(registry)
        self.assertEqual(code, 0)
        self.assertIn("ids=4\n", output)

    def test_a_registry_busy_for_good_takes_nothing(self):
        registry = Registry(failing={"sha256:index": 429})
        code, output = self.run_prune(registry)
        self.assertNotEqual(code, 0)
        self.assertEqual(output, "")
        asked = [url for url in registry.calls if url.endswith("sha256:index")]
        self.assertEqual(len(asked), prune.ATTEMPTS)


if __name__ == "__main__":
    unittest.main()
