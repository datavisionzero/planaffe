#!/usr/bin/env bash
# Start an image once, the way an installation would, and ask it the two
# questions that tell a working image from one that only built:
#
#   smoke-image.sh IMAGE [VERSION]
#
# Everything the contract job checks runs from the source tree, so what only
# the image can get wrong — its entrypoint, its user, the SPA in `wwwroot`, a
# native library the runtime stage forgot — would otherwise first be noticed by
# whoever pulls it. It runs against a Postgres of its own, on a network of its
# own, with bootstrap values made up on the spot, and cleans all of it up
# whatever the outcome.
#
# With VERSION, `/version` also has to say it, which is what a release claims.

set -euo pipefail

image=${1:?usage: smoke-image.sh IMAGE [VERSION]}
version=${2:-}
# Long enough for a first start on a cold runner, migrations included; short
# enough that a start that hangs is a red job in minutes rather than hours.
deadline=${SMOKE_DEADLINE_SECONDS:-120}
port=${SMOKE_PORT:-18080}

run=smoke-$$
network=$run
db=$run-db
app=$run-app
password=$(openssl rand -hex 16)
token=$(openssl rand -hex 24)

cleanup() {
  docker rm -f "$app" "$db" >/dev/null 2>&1 || true
  docker network rm "$network" >/dev/null 2>&1 || true
}
trap cleanup EXIT

fail() {
  echo "::error::$1"
  echo "---- the instance's log ----"
  docker logs "$app" 2>&1 | tail -n 100 || true
  exit 1
}

docker network create "$network" >/dev/null

docker run -d --name "$db" --network "$network" \
  -e POSTGRES_DB=planaffe \
  -e POSTGRES_USER=planaffe \
  -e POSTGRES_PASSWORD="$password" \
  postgres:18 >/dev/null

# Over TCP rather than the socket: the image's first start runs a temporary
# server for initdb that listens on the socket alone and then shuts down, and a
# check that caught that one would hand the instance a database mid-restart.
echo "Waiting for Postgres."
ready=false
for _ in $(seq 1 60); do
  if docker exec "$db" pg_isready -h 127.0.0.1 -U planaffe -d planaffe >/dev/null 2>&1; then
    ready=true
    break
  fi
  sleep 1
done
if [ "$ready" != "true" ]; then
  docker logs "$db" 2>&1 | tail -n 50
  echo "::error::Postgres never became ready"
  exit 1
fi

docker run -d --name "$app" --network "$network" \
  -p "127.0.0.1:$port:8080" \
  -e "ConnectionStrings__Postgres=Host=$db;Port=5432;Database=planaffe;Username=planaffe;Password=$password" \
  -e PLANAFFE_BOOTSTRAP_ADMIN=smoke \
  -e PLANAFFE_BOOTSTRAP_EMAIL=smoke@example.org \
  -e PLANAFFE_BOOTSTRAP_TOKEN="$token" \
  "$image" >/dev/null

base="http://127.0.0.1:$port"
echo "Waiting up to ${deadline}s for $base/version."
started=$SECONDS
said=""
while [ $((SECONDS - started)) -lt "$deadline" ]; do
  if [ "$(docker inspect -f '{{.State.Running}}' "$app" 2>/dev/null)" != "true" ]; then
    fail "the container stopped before it answered"
  fi
  if said=$(curl --fail --silent --max-time 5 "$base/version"); then
    break
  fi
  said=""
  sleep 2
done
[ -n "$said" ] || fail "no answer from /version within ${deadline}s"
echo "/version says: $said"

if [ -n "$version" ] && ! grep -qF "\"$version\"" <<<"$said"; then
  fail "/version does not say $version"
fi

# The SPA, and one of the files it names: an index.html without its bundle is
# a blank page, and that is exactly what a missing `wwwroot` copy looks like
# from outside.
page=$(curl --fail --silent --max-time 10 "$base/") || fail "GET / did not answer"
grep -q '<div id="root">' <<<"$page" || fail "GET / is not the web application"
script=$(grep -oE 'src="/assets/[^"]+\.js"' <<<"$page" | head -n 1 | cut -d'"' -f2 || true)
[ -n "$script" ] || fail "the web application names no script under /assets"
curl --fail --silent --max-time 10 --output /dev/null "$base$script" \
  || fail "$script is named by the page and not served"
echo "GET / serves the web application, and $script is there."
