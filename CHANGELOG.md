# Changelog

All notable changes to Preesta are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and the project uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0] - 2026-05-21

First tagged release. Pre-1.0 — the `rules.yaml` and `appsettings.yaml` schemas
may still evolve in breaking ways between minor versions.

### Added

- Jira (Server & Cloud), Linear, GitHub, GitLab, Shortcut as issue sources
- Email (SMTP), Telegram, Slack as independent delivery channels
- Impersonal rule model: rules say *which issues*, identity maps say *who to notify*
- REST and GraphQL mutation execution (comments, status changes, label updates)
- Custom Jira field auto-discovery
- YAML and XML rule configuration formats
- MkDocs Material documentation site at `docs/`
- Multi-arch Docker image at `ghcr.io/preesta/preesta`
- Self-contained binaries for linux-x64, linux-arm64, osx-x64, osx-arm64, win-x64

[Unreleased]: https://github.com/preesta/preesta/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/preesta/preesta/releases/tag/v0.1.0
