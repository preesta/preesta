# CLI

Preesta is a single-shot CLI. One invocation, one schedule group, one batch of work, exit.

## Invocation

```bash
preesta <schedule-group>
```

`<schedule-group>` is the `group:` value on the rules to fire. Every rule with a matching group runs; everything else is ignored.

## Exit codes

| Code | Meaning |
|---|---|
| 0 | Success — the run finished. Note this is "the process finished without crashing", not "every rule succeeded". Per-rule failures (HTTP errors, parse failures, send failures) are logged at `Error` and swallowed |
| Non-zero | The host (`dotnet` runtime) crashed before / during startup, or a precondition failed (missing `appsettings.yaml`, malformed Logger config, etc.) |

The "every rule succeeded" check is intentionally not the exit-code semantics — a transient SMTP outage shouldn't fail a cron job that also dispatches Slack messages successfully. Watch logs for `Error`-level lines if you want to alert on partial failures.

## Working directory

Preesta reads configuration from files **relative to the current working directory**:

- `./appsettings.yaml` (required)
- `./appsettings.yml` (alternate extension)
- `./secrets/appsettings.secrets.yaml` (optional)
- `./rules.yaml` (path overridable via `Application:rulesFileName`)
- `./rules.xml` (legacy XML rules — still read if `rulesFileName: rules.xml`)

For deployments, `cd /path/to/preesta && dotnet Preesta.dll <group>` is the idiomatic invocation. The Docker image's entrypoint is set up this way.

## Logging

Stdout/stderr. Serilog driven by the `Logger:` block in `appsettings.yaml`. The default in-repo config writes to console with an ANSI theme — production usually adds a file or Sentry sink in `appsettings.secrets.yaml`.

Logs are structured (Serilog's `MessageTemplate` system) — every field is keyed, JSON sinks reconstruct them cleanly.

## Help / version

Not implemented. The CLI is meant for cron tabs and CI jobs, not interactive use — if you're invoking it manually enough to need `--help`, you're probably building rules and the docs site is more useful.
