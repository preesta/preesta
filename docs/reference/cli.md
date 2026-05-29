# CLI

Preesta is a single-shot CLI. One invocation, one batch of work, exit. Tag arguments are an optional filter — pass none to run every rule, pass some to narrow to the matching ones.

## Invocation

```bash
preesta                       # run every rule in rules.yaml
preesta <tag>                 # run rules whose tags include <tag>
preesta <tag> [<tag>…]        # OR-match: any rule with any matching tag fires
preesta --version | -v        # print version, exit
preesta --help    | -h        # show usage, exit
```

Lefthook-style positive tag selection: an untagged rule (`tags:` omitted) runs **only** when the CLI has no tag args; the moment any tag is requested, only tagged rules with at least one matching tag participate. Multiple CLI tags OR together.

Examples:

```bash
preesta                         # everything fires
preesta morning                 # only rules tagged "morning"
preesta blocker-watch release   # rules tagged either "blocker-watch" or "release"
```

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

For deployments, `cd /path/to/preesta && dotnet Preesta.dll [<tag>…]` is the idiomatic invocation. The Docker image's entrypoint is set up this way.

## Logging

Stdout/stderr. By default — colorized console. Production typically adds a file or Sentry destination in the config; see [Logging and monitoring](../operations/logging-and-monitoring.md) for the options.

## Help / version

`--help` / `-h` prints usage. `--version` / `-v` prints the build version string (informational + assembly version). Anything else is treated as tag arguments.
