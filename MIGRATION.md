# Bender → Preesta: Migration & Roadmap

## Branding

- **New name:** Preesta
- **GitHub org:** `github.com/preesta` (created 2026-04-10)
- **Domain:** `preesta.dev` (free, not yet purchased)
- **Tagline:** "Pre-established rules for your issue tracker"
- **Folk-etymology:** "pre-established" (no Russian/Slavic backstory in public materials)
- **Old repo** `ValentinLevitov/bender` — оставляем, потом архивируем
- **Plan:** работаем локально, первый push в `preesta/preesta` когда будет готово

## Completed

### Phase 1: .NET 5 → .NET 8 ✅
- 4 `.csproj`: `net5.0` → `net8.0`, `LangVersion` removed (C# 12 default)
- `Microsoft.Extensions.*` 5.0.0 → 8.0.0, `System.CodeDom` 5.0.0 → 8.0.0
- Dockerfile: SDK/runtime images 5.0 → 8.0
- CI workflows: checkout v2→v4, setup-dotnet v1→v4, dotnet 5.0.x→8.0.x
- Tests: 37/37 passed

### Phase 2: Unity DI → Microsoft.Extensions.DependencyInjection ✅
- Removed Unity 5.11.10 + Unity.Interception 5.11.1
- Added M.E.DI 8.0.0 with .NET 8 keyed services for named registrations
- Rewrote `DependencyContainer.cs` (192 → ~60 lines)
- Deleted `SeriloggingBehavior.cs`
- Added constructors to `ReactionPipe<T>`, `HttpJiraService`, `IssuesInMultipleStructuresSupplier`, `PackageConverterBase<T>` (kept `{ get; set; }` properties intact)
- Tests: 37/37 passed

### Phase 3: SmtpClient → MailKit ✅
- Replaced deprecated `System.Net.Mail.SmtpClient` with MailKit 4.8.0
- `MailMessage` → `MimeMessage` + `BodyBuilder`, `LinkedResource` → `BodyBuilder.LinkedResources`
- Removed unused `System.Reactive.Linq`
- Tests: 37/37 passed

### Phase 4: Serilog packages ✅
- Serilog 2.10→4.3, Settings.Configuration 3.1→8.0, Sinks.Console 3.1→6.1
- Enrichers.Environment 2.1→3.0, Enrichers.Thread 3.1→4.0
- Sentry.Serilog 3.0→6.3, Exceptions 6.0→8.4
- Removed deprecated `Serilog.Sinks.ColoredConsole` → Console sink with Literate ANSI theme
- Tests: 37/37 passed

### Phase 5: Test packages ✅
- NUnit 3.12→4.3 (via `GlobalUsings.cs` ClassicAssert alias — zero assertion rewrites)
- NUnit3TestAdapter 3.16→4.6, Microsoft.NET.Test.Sdk 16.5→17.11
- Replaced Moq 4.16 with NSubstitute 5.3 (9 test files migrated)
- Added `StubDelegatingHandler` helper for tests that used `Moq.Protected`
- Tests: 37/37 passed

### Phase 6: Rebranding Bender → Preesta ✅
- `Bender/` → `Preesta/`, `bender.sln` → `preesta.sln`, `bender-cron` → `preesta-cron`
- `namespace Bender` → `namespace Preesta` (all .cs files)
- `BenderSendsLetter` → `SendsNotification`, `BenderMakesUpdateHimself` → `SelfUpdate`
- `TBendersReaction` → `TReaction` in `Package<T,T>`
- Embedded resource `"Bender.rules.xsd"` → `"Preesta.rules.xsd"`, Serilog config, T4 templates
- Dockerfile, docker-publish.yml, .vscode/launch.json, README.md updated
- JIRA issue keys `BENDER-2301` etc. and test data name "Bender" in XmlConfigTests preserved
- Tests: 37/37 passed

### Phase 7: Telegram + Formatting Refactor ✅
- Added `TelegramMessenger` — sends via Bot API (`/bot{token}/sendMessage`, HTML parse_mode)
- Replaced T4 templates with C# `IssueFormatter` and `ReleaseFormatter` (StringBuilder-based)
- Dual output: styled HTML tables for email, Telegram-compatible HTML for chat
- `NotificationSpec.TelegramChatIds` in rules config (XML `telegramChatId` attribute, YAML `telegramChatId` field)
- `Telegram:botToken` in `appsettings.yaml` / `appsettings.secrets.yaml`
- `ReactionPipeline<T>` runs email + Telegram flows in parallel
- `Message.TextBody` property for plain/Telegram text alongside HTML `Body`
- Deleted T4 template files (`Preesta/Template/` directory removed)

### Phase 8: Scriban templates, list-of-items layout ✅
- Scriban templates in `Preesta/Templates/*.scriban-html|text` (separate from C# code)
- List-of-items layout (Linear-style) replaces table; coloured pills for Status, dots for Priority
- `notify.columns` per rule controls meta-line; `all-non-empty` magic value expands to every populated field
- Resolution / Updated / Project added to Issue model
- Renamed `BuildFound`/`BuildFixed` → `AffectsVersions`/`FixVersions` (matches Jira UI naming)

### Phase 9a: typos + dead code cleanup ✅
- Fixed `Newtownsoft → Newtonsoft`, `RulesExtentions → RulesExtensions`, log message "convertion", structured prop `FoundRueles`
- Test fixtures: `faired-suprevisor → expired-supervisor`
- DTO field `Status.desription → Status.description`
- Removed dead code: `Service.cs` (never called since 2021), `SimpleCustomField`, decade-old TODO comments

### Phase 9b: naming refactor ✅
- `Notify` → `NotificationSpec` (class); YAML keyword `notify:` preserved
- `Notify.MetaAddressers` → `RawRecipients`, `MetaCarbonCopy` → `RawCc`
- `Update` → `SelfUpdateSpec`
- `Rule.HowToNotify` → `Notification`, `HowToUpdate` → `Updates`
- `SendsNotification` (in `Letter.cs`) → `NotificationReaction` (file `NotificationReaction.cs`)
- `IssueStaff` → `IssueParticipants`; `Issue.Staff` → `Issue.Participants`
- `ReactionPipe<T>` → `ReactionPipeline<T>` (`Reaction` semantics kept — we react to a rule trigger)
- `Common<T>` → `MessageBuilder<T>` (former `Convert/Common.cs` was generic anti-pattern)
- Full rename of `Build*` → `Release*` (entity, rule, supplier, formatter, converter, templates) — matches Jira UI
- `IssueInclusionToStructRule` → `StructureAmbiguityRule`; `IssuesInMultipleStructuresSupplier` → `StructureAmbiguitySupplier`
- `Preesta.Data.User` kept as is (namespaces resolve the conflict with `JiraRest.Data.User`)

### Phase 12 (partial / MVP): Linear support ✅
- New `type: linear` rule alongside `type: jql` and `type: build`
- `LinearIssueSource` queries Linear's GraphQL API (`api.linear.app/graphql`) with hardcoded MVP filter (`assignee = viewer, state.type ≠ completed`)
- New `LinearGraphQL/` project: `LinearConnection` (raw API key in `Authorization` header — no `Bearer` prefix), `ILinearGateway`
- New `LinearRule` (marker class), `LinearIssueSupplier` (mirrors `JqlSupplier`)
- `Issue.Url` field added — populated by sources that return a canonical URL (Linear); formatter prefers it over the reconstructed `rootUri/browse/{key}` form
- `Linear:apiKey` + `Linear:workspace` in `appsettings.yaml`; pipeline registered only when API key is configured
- Tests: 68 → 71 (`MockLinearServer` + 3 `LinearIssueSourceTests`)
- **Filter syntax DSL deferred to Phase 12.1** — no per-rule filter field yet, the MVP query is fixed inside `LinearIssueSource`

### Phase 12.1: Linear filter modes ✅
- Three mutually exclusive filter modes per `type: linear` rule: `filter` (AI prompt — primary, the only one we advertise), `filterRaw` (raw Linear GraphQL filter — undocumented escape hatch), `viewId` (Linear saved-view ID — undocumented escape hatch)
- Validation in `YamlRulesConfig.GetLinearRules`: rules with zero or 2+ filter sources are dropped with an `_logger.Error(...)` message
- AI prompt path is a 2-hop GraphQL fetch: `issueFilterSuggestion(prompt:)` translates the prompt into a Linear filter object, which is then passed straight into `issues(filter:)`. No caching — the extra hop is accepted as noise
- `viewId` path uses `customView(id:){ issues { nodes { ... } } }` so Linear evaluates the saved view server-side
- The hardcoded `viewer.assignedIssues` MVP query is gone; `ILinearGateway` exposes a generic `Query(string, object?)`
- Tests: 71 → 82 (8 `LinearIssueSourceTests` covering all three modes + 5 `YamlConfigTests` for filter-mode validation)

### Phase 12.2: Linear filter transparency in digest ✅
- Each Linear section header now shows what produced its issue list — mirror of Jira's "Open in Jira →" pattern
  - `filter:` mode → `AI filter: «<prompt>»` (text only — Linear has no URL-encoded filter state)
  - `filterRaw:` mode → `Filter: <compact JSON>` (truncated to 200 chars)
  - `viewId:` mode → `View: <name>` + clickable **Open in Linear →** link to `linear.app/{workspace}/view/{id}`
- `customView` GraphQL projection now also fetches `name` in the same hop — no extra request
- Linear UI does **not** encode filter state in URL (verified live: query params/hash are ignored, state lives in localStorage). Only saved views have a permanent shareable URL. Hence no fallback link for AI/raw modes — we deliberately don't lie to the user about which Linear page reproduces the rule
- Tests: 82 → 92 (LinearTransparencyTests + LinearIssueSourceTests projection assertions)

### Phase 12.3: Linear self-update via raw GraphQL mutations ✅
- Linear rules can now carry `mutations:` — list of raw GraphQL `mutation { ... }` strings, mirror of Jira's `callRest`/`mutations` REST hook
- No DSL — power-user surface. The first attempt at a high-level `do:` DSL (`addComment` / `setState` / etc.) was rejected and reverted: this is a power-user feature, the right abstraction level is GraphQL itself
- Same `mutations:` YAML key as jql rules; entry shape diverges by rule type (REST verb/url/body for jql, single `mutation:` string for linear)
- Marker substitution unified: `{{@issueKey}}`, `{{@issueId}}` (new — Linear UUID), `{{@title}}` (new — issue summary), `{{@assignee.email}}` etc., plus `<<c# ... #>>` script injection. Marker replacer extracted to `internal static IssuePackageConverter.ReplaceKnownMarkers`
- Architecture: `GraphQLMutation` reaction wrapper parallel to `SelfUpdate`; `IssueSupplier.GetMutationPackages` hook (default REST, override produces GraphQL); `ILinearMutationHandler` parallel to `IHttpHandler`; `LinearMutationExecutor` reuses the read-side `LinearConnection` (one HttpClient, one auth header)
- DI: registers Linear pipeline with both read source and write executor when `Linear:apiKey` is set; Jira pipeline byte-identical
- Per-mutation failures (HTTP error, GraphQL `errors` envelope) logged at Error and swallowed — one bad mutation does not abort the rule
- Renamed for symmetry: `SelfUpdateSpec` → `RestMutationSpec`, `Rule.Updates` → `Rule.Mutations`, YAML key `callRest:` → `mutations:` (XML format keeps legacy `callRest` element)
- Tests: 92 → 100 (LinearMutationExecutorTests + LinearGraphQLMutations YAML parsing + IssuePackageConverter normalisation)
- Live-validated: `commentCreate` mutation against PRE-9 in workspace `preesta-dev` — comment appeared in Linear with correct timestamp + marker-substituted body
- `filterRaw:` scalar-type preservation: YamlDotNet returns all scalars as strings when target is `object` (CoreSchema tag inference doesn't apply in this code path). `ConvertFilterRaw` now walks the `Dictionary<object, object>`/`List<object>` tree and recovers `int`/`double`/`bool` via `TryParse`. Quoted-string scalars that happen to look numeric (`"9"`) parse as numbers too — known limitation, acceptable for the power-user surface (test asserts the behaviour explicitly).

### Phase 10: Slack notifications ✅
- Personal DMs (mirror of Telegram), not channel webhooks — one workspace bot token, per-message routing to individual users
- Same per-rule `slackUserId:` + workspace-level `slackUsers:` map (email→id) shape as Telegram
- `chat.postMessage` Web API with `Authorization: Bearer xoxb-…` header (note the `Bearer` prefix — Linear API keys go raw, Slack doesn't)
- Required Slack bot scopes: `chat:write`, `im:write`, plus optional `users:read.email`
- Rich Slack mrkdwn: `*PRE-7*` bold issue keys with `<url|label>` click-through, `_..._` italic filter description, `:hourglass_flowing_sand:` / `:white_check_mark:` / `:red_circle:` etc. emoji chips for status & priority — note `*bold*` (single asterisk), not `**bold**` Markdown
- HTTP 200 with `{ok:false, error:"..."}` treated as failure (logged at Error with the Slack error code, swallowed — one bad user ID doesn't abort the digest); HTTP-level errors and JSON parse errors also logged + swallowed
- `SlackMessenger` lives in `Messaging/` next to `TelegramMessenger`; endpoint constant overridable for in-process WireMock testing (`MockSlackServer`)
- DI registers `SlackMessenger` only when `Slack:botToken` is set; otherwise pipeline runs the email-only / email+telegram path with no behaviour change
- Inline `StringBuilder` mrkdwn formatter rather than a third Scriban template — short format, Slack-specific emoji needed, fewer build inputs
- Tests: 100 → 113 (3 SlackMessenger HTTP, 4 routing, 2 mrkdwn format, 2 ReactionPipeline integration, 2 YAML parsing — `MockSlackServer` WireMock helper)

## Remaining

## Roadmap: New Features

### Phase 9c (consider): drop Structure plugin support
- `StructureAmbiguityRule` + `StructureAmbiguitySupplier` integrate with Almworks Structure (Atlassian Marketplace plugin, alive in 2026 — Platinum Partner, v7.3.0 released 2026-05-08)
- Currently use Server-style REST endpoint `/rest/structure/2.0/forest/latest`. Cloud compatibility unverified
- Niche feature originally written for one specific project. Preesta itself works without it
- Decision: keep for one more release cycle; if no users opt in by then, delete this rule type, supplier, XML/YAML parsing, DI registration, related tests

### Phase 11: MS Teams notifications
- `TeamsMessenger` via Incoming Webhook connector (POST JSON with Adaptive Card)
- Adaptive Card format for rich issue tables (natively rendered in Teams)
- Config: `Teams:webhookUrl` in appsettings, `teamsWebhookUrl` per rule in rules config

### Phase 12: Input — Linear + GitHub Issues
Linear and GitHub Issues are the primary targets — both popular, both lack built-in rule-engine automation.

| Tracker | Built-in automation | Preesta value |
|---|---|---|
| **Linear** | Basic (auto-archive, status transitions). No rule engine. | High — GraphQL API, no JQL-like queries+actions |
| **GitHub Issues** | Only via Actions (CI, not issue management) | High — search syntax as JQL analog |
| **GitLab Issues** | Limited | Good |
| **Plane** | OSS Linear alternative, minimal automation | Good |
| **Shortcut** | Minimal | Good |

Architecture: abstract `IIssueSource` interface (replaces current Jira-specific `IJiraService`), with implementations per tracker. Rules reference a source by name in config.

Trackers NOT worth targeting (strong built-in automation): Jira Cloud, Azure DevOps, Monday.com, Asana.

### Phase 13: REST API
- ASP.NET Minimal API alongside the CLI
- Endpoints: list rules, trigger rule group, get last run status/results, health check
- Config stays in files (GitOps principle — API is for operations, not configuration)
- Prometheus `/metrics` endpoint: rules executed, issues matched, errors

### Phase 14: Web dashboard (read-only)
- Blazor or static SPA
- Shows: rule run history, matched issues, notification log, system health
- NOT a config editor — config lives in git
- Light enough to embed in the same Docker container

### Phase 15: Integration testing infrastructure
- **WireMock.Net** — mock any HTTP API (Jira, Linear, GitHub, Slack, Telegram) in-process
- **Testcontainers** + **MailHog** — real SMTP in Docker, verify sent emails via MailHog API
- Test topology:
  ```
  [Preesta] → WireMock (pretends to be Linear/Jira/GitHub API) → returns test issues
  [Preesta] → MailHog container (pretends to be SMTP) → verify email content
  [Preesta] → WireMock (pretends to be Slack/Telegram webhook) → verify payload
  ```

## Decisions Log

- **Newtonsoft.Json:** keeping as-is. Entire Jira REST client uses `dynamic` deserialization. No benefit to migrating to System.Text.Json.
- **Nito.AsyncEx:** keeping. Used for `.WaitAndUnwrapException()` in `Connection.cs`. Proper async/await refactor deferred.
- **T4 templates:** removed in Phase 7. Replaced with C# StringBuilder-based formatters in `Preesta/Formatting/`.
- **supercronic:** update to v0.2.x during Phase 6 Docker cleanup.
- **Bender lineage:** README origin section: "Preesta started in 2019 as 'Bender'. In 2026 it was rebuilt on .NET 8 and renamed."
