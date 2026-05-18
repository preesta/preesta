# `appsettings.yaml` schema

Two files. `appsettings.yaml` (committed) holds defaults and non-secret config. `secrets/appsettings.secrets.yaml` (gitignored) holds tokens, passwords, and anything else you don't want in source control.

The second file is loaded on top of the first if it exists — overrides win.

## Application

```yaml
Application:
  rulesFileName: rules.yaml         # path to the rules file, relative to cwd
  maintenanceTeam: ""               # comma-separated emails — see Redirector
  supervisors: ""                   # comma-separated emails — see Redirector
  subjectPrefix: ""                 # prepended to every email subject (e.g. "[PREESTA] ")
```

## Jira

```yaml
Jira:
  rootUri: https://yourcompany.atlassian.net/    # required
  apiToken: "ATATT3xFf..."                       # preferred for Cloud + Server 9.x
  # userName: "you@example.com"                  # Server fallback
  # password: "..."
  maxResults: 300                                # per-search ceiling
```

If `apiToken` is set, Preesta authenticates via `Basic ApiToken` (Atlassian's Cloud convention). Otherwise falls back to `userName:password`.

## Linear

```yaml
Linear:
  apiKey: "lin_api_..."         # required to enable Linear pipeline
  workspace: "your-slug"        # used for "Open in Linear →" links (viewId mode only)
```

## GitHub

```yaml
Github:
  token: "ghp_..."     # required to enable GitHub pipeline
```

Scopes needed on the PAT: `repo` (or `public_repo`) + `user:email` (or `read:user`). See [GitHub tracker page](../trackers/github.md).

## GitLab

```yaml
Gitlab:
  token: "glpat-..."                                   # required
  apiBase: "https://gitlab.example.com/api/graphql"    # optional, defaults to gitlab.com
```

## Shortcut

```yaml
Shortcut:
  apiToken: "sct_rw_workspace_..."   # required; sct_rw_ for write, sct_ro_ for read-only
```

## SMTP

```yaml
Smtp:
  Host:      smtp.gmail.com    # required
  Port:      465               # required
  User:      you@example.com
  Password:  "app-password"
  From:      you@example.com
  EnableSsl: true
```

## Telegram

```yaml
Telegram:
  botToken: "8640...:AAEV..."    # required to enable Telegram pipeline
```

## Slack

```yaml
Slack:
  botToken: "xoxb-..."           # required to enable Slack pipeline
```

## Logger

Serilog configuration block — see [Serilog.Settings.Configuration](https://github.com/serilog/serilog-settings-configuration) for the schema. Default in `appsettings.yaml` writes to Console with a literate ANSI theme; production deployments typically add a file sink or Sentry sink.

## Pipeline gating

A tracker pipeline is registered in DI **only if its required credentials are set**. Missing credentials silently disable the pipeline — no error, no warning, no log noise. This is by design: a single deployment commonly exposes only one or two trackers, and the rest shouldn't generate noise.

The gating rules:

| Pipeline | Required |
|---|---|
| `Jql` (Jira) | `Jira:rootUri` + (`Jira:apiToken` or `Jira:userName + Jira:password`) |
| Release | same as Jira |
| `Linear` | `Linear:apiKey` |
| `Github` | `Github:token` |
| `Gitlab` | `Gitlab:token` |
| `Shortcut` | `Shortcut:apiToken` |
| Email (SMTP) | `Smtp:Host` |
| Telegram | `Telegram:botToken` |
| Slack | `Slack:botToken` |
