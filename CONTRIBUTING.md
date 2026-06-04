# Contributing

Preesta is small and pre-1.0; the contribution path is short.

## Bugs

Open an issue with the bug template. Include `preesta --version`, the rule (redacted of any secrets / internal hostnames), and the relevant log section.

## Features / changes

For anything beyond a tiny fix, open an issue first to talk about the shape — saves both of us throwaway PR work.

## Pull request flow

1. Fork → branch from `main`
2. Make the change. Keep the diff focused; please don't pile in unrelated cleanups
3. `dotnet test` — 200+ tests, all in-process (no network). Must stay green
4. Open the PR against `main`. CI runs the test suite, CodeQL, and a Docker build
5. Maintainer reviews and merges

## Local setup

For a development checkout:

```bash
git clone https://github.com/preesta/preesta.git
cd preesta
dotnet test
```

To run a Docker image instead, see [Quickstart](https://preesta.dev/quickstart/).

## Code style

No formatter enforced. Match what's already in the file you're editing. Comments explain the *why* when it's non-obvious — don't paraphrase what the code does.

## Engineering notes

Architecture, adding a tracker, release process, and other contributor-facing docs live in [`dev-notes/`](dev-notes/) — not published to the docs site, but in the repo for people working on Preesta itself.

## Code of conduct

This project follows the [Contributor Covenant](CODE_OF_CONDUCT.md). Be civil; we'll all save time.
