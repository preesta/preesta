# Bender → Preesta: Migration Plan

## Branding

- **New name:** Preesta
- **GitHub org:** `github.com/preesta` (created 2026-04-10)
- **Domain:** `preesta.dev` (free, not yet purchased)
- **Tagline:** "Pre-established rules for your Jira"
- **Folk-etymology:** "pre-established" (no Russian/Slavic backstory in public materials)
- **Old repo** `ValentinLevitov/bender` — оставляем как есть, потом архивируем или добавим "moved to preesta/preesta"
- **Plan:** работаем локально, первый push в `preesta/preesta` когда будет готово

## Completed

### Phase 1: .NET 5 → .NET 8 ✅
- 4 `.csproj`: `net5.0` → `net8.0`, `LangVersion` removed (C# 12 default)
- `Microsoft.Extensions.*`: 5.0.0 → 8.0.0
- `System.CodeDom`: 5.0.0 → 8.0.0
- Dockerfile: SDK/runtime images 5.0 → 8.0
- CI workflows: checkout v2→v4, setup-dotnet v1→v4, dotnet 5.0.x→8.0.x
- `.vscode/launch.json`: path updated
- Build: 0 errors, 8 nullable warnings (pre-existing)
- Tests: 37/37 passed
- **Key finding:** Unity 5.11 works on .NET 8 — phases 1 and 2 can be done separately

## Remaining Phases

### Phase 2: Unity DI → Microsoft.Extensions.DependencyInjection (HIGH complexity)
- Remove: `Unity` 5.11.10, `Unity.Interception` 5.11.1
- Add: `Microsoft.Extensions.DependencyInjection` 8.0.x
- **Critical file:** `Bender/DI/DependencyContainer.cs` (192 lines) — full rewrite
- Convert `ReactionPipe<T>` from property injection to constructor injection (6 properties → ctor params)
- Replace `SeriloggingBehavior` (Unity.Interception) with decorator pattern
- Use .NET 8 keyed services for named registrations (`"Jql"`, `"Structure"`)
- Update all tests that construct `ReactionPipe<T>`

### Phase 3: SmtpClient → MailKit (MEDIUM complexity, independent)
- **Critical file:** `Messaging/SmtpClient.cs`
- Replace `System.Net.Mail.SmtpClient` (deprecated) with `MailKit.Net.Smtp.SmtpClient`
- Replace `MailMessage` → `MimeMessage`, `LinkedResource`/`AlternateView` → `BodyBuilder`
- Update exception handling in tests (`SmtpException` → MailKit exceptions)
- Remove unused `System.Reactive.Linq` from `Messaging.csproj`

### Phase 4: Update Serilog packages (LOW complexity, independent)
- `Serilog` 2.10.0 → 4.x
- `Serilog.Settings.Configuration` 3.1.0 → 8.x
- `Serilog.Sinks.Console` 3.1.1 → 6.x
- `Serilog.Enrichers.Environment` 2.1.3 → latest
- `Serilog.Enrichers.Thread` 3.1.0 → latest
- `Sentry.Serilog` 3.0.7 → 4.x
- `Serilog.Exceptions` 6.0.0 → 8.x
- **Remove** `Serilog.Sinks.ColoredConsole` (deprecated) → use Console sink with theme
- Update `appsettings.json`: remove ColoredConsole, update assembly refs

### Phase 5: Update test packages (LOW complexity, independent)
- `NUnit` 3.12.0 → 4.x (rewrite assertions: `Assert.AreEqual` → `Assert.That`)
- `NUnit3TestAdapter` → NUnit4 adapter
- `Microsoft.NET.Test.Sdk` 16.5.0 → 17.x
- **Replace** `Moq` 4.16.0 → `NSubstitute` (decision confirmed by user)

### Phase 6: Rebranding Bender → Preesta (MEDIUM complexity, DO LAST)

#### Directories to rename
- `Bender/` → `Preesta/`

#### Files to rename
- `bender.sln` → `preesta.sln`
- `Bender/Bender.csproj` → `Preesta/Preesta.csproj`
- `bender-cron` → `preesta-cron`
- `BenderMakeUpdateHimself.cs` → consider `SelfUpdate.cs`
- `BenderSendsNotificationsWithIssues.cs` → rename
- `BenderUpdatesIssuesHimself.cs` → rename

#### Namespaces (all .cs files)
- `namespace Bender` → `namespace Preesta` (and all `using Bender.` → `using Preesta.`)

#### Classes
- `BenderSendsLetter` → `PreestaSendsLetter` or `SendsNotification`
- `BenderMakesUpdateHimself` → `SelfUpdate`
- Test classes similarly

#### String literals / config
- `Program.cs`: help text "Bender must always be run..."
- `XmlRulesConfig.cs`: `GetManifestResourceStream("Bender.rules.xsd")` → `"Preesta.rules.xsd"`
- `appsettings.json`: Serilog `Using` array `"Bender"` → `"Preesta"`
- `AssemblyInfo.cs`: comment about "Bender.ILogger"

#### Docker / CI
- `Dockerfile`: all `/app/Bender`, `/usr/local/bin/bender`, `bender-cron` refs
- `docker-publish.yml`: `IMAGE_NAME: bender` → `preesta`
- `LABEL org.opencontainers.image.source` → update to new repo URL
- `preesta-cron`: update `/app/Bender` → `/app/Preesta`

#### Test data (LEAVE AS-IS)
- JIRA issue keys like `BENDER-2301` in JSON test fixtures — these are test data, not brand
- JQL strings `project=BENDER` in tests — same
- Redirection rule `from="Bender"` in XmlConfigTests — test data

### Phase 7 (OPTIONAL): Newtonsoft.Json → System.Text.Json
- **Decision: NOT doing this.** Newtonsoft works fine, entire Jira REST client is built on `dynamic` deserialization. Rewriting would be massive with no practical benefit.

## Dependency Order

```
Phase 1 ✅
  ↓
Phase 2 (Unity → M.E.DI) — depends on Phase 1
  ↓
Phases 3, 4, 5 — independent of each other, can run in parallel
  ↓
Phase 6 (Rebranding) — LAST
```

## Other Notes

- `Nito.AsyncEx` — used only for `.WaitAndUnwrapException()` in `Connection.cs`. Ideally replace with proper async/await throughout, but this is a significant refactor. Defer.
- T4 templates — work on .NET 8, tooling is aging. Consider Razor/Source Generators in future. Not urgent.
- supercronic in Dockerfile — v0.1.12, consider updating to v0.2.x
- Bender history mention in future README: "Preesta started in 2019 as 'Bender'. In 2026 it was rebuilt on .NET 8 and renamed."
