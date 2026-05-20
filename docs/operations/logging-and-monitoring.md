# Logging and monitoring

Preesta uses [Serilog](https://serilog.net/). The `Logger` block in `appsettings.yaml` is read by `Serilog.Settings.Configuration` — every Serilog sink and enricher is available, configured declaratively.

## Default configuration

```yaml
Logger:
  Serilog:
    MinimumLevel: Verbose
    Using:
      - Serilog.Sinks.Console
      - Preesta
      - Serilog.Enrichers.Thread
    Enrich:
      - WithThreadId
    WriteTo:
      - Name: Console
        Args:
          outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {NewLine}{Exception}{NewLine}{Properties:j}{NewLine}"
          theme: "Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme::Literate, Serilog.Sinks.Console"
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
| `Information` | `N rules of type X found in schedule group 'Y'`, `<Tracker> mutation succeeded`, SMTP send confirmations |
| `Warning` | Custom-field discovery failure (graceful), individual GitHub/GitLab/Linear hidden-email cases |
| `Error` | Per-rule conversion failures, per-mutation GraphQL errors, per-message Slack/Telegram failures, per-issue source fetch failures. **Never aborts the run** — Preesta keeps going for the other rules / mutations / channels |
| `Fatal` | Only if the host crashes before/during DI container construction |

The "swallow + log + continue" pattern is universal. There's no "stop on first error" mode.

## Adding sinks

Common production setups:

### File sink

```yaml
Logger:
  Serilog:
    Using:
      - Serilog.Sinks.Console
      - Serilog.Sinks.File
    WriteTo:
      - Name: Console
      - Name: File
        Args:
          path: "/var/log/preesta/preesta-.log"
          rollingInterval: Day
          retainedFileCountLimit: 30
```

Add the `Serilog.Sinks.File` NuGet package reference to `Preesta.csproj` first.

### Sentry

`Sentry.Serilog` is already referenced in `Preesta.csproj`. Configure with a DSN:

```yaml
Logger:
  Serilog:
    Using:
      - Serilog.Sinks.Console
      - Sentry.Serilog
    WriteTo:
      - Name: Console
      - Name: Sentry
        Args:
          dsn: "https://...@sentry.io/..."
          minimumEventLevel: Error
          minimumBreadcrumbLevel: Information
```

Errors land in Sentry as events, lower-level messages as breadcrumbs.

### JSON sink

For Loki / Loki-compatible aggregators or any pipeline that wants structured logs:

```yaml
WriteTo:
  - Name: File
    Args:
      path: "/var/log/preesta/preesta-.jsonl"
      formatter: "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact"
      rollingInterval: Day
```

Requires the `Serilog.Formatting.Compact` package.

## Metrics

There's no built-in `/metrics` endpoint — Preesta is a one-shot CLI, no HTTP server. For Prometheus-style observability today the path is:

1. Each cron invocation pipes its stdout through `logger -t preesta` (or equivalent) into a log shipper
2. Shipper extracts structured fields (rule count, mutation count, error count) into metrics

This is enough for most teams. A future REST-API mode (Phase 13 in the roadmap) would add a real `/metrics` endpoint.

## Correlation across runs

Each run is independent — no run ID, no parent run. If you need to correlate digest emails with mutation actions and tracker events, the timestamp + schedule group are the join key:

```
INFO 2026-05-18 09:00:01  1 rules of type jql found in schedule group 'morning'
INFO 2026-05-18 09:00:03  GitHub mutation succeeded: mutation { addComment ...
INFO 2026-05-18 09:00:04  Sent 4 email messages
```

These three lines belong to the same run if the timestamps are within a few seconds and the schedule group lines up. A more rigorous correlation ID can be added via `LogContext.PushProperty` if it becomes a real need.
