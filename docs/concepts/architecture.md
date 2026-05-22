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
                  │  each configured IDeliveryChannel    │
                  │  sends the digest (email → SMTP,     │
                  │  email→ID map → Telegram / Slack)    │
                  │                ▼                     │
                  │  Optional: one IMutationHandler runs │
                  │  the rule's write-side actions       │
                  │  (comments, status changes)          │
                  └──────────────────────────────────────┘
```

Each issue tracker is a self-contained module; the CLI assembles the shared delivery channels once, then registers a pipeline for every configured tracker (see [Wiring](#wiring-one-module-per-tracker)).

## The five moving parts

**Source** — talks to one tracker's API. `HttpJiraService` (REST), `LinearIssueSource` / `GithubIssueSource` / `GitlabIssueSource` (GraphQL), `ShortcutIssueSource` (REST). Each one maps the tracker's native response shape into a shared `Issue` model with `Key`, `Summary`, `Status`, `Priority`, `Participants {Assignee, Reporter, Creator}`, `Labels`, dates, etc.

**Supplier** — `IssueSupplier<TRule>` base class. Per type (`JqlSupplier`, `LinearIssueSupplier`, `GithubIssueSupplier`, …) it walks the rules, asks the matching Source for issues, and groups them. The grouping key is `(To, Cc, Subject, Rule)`, and the `To`/`Cc` lists already have the [`assignee` / `reporter` markers resolved](routing-model.md) into per-issue email addresses — so one rule with `mailTo: assignee` naturally produces N packages, one per distinct assignee email.

**Formatter** — `IssueFormatter` and the Scriban templates in `Preesta/Templates/`. Renders one digest per package as HTML email, plain text (Telegram-compatible HTML), and Slack mrkdwn. Knows about per-tracker concerns: "Open in Jira → / Linear / GitHub / GitLab / Shortcut" round-trip links in the header, filter-description lines, status pill colors, priority dots.

**Delivery channel** — `IDeliveryChannel` (`EmailChannel`, `TelegramChannel`, `SlackChannel`) is one send target. Given the run's notification packages and the converter, it produces and sends the channel-native messages; underneath, `MessageBuilder` does the per-recipient fan-out (email → SMTP, and for the DM channels the workspace-level `telegramUsers:` / `slackUsers:` email→ID maps — literal `slackUserId:` / `telegramChatId:` on the rule are one-for-all). The configured channels are assembled once into a `DeliveryChannels` object shared by every pipeline. Adding a target (Discord, a webhook) is one new `IDeliveryChannel` plus one line in `DeliveryChannels.Build` — the pipeline doesn't change.

**Mutation handler** — one `IMutationHandler` per pipeline: `RestMutations` (Jira `callRest`, Shortcut) or `GraphQLMutations` (Linear, GitHub, GitLab), each wrapping the wire-level executor. It pulls the run's mutation packages and executes them; per-mutation failures (HTTP errors, GraphQL `errors` envelope) are logged and swallowed — one bad mutation never stops the others. A tracker has exactly one mutation transport, so the pipeline holds exactly one handler (not a REST-or-GraphQL pair).

## Wiring: one module per tracker

Each tracker is an `ITrackerModule` — `JqlModule`, `LinearModule`, `GithubModule`, `GitlabModule`, `ShortcutModule`. A module knows two things: whether it's configured (`IsConfigured`) and how to build its own pipeline (`BuildPipeline` — source + supplier + converter + mutation handler). `DependencyContainer` assembles the shared `DeliveryChannels` once, then loops the modules and registers a pipeline for each configured one. It never names a specific tracker.

The payoff is that the two axes of growth are each a one-file change:

- **A new tracker** — write one `ITrackerModule` (plus its transport `.csproj`) and add one entry to the module list. `Application.cs`, the DI loop, and the other modules don't change.
- **A new delivery target** — write one `IDeliveryChannel` and add one line to `DeliveryChannels.Build`. The pipelines and modules don't change.

Jira is not privileged — it's `JqlModule`, registered through the same loop as the rest. A deployment with no `Jira:` section simply doesn't register it; the same goes for any other tracker.

## Why a single CLI, not a daemon

Preesta is a one-shot CLI on purpose: `preesta <schedule-group>`. The reason is operational simplicity — no persistent process to crash, no in-memory state to lose, no port to expose. You schedule it however you already schedule things (cron, systemd timer, Kubernetes CronJob, GitHub Actions on a schedule). Each invocation reads rules + secrets, does its work, exits.

The trade-off: anything stateful (run history, last-seen IDs, dedup across runs) doesn't exist. That's deliberate — every digest is computed fresh from the current tracker state. If you want "issues that became blocked since yesterday" you express it as a filter on `updated_at >= today - 1d`, not as a stored diff.

## Why per-tracker projects

`LinearGraphQL/`, `GithubGraphQL/`, `GitlabGraphQL/`, `ShortcutRest/`, `JiraRest/`, `Messaging/` (SMTP/Telegram/Slack) are each their own .csproj. Each is a thin wrapper over `HttpClient` — one `Query()` or `Send()` method, just enough to keep auth and request-shaping in one place. They have no Preesta-specific code, just transport concerns. The Preesta project depends on all of them.

Why not one project? Because every new tracker integration would otherwise grow the main project's dependency surface. Keeping the transport in its own .csproj means upgrading one tracker's library never touches the others.

## Why YAML for rules

The rule file is what end-users edit, and they aren't always engineers. YAML is the right pivot — multi-line strings for GraphQL mutation bodies are readable, mapping shapes for chip filters (GitLab) are natural, scalar strings work for raw search queries (GitHub, Shortcut), and comments survive round-trips. We accept some YAML weirdness (`YamlRulesConfig.ConvertFilterRaw` exists to recover scalar types after YamlDotNet flattens everything to string) but the user surface stays human-friendly.
