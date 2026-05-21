# Releasing

Cutting a release is two commands once `main` is green.

## One-time: publish the repo

The org `preesta` already exists. The local repo has no `origin` yet, so the
first time around we create the GitHub repo inside the org and push:

```bash
gh repo create preesta/preesta \
  --source=. \
  --public \
  --remote=origin \
  --push \
  --description "Rule-based digests for your issue trackers"
```

After the push, enable GitHub Pages: Settings → Pages → Source: "GitHub
Actions". The `docs.yml` workflow already targets the `gh-pages` branch on
push to `main`, so the docs site comes up at
<https://preesta.github.io/preesta/> on the next push.

The release and Docker workflows use `${{ github.repository }}` for image
and artifact names, so they pick up `preesta/preesta` automatically once the
remote is in place — no edits to the YAML.

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
