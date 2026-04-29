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
- Replaced T4 templates with C# `IssueFormatter` and `BuildFormatter` (StringBuilder-based)
- Dual output: styled HTML tables for email, Telegram-compatible HTML for chat
- `Notify.TelegramChatIds` in rules config (XML `telegramChatId` attribute, YAML `telegramChatId` field)
- `Telegram:botToken` in `appsettings.yaml` / `appsettings.secrets.yaml`
- `ReactionPipe<T>` runs email + Telegram flows in parallel
- `Message.TextBody` property for plain/Telegram text alongside HTML `Body`
- Deleted T4 template files (`Preesta/Template/` directory removed)
- Tests: 50/50 passed (4 new Telegram tests)

## Remaining

## Roadmap: New Features

### Phase 8: Slack notifications
- `SlackMessenger` (incoming webhook)
- New XML/YAML syntax for Slack channel targeting

### Phase 9: MS Teams notifications
- `TeamsMessenger` via Incoming Webhook connector (POST JSON with Adaptive Card)
- Adaptive Card format for rich issue tables (natively rendered in Teams)
- Config: `Teams:webhookUrl` in appsettings, `teamsWebhookUrl` per rule in rules config

### Phase 10: Input — Linear + GitHub Issues
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

### Phase 11: REST API
- ASP.NET Minimal API alongside the CLI
- Endpoints: list rules, trigger rule group, get last run status/results, health check
- Config stays in files (GitOps principle — API is for operations, not configuration)
- Prometheus `/metrics` endpoint: rules executed, issues matched, errors

### Phase 12: Web dashboard (read-only)
- Blazor or static SPA
- Shows: rule run history, matched issues, notification log, system health
- NOT a config editor — config lives in git
- Light enough to embed in the same Docker container

### Phase 13: Integration testing infrastructure
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
