# Releasing

Cutting a release is two commands once `main` is green.

## One-time: GitHub org migration

The release infrastructure (`Directory.Build.props`, the Docker label,
`release.yml`'s release URLs, the docs links) assumes the repo lives at
`github.com/preesta/preesta`. Until that's true, the workflows still publish
artifacts under whatever `${{ github.repository }}` resolves to — but the
in-image labels and doc links will be wrong.

Steps the repo owner runs once:

1. **Create the GitHub organization** `preesta` at
   <https://github.com/organizations/new>.
2. **Transfer the repo**: Settings → General → Transfer ownership →
   target `preesta`. GitHub forwards traffic from the old URL to the new
   one indefinitely.
3. **Re-create the org-scoped Pages site**: Settings → Pages → re-enable
   (transfer doesn't always carry GH Pages config). Confirm
   <https://preesta.github.io/preesta/> serves the docs.
4. **Update local clones**:
   ```bash
   git remote set-url origin git@github.com:preesta/preesta.git
   ```
5. **Container retag** (optional): existing pushes under
   `ghcr.io/valentinlevitov/preesta` won't auto-move. After the first tagged
   release publishes to `ghcr.io/preesta/preesta`, the old image stays
   pinnable but isn't refreshed. Update any infra referencing the old path.

## Steps

1. **Update CHANGELOG.md** — promote the `## [Unreleased]` section to
   `## [X.Y.Z] - YYYY-MM-DD` and add a fresh empty `## [Unreleased]` on top.
   Update the compare links at the bottom.

2. **Commit + tag**:

   ```bash
   git add CHANGELOG.md
   git commit -m "Release vX.Y.Z"
   git tag vX.Y.Z
   git push origin main --tags
   ```

3. **GitHub Actions does the rest**:
   - `release.yml` builds self-contained binaries for five RIDs (linux-x64,
     linux-arm64, osx-x64, osx-arm64, win-x64), packages each as
     `preesta-X.Y.Z-<rid>.{tar.gz,zip}` with a matching `.sha256`, and creates
     a GitHub release containing all artifacts plus the CHANGELOG section as
     the release body.
   - `docker-publish.yml` builds a multi-arch image (linux/amd64 + linux/arm64)
     and pushes `ghcr.io/preesta/preesta:X.Y.Z`, `:X.Y`, and `:latest`.

4. **Smoke-test** the published artifacts:

   ```bash
   # Docker
   docker run --rm ghcr.io/preesta/preesta:vX.Y.Z preesta --version

   # Binary (pick one for your platform)
   curl -L https://github.com/preesta/preesta/releases/download/vX.Y.Z/preesta-X.Y.Z-linux-x64.tar.gz \
     | tar xz
   ./preesta-X.Y.Z-linux-x64/preesta --version
   ```

## Versioning

Pre-1.0: rule-schema breakage is allowed between minor versions but must be
called out in CHANGELOG.md with a `### Breaking` heading and a migration
note. Patch releases stay backwards-compatible.

Post-1.0: standard semver. Breaking changes only on major bumps.

## What's automated, what's not

| Task | Where |
|---|---|
| Build + test on every push | `dotnet.yml` |
| Build + push Docker image | `docker-publish.yml` (on push to main, tag, or PR) |
| Build + publish binary artifacts + GitHub release | `release.yml` (on `v*` tag) |
| Update CHANGELOG.md | **Manual** |
| Update version in csproj | **Not needed** — `release.yml` passes `-p:Version=` |
| Announce the release | **Manual** |
