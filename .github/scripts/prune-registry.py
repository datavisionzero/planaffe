#!/usr/bin/env python3
"""Work out which versions of the container package have stopped being useful.

Every commit on the trunk sets a `sha-<commit>` tag, and nothing has ever taken
one away. The tag earns its place while it is young: it is what an installation
pins itself to when the trunk turns out bad, without waiting for the next
commit. A `sha-` tag from half a year ago pins nobody — it only makes the
package page unreadable.

So the rule is narrow on purpose, and it is a rule about the `sha-` prefix
rather than about age alone:

  * A version carrying any tag that is not `sha-` is never touched. `:main`,
    `:latest` and every release tag fall out here, whatever else they carry.
  * A `sha-` version is a candidate only when it is both older than the window
    and outside the newest few. Either alone would be enough on a quiet week
    and wrong on a busy one.
  * An untagged version goes only when nothing that survives refers to it. The
    manifests under a multi-architecture index are untagged too, and so is the
    attestation buildx attaches, so "untagged" on its own is not a fact about
    whether something is still in use.

Nothing is deleted here. This prints the version ids to delete, and the
workflow hands them to the action that does it.

And nothing is decided on half an answer. "Referred to by nothing that stays"
is only true when every survivor's manifest was actually read, so a registry
that will not answer — a 429, a 5xx, a 401, a manifest that is not there —
stops the run before it has named anything. A red run on a Monday costs a
week's housekeeping; a guess costs a release nobody can pull any more.
"""

import json
import os
import sys
import time
import urllib.error
import urllib.request
from datetime import datetime, timedelta, timezone

# What a manifest may be. Without these the registry answers with the schema-1
# manifest of whichever architecture it feels like, and the children are lost.
MANIFEST_TYPES = ", ".join(
    [
        "application/vnd.oci.image.index.v1+json",
        "application/vnd.oci.image.manifest.v1+json",
        "application/vnd.docker.distribution.manifest.list.v2+json",
        "application/vnd.docker.distribution.manifest.v2+json",
    ]
)

# A registry that is briefly busy gets a second and a third chance before the
# run gives up. Only for the answers that can mean "later"; a 401 or a 404
# will not change its mind in a few seconds.
ATTEMPTS = 3
RETRY_AFTER_SECONDS = 5
TRANSIENT = {429, 500, 502, 503, 504}


class Unreadable(Exception):
    """A manifest a survivor names could not be read, so nothing is safe to go."""


def created(version):
    """The API stamps a trailing `Z`, which only Python 3.11 and up reads."""
    return datetime.fromisoformat(version["created_at"].replace("Z", "+00:00"))


def get_json(url, headers):
    request = urllib.request.Request(url, headers=headers)
    with urllib.request.urlopen(request, timeout=30) as answer:
        return json.load(answer)


def versions(settings):
    """Every version of the package, newest first, as the API pages them."""
    page = 1
    while True:
        batch = get_json(
            f"https://api.github.com/users/{settings['owner']}/packages/container/"
            f"{settings['package']}/versions?per_page=100&page={page}",
            {
                "Authorization": f"Bearer {settings['token']}",
                "Accept": "application/vnd.github+json",
                "X-GitHub-Api-Version": "2022-11-28",
            },
        )
        if not batch:
            return
        yield from batch
        page += 1


def registry_token(settings):
    """A pull token. The package is public, so this needs no credentials."""
    return get_json(
        f"https://ghcr.io/token?service=ghcr.io"
        f"&scope=repository:{settings['owner']}/{settings['package']}:pull",
        {},
    )["token"]


def children(settings, digest, token):
    """The digests one manifest refers to, or `Unreadable` when it cannot say."""
    url = f"https://ghcr.io/v2/{settings['owner']}/{settings['package']}/manifests/{digest}"
    headers = {"Authorization": f"Bearer {token}", "Accept": MANIFEST_TYPES}
    for attempt in range(1, ATTEMPTS + 1):
        try:
            manifest = get_json(url, headers)
            break
        except urllib.error.HTTPError as error:
            # Every answer that is not the manifest is a reason to stop, a 404
            # included: a survivor whose manifest is missing is a registry that
            # disagrees with its own listing, and that is not the moment to
            # conclude what nothing refers to.
            reason = str(error.code)
            transient = error.code in TRANSIENT
        except (urllib.error.URLError, OSError, ValueError) as error:
            reason = f"{type(error).__name__}: {error}"
            transient = True
        if not transient or attempt == ATTEMPTS:
            raise Unreadable(f"{digest}: {reason} after {attempt} attempt(s)")
        print(f"  ! {digest}: {reason}, trying again")
        time.sleep(RETRY_AFTER_SECONDS * attempt)

    referred = set()
    for child in manifest.get("manifests", []):
        referred.add(child["digest"])
    for layer in [manifest.get("config")] + manifest.get("layers", []):
        # Layers are blobs, not versions; only manifests appear in the package
        # listing. Collected anyway so the set is the whole truth about what
        # this manifest points at.
        if layer:
            referred.add(layer["digest"])
    return referred


def settings_from(environ):
    """Read when the run starts rather than on import, so a test can load this."""
    return {
        "owner": environ["OWNER"],
        "package": environ["PACKAGE"],
        "token": environ["GH_TOKEN"],
        "keep_newest": int(environ.get("KEEP_NEWEST", "20")),
        "max_age_days": int(environ.get("MAX_AGE_DAYS", "60")),
        # The action takes them in one input, and a run that would delete more
        # than this is a run worth looking at before it does. The schedule
        # catches up.
        "max_per_run": int(environ.get("MAX_PER_RUN", "100")),
    }


def main(environ=None, now=None):
    environ = os.environ if environ is None else environ
    settings = settings_from(environ)
    keep_newest = settings["keep_newest"]
    max_age_days = settings["max_age_days"]
    cutoff = (now or datetime.now(timezone.utc)) - timedelta(days=max_age_days)

    tagged_keep, sha_versions, untagged = [], [], []

    for version in versions(settings):
        tags = version["metadata"]["container"]["tags"]
        if not tags:
            untagged.append(version)
        elif all(tag.startswith("sha-") for tag in tags):
            sha_versions.append(version)
        else:
            tagged_keep.append(version)

    # Newest first, so that "the newest few" is the head of the list. The API
    # orders them this way already; sorting says so rather than relying on it.
    sha_versions.sort(key=created, reverse=True)

    doomed = [
        version
        for version in sha_versions[keep_newest:]
        if created(version) < cutoff
    ]
    doomed_ids = {version["id"] for version in doomed}

    print(
        f"{len(tagged_keep)} versions carry a tag that is not `sha-`, "
        f"{len(sha_versions)} carry only `sha-` tags, {len(untagged)} carry none."
    )
    print(
        f"Of the `sha-` ones, {len(doomed)} are both older than {max_age_days} days "
        f"and outside the newest {keep_newest}."
    )

    # What everything that survives points at. An untagged version in this set
    # is a live child, whatever its age. A survivor that cannot be read ends the
    # run here, before a single id has been handed to the step that deletes —
    # the `sha-` ones included, so that a red run is a run that took nothing.
    survivors = tagged_keep + [v for v in sha_versions if v["id"] not in doomed_ids]
    token = registry_token(settings)
    referenced = set()
    try:
        for version in survivors:
            referenced |= children(settings, version["name"], token)
    except Unreadable as error:
        print(
            f"::error::Could not read what a surviving version refers to ({error}). "
            "Nothing is taken on this run."
        )
        return 1

    orphans = [
        version
        for version in untagged
        if version["name"] not in referenced
        and created(version) < cutoff
    ]
    print(
        f"{len(orphans)} untagged versions are older than {max_age_days} days and "
        f"referred to by nothing that stays."
    )

    going = doomed + orphans
    for version in going:
        tags = version["metadata"]["container"]["tags"]
        print(f"  - {version['id']} {version['created_at']} {tags or '(untagged)'}")

    max_per_run = settings["max_per_run"]
    if len(going) > max_per_run:
        print(f"Capped at {max_per_run}; the rest goes on the next run.")
        going = going[:max_per_run]

    ids = ",".join(str(version["id"]) for version in going)
    if output := environ.get("GITHUB_OUTPUT"):
        with open(output, "a", encoding="utf-8") as handle:
            handle.write(f"ids={ids}\n")
            handle.write(f"count={len(going)}\n")
    print(f"\n{len(going)} to delete.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
