# One image, carrying the API and — once it exists — the built SPA: the two
# toolchains of ADR 0002 and ADR 0004 meet here and nowhere else. The runtime
# image carries neither of them.
#
# Build context is the repository root:
#   docker build -f deploy/Dockerfile -t planaffe .
#
# The web stage is not here yet. The single-page application arrives with its
# own ticket (the web shell, cut three), and this file gains a Node stage that
# builds it and a COPY into ./wwwroot then. Until then the image serves the API.

# ------------------------------------------------------------- the server ----
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# What this build calls itself. The release workflow passes the tag; a build
# started by hand keeps the default the props file gives, which says plainly
# that it is not a release (Directory.Build.props).
ARG VERSION=0.0.0-dev

# Which commit it was built from, appended by the SDK to the informational
# version behind a `+`. CI passes it, because this build has no repository to
# read it out of — `Dockerfile.dockerignore` excludes `.git/`. Left empty it
# stamps nothing, and `InstanceVersion` cuts the suffix off anyway: the commit
# is the build's business, the semver is what the CLI compares (ADR 0011).
ARG REVISION=

# Restore against the manifests alone, so that a change to a source file does
# not invalidate the package layer.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Planaffe.Domain/Planaffe.Domain.csproj src/Planaffe.Domain/
COPY src/Planaffe.Application/Planaffe.Application.csproj src/Planaffe.Application/
COPY src/Planaffe.Infrastructure/Planaffe.Infrastructure.csproj src/Planaffe.Infrastructure/
COPY src/Planaffe.Api/Planaffe.Api.csproj src/Planaffe.Api/
RUN dotnet restore src/Planaffe.Api/Planaffe.Api.csproj

COPY src/Planaffe.Domain/ src/Planaffe.Domain/
COPY src/Planaffe.Application/ src/Planaffe.Application/
COPY src/Planaffe.Infrastructure/ src/Planaffe.Infrastructure/
COPY src/Planaffe.Api/ src/Planaffe.Api/
RUN dotnet publish src/Planaffe.Api/Planaffe.Api.csproj -c Release -o /app \
    -p:Version=$VERSION -p:SourceRevisionId=$REVISION

# ------------------------------------------------------------ the runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

# Npgsql probes for GSSAPI when it opens a connection, whatever the connection
# string says about authentication. Without the library the probe writes two
# lines with the word `Error` straight to stderr on every start of a run that is
# going fine, and the next person reading the log during a real outage has to
# rule it out first. Three megabytes to keep the log honest; nothing here asks
# for Kerberos. Ahead of the COPY, so a source change does not re-run apk.
RUN apk add --no-cache krb5-libs

COPY --from=build /app ./

# The operational verbs the CLI deliberately does not have — the ones that
# need the connection string — run as `docker compose exec planaffe planaffe …`
# (docs/codebase.md), so the binary has to be found by name.
ENV PATH="/app:${PATH}" \
    ASPNETCORE_HTTP_PORTS=8080

EXPOSE 8080

# Reachable over the network, so it does not run as root.
RUN chown -R $APP_UID:$APP_UID /app
USER $APP_UID

ENTRYPOINT ["planaffe"]
