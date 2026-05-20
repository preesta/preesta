# Upgrading

Preesta is pre-1.0. Breaking changes can happen between minor versions until a stable surface is declared. This page lists the breaking changes per release and how to migrate.

## Versioning policy (pre-1.0)

- **Minor** (`0.X.0`) — may break rules.yaml grammar, appsettings keys, or CLI flags. Always called out in this page.
- **Patch** (`0.X.Y`) — bug fixes only, never breaks user-facing surfaces.
- **Post-1.0** — semantic versioning. Breaking changes only on major bumps, deprecation cycle of at least one minor.

## Upgrade procedure

1. Read this page from your current version down to the new one.
2. Update `appsettings.yaml` / `rules.yaml` per the changes called out.
3. Pull the new image / rebuild from source.
4. Run the command on a dev schedule group first; observe one cron tick of output.
5. Promote.

## Release history

> TODO — fill this section in once releases start. Until then, `MIGRATION.md` at the repo root holds the internal "what changed in each phase" timeline (developer-facing, less polished). When the first tagged release ships, this page becomes the user-facing changelog.

### Pre-release evolution

- **Phase 12.5** (May 2026) — GitHub Issues + PRs support added
- **Phase 13** (May 2026) — GitLab Issues, Shortcut stories support added
- **Custom fields** (May 2026) — Jira custom fields auto-discovered; `columns:` accepts custom field display names
- **Slack DMs** (Apr 2026) — Slack personal-DM delivery added; mirrors Telegram routing
- **Linear** (Apr 2026) — Linear support added; AI prompt + raw filter + saved view modes
- **YAML rules** (Mar 2026) — `rules.yaml` format added alongside legacy `rules.xml`. Both still parse; new deployments should use YAML

If you've been tracking `main`, `git log --oneline --grep "^Phase"` is the most accurate per-feature timeline.
