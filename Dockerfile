# syntax=docker/dockerfile:1
# Multi-arch build: docker buildx with --platform linux/amd64,linux/arm64.
# Version stamped via --build-arg VERSION=X.Y.Z; defaults to 0.0.0-dev for
# local builds so `preesta --version` still prints something useful.

ARG VERSION=0.0.0-dev

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS publish
ARG TARGETARCH
ARG VERSION
WORKDIR /src
COPY . .
# Framework-dependent publish (--self-contained false) keeps the image small;
# the runtime base layer already bundles .NET 8. No --runtime flag — the JIT
# in the runtime image picks the right architecture for $TARGETARCH.
RUN dotnet publish "./Preesta/Preesta.csproj" \
      -c Release \
      -o /app/publish \
      --self-contained false \
      -p:Version=${VERSION} \
      -p:PublishTrimmed=false \
      -p:PublishSingleFile=false

# ---------------------------------

# supercronic ships per-arch binaries; pick the one matching $TARGETARCH.
# TODO: pin SHA256 checksums per arch from
# https://github.com/aptible/supercronic/releases/tag/v0.1.12 before the first
# release. Network-fetched without verification is fine for dev builds.
FROM --platform=$BUILDPLATFORM curlimages/curl AS get-supercronic
ARG TARGETARCH
WORKDIR /tmp
RUN case "${TARGETARCH}" in \
      amd64) ARCH=amd64 ;; \
      arm64) ARCH=arm64 ;; \
      *) echo "unsupported TARGETARCH: ${TARGETARCH}" >&2; exit 1 ;; \
    esac \
 && curl -fsSL "https://github.com/aptible/supercronic/releases/download/v0.1.12/supercronic-linux-${ARCH}" -o supercronic \
 && chmod +x supercronic

# ---------------------------------

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
ARG VERSION
LABEL org.opencontainers.image.source=https://github.com/preesta/preesta \
      org.opencontainers.image.description="Rule-based digests for your issue trackers" \
      org.opencontainers.image.licenses=MIT \
      org.opencontainers.image.version=${VERSION}
WORKDIR /app
COPY --from=publish /app/publish/ .
RUN ln -s /app/Preesta /usr/local/bin/preesta
COPY --from=get-supercronic /tmp/supercronic /usr/local/bin/supercronic
RUN chmod +x /usr/local/bin/supercronic /usr/local/bin/preesta
ADD preesta-cron .
# The official dotnet/runtime image already ships a non-root `app` user with
# /app as home. Just take ownership of the dir we copied into.
RUN chown -R app /app
USER app
CMD ["supercronic", "-passthrough-logs", "/app/preesta-cron"]
