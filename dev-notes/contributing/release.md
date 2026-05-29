# Release

Pre-1.0 release process. Will firm up once 1.0 ships and breaking changes get a deprecation cycle.

## When to cut a release

- A user-facing feature lands (new tracker, new delivery channel, new rule field)
- An accumulated batch of fixes deserves a tag
- A breaking change merges (always tagged, always called out in the changelog)

Not for: internal refactors that don't touch user surfaces, test-only changes, documentation-only PRs.

## Version numbers

Pre-1.0 minor bumps for breaking changes, patch bumps for fixes. The actual version lives in `Preesta/Preesta.csproj` as `<Version>0.X.Y</Version>` — bump it on the release commit.

## Steps

1. **Update `Preesta.csproj` `<Version>`** to the new number.
2. **Update `docs/operations/upgrading.md`** with a new section: what's changed, what users need to do.
3. **Update `MIGRATION.md`** with a phase / fix-batch entry if not already there (internal-facing developer log, separate from the user-facing changelog).
4. **Tag the commit** — `git tag v0.X.Y -m "..."`, `git push origin v0.X.Y`.
5. **GitHub Release** — the tag triggers a CI workflow that builds the Docker image and pushes to ghcr.io/preesta/preesta:0.X.Y plus `:latest`. (Or run the build manually if the workflow isn't set up yet — TODO.)
6. **Announce** — if there's a Discord / mailing list (none currently), drop a note. For now: a tweet or a heads-up to early users.

## Hot-fix release

1. Branch from the last release tag.
2. Apply the fix.
3. Bump patch version.
4. Tag, build, push.
5. Cherry-pick / forward-port to main.

## Docker image tagging

When a release is cut, three image tags get pushed:

- `ghcr.io/preesta/preesta:0.X.Y` — pinned, never moves
- `ghcr.io/preesta/preesta:0.X` — moves with each patch of the minor
- `ghcr.io/preesta/preesta:latest` — moves with every release

Production deployments should pin to `0.X.Y` for predictability.

## What about the docs site

The MkDocs build deploys on every push to `main` (via a GitHub Pages workflow). Tagged releases don't get their own doc snapshots yet — the live docs always describe the current `main`. This is fine pre-1.0; post-1.0 with breaking changes it'll need versioned docs ([mike](https://github.com/jimporter/mike) is the standard MkDocs answer).
