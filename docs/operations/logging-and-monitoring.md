# Logging and monitoring

Logs are emitted as structured events. The `Logger` block in `appsettings.yaml` controls them entirely — destination, severity, formatting.

## Default configuration

```yaml
Logger:
  Serilog:
    MinimumLevel: Verbose
    WriteTo:
      - Name: Console
        Args:
          outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {NewLine}{Exception}{NewLine}{Properties:j}{NewLine}"
```

`MinimumLevel: Verbose` is good for development, noisy in production. Override in `appsettings.secrets.yaml` (which loads on top):

```yaml
Logger:
  Serilog:
    MinimumLevel: Information
```

## What gets logged

| Level | Examples |
|---|---|
| `Verbose` | Per-rule found rule lists, per-fetch payload sizes |
| `Information` | `N rules of type X found in tag 'Y'`, `<Tracker> mutation succeeded`, SMTP send confirmations |
| `Warning` | Custom-field discovery failure (graceful), individual GitHub/GitLab/Linear hidden-email cases |
| `Error` | Per-rule conversion failures, per-mutation GraphQL errors, per-message Slack/Telegram failures, per-issue source fetch failures. **Never aborts the run** — Preesta keeps going for the other rules / mutations / channels |
| `Fatal` | Only if the host crashes before/during DI container construction |

The "swallow + log + continue" pattern is universal. There's no "stop on first error" mode.

## Routing logs elsewhere

### Sentry (built into the official image)

```yaml
Logger:
  Serilog:
    WriteTo:
      - Name: Console
      - Name: Sentry
        Args:
          dsn: "https://...@sentry.io/..."
          minimumEventLevel: Error
          minimumBreadcrumbLevel: Information
```

Errors land in Sentry as events, lower-level messages as breadcrumbs.

### File / JSON output

Tail Preesta's stdout from your container runtime — most production setups already pipe container logs to a file or shipper. If you really need Preesta itself to write a rolling file (or JSON for Loki), you need a custom image with the file sink added; see the engineering notes in `dev-notes/` in the repo.

## Metrics

There's no built-in `/metrics` endpoint — Preesta is a one-shot CLI, no HTTP server. For Prometheus-style observability today the path is:

1. Each cron invocation pipes its stdout through `logger -t preesta` (or equivalent) into a log shipper
2. Shipper extracts structured fields (rule count, mutation count, error count) into metrics

This is enough for most teams. A future REST-API mode (Phase 13 in the roadmap) would add a real `/metrics` endpoint.

## Correlation across runs

Each run is independent — no run ID, no parent run. If you need to correlate digest emails with mutation actions and tracker events, the timestamp + tag are the join key:

```
INFO 2026-05-18 09:00:01  1 rules with tracker=jql found for tags [morning]
INFO 2026-05-18 09:00:03  GitHub mutation succeeded: mutation { addComment ...
INFO 2026-05-18 09:00:04  Sent 4 email messages
```

These three lines belong to the same run if the timestamps are within a few seconds and the tag lines up.
