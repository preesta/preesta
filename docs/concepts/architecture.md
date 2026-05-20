# Architecture

Preesta is small on purpose. The whole runtime fits on a napkin:

```
┌────────────┐    ┌──────────────────────────────────────┐    ┌────────────┐
│ rules.yaml │───▶│  Preesta CLI                         │───▶│  SMTP/IMAP │
│            │    │                                      │    │  Telegram  │
│ appsettings│───▶│  per-type Supplier ─▶ per-tracker    │    │  Slack     │
│ .yaml(+sec)│    │  fetches issues       Source         │    └────────────┘
└────────────┘    │                ▼                     │
                  │  group by (recipient, subject, rule) │
                  │                ▼                     │
                  │  Formatter renders one digest        │
                  │  per group: HTML + text + mrkdwn     │
                  │                ▼                     │
                  │  per-channel MessageBuilder fans     │
                  │  out the digest to each channel's    │
                  │  recipient list (email → SMTP,       │
                  │  email→chatId map → Telegram, etc)   │
                  │                ▼                     │
                  │  Optional: same matched issues feed  │
                  │  the MutationExecutor for write-side │
                  │  actions (comments, status changes)  │
                  └──────────────────────────────────────┘
```

## The five moving parts

**Source** — talks to one tracker's API. `HttpJiraService` (REST), `LinearIssueSource` / `GithubIssueSource` / `GitlabIssueSource` (GraphQL), `ShortcutIssueSource` (REST). Each one maps the tracker's native response shape into a shared `Issue` model with `Key`, `Summary`, `Status`, `Priority`, `Participants {Assignee, Reporter, Creator}`, `Labels`, dates, etc.

**Supplier** — `IssueSupplier<TRule>` base class. Per type (`JqlSupplier`, `LinearIssueSupplier`, `GithubIssueSupplier`, …) it walks the rules, asks the matching Source for issues, and groups them. The grouping key is `(To, Cc, Subject, Rule)`, and the `To`/`Cc` lists already have the [`assignee` / `reporter` markers resolved](routing-model.md) into per-issue email addresses — so one rule with `mailTo: assignee` naturally produces N packages, one per distinct assignee email.

**Formatter** — `IssueFormatter` and the Scriban templates in `Preesta/Templates/`. Renders one digest per package as HTML email, plain text (Telegram-compatible HTML), and Slack mrkdwn. Knows about per-tracker concerns: "Open in Jira → / Linear / GitHub / GitLab / Shortcut" round-trip links in the header, filter-description lines, status pill colors, priority dots.

**MessageBuilder** — per-channel fan-out. `ToMessages` for SMTP, `ToTelegramMessages` and `ToSlackMessages` for the DM channels. The latter two look up each package's resolved emails in workspace-level `telegramUsers:` / `slackUsers:` (email → ID) maps and emit one message per ID. Literal `slackUserId:` / `telegramChatId:` on the rule itself is one-for-all (no per-recipient routing).

**MutationExecutor** — `IHttpHandler` (REST) and `IGraphQLMutationHandler` (GraphQL) are the two write-side abstractions. `LinearMutationExecutor` and `GithubMutationExecutor` implement the GraphQL one; `GitlabMutationExecutor` joins them; Jira's `callRest` and Shortcut's REST mutations go through the REST handler. Per-mutation failures (HTTP errors, GraphQL `errors` envelope) are logged and swallowed — one bad mutation never stops the others.

## Why a single CLI, not a daemon

Preesta is a one-shot CLI on purpose: `preesta <schedule-group>`. The reason is operational simplicity — no persistent process to crash, no in-memory state to lose, no port to expose. You schedule it however you already schedule things (cron, systemd timer, Kubernetes CronJob, GitHub Actions on a schedule). Each invocation reads rules + secrets, does its work, exits.

The trade-off: anything stateful (run history, last-seen IDs, dedup across runs) doesn't exist. That's deliberate — every digest is computed fresh from the current tracker state. If you want "issues that became blocked since yesterday" you express it as a filter on `updated_at >= today - 1d`, not as a stored diff.

## Why per-tracker projects

`LinearGraphQL/`, `GithubGraphQL/`, `GitlabGraphQL/`, `ShortcutRest/`, `JiraRest/`, `Messaging/` (SMTP/Telegram/Slack) are each their own .csproj. Each is a thin wrapper over `HttpClient` — one `Query()` or `Send()` method, just enough to keep auth and request-shaping in one place. They have no Preesta-specific code, just transport concerns. The Preesta project depends on all of them.

Why not one project? Because every new tracker integration would otherwise grow the main project's dependency surface. Keeping the transport in its own .csproj means upgrading one tracker's library never touches the others.

## Why YAML for rules

The rule file is what end-users edit, and they aren't always engineers. YAML is the right pivot — multi-line strings for GraphQL mutation bodies are readable, mapping shapes for chip filters (GitLab) are natural, scalar strings work for raw search queries (GitHub, Shortcut), and comments survive round-trips. We accept some YAML weirdness (`YamlRulesConfig.ConvertFilterRaw` exists to recover scalar types after YamlDotNet flattens everything to string) but the user surface stays human-friendly.
