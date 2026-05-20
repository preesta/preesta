# Adding a tracker

Blueprint for the next integration. Modeled on Phase 12.5 (GitHub) and Phase 13 (GitLab, Shortcut) — see `git log --grep "^Github:"` or `^Gitlab:` for the actual commit sequence those phases produced.

## Pre-work: does the tracker fit?

Before writing code, answer five questions:

1. **Does the tracker have a public API at all?** Many in-app "automation" features are UI-only.
2. **Is authentication via PAT / API key / OAuth?** Preesta is built for service-style auth, not OAuth-flow-on-every-fetch.
3. **Does the issue-list endpoint support server-side filtering?** Filter chips (priority, state, labels, assignees) must work in the API, not just the UI. If filtering is UI-only, the tracker isn't a fit. **This is non-negotiable.**
4. **Is the API stable?** A tracker that's deprecating its REST API for GraphQL next quarter is more work than payoff.
5. **Is there an audience?** Useful but-niche tracker is fine for a personal fork; mainline integration wants users.

If any answer is "no" or "unclear", document the finding in MIGRATION as a deferred / dropped item rather than starting the integration.

## The shape

Every tracker integration is the same six pieces:

1. **Transport project** — `XxxGraphQL/` or `XxxRest/`, a thin wrapper over `HttpClient`. One `Query()` (GraphQL) or `Send()` (REST) method, just enough to keep auth + serialization in one place.
2. **`Xxx:token` in `AppSettings.cs`** — credential reader.
3. **`XxxRule` + `GetXxxRules` in `YamlRulesConfig.cs`** — rule shape and parsing.
4. **`XxxIssueSource`** — fetch + mapping into the shared `Issue` model.
5. **`XxxIssueSupplier : IssueSupplier<XxxRule>`** — group-by-recipient via the inherited base class; override `Enrich` to attach package-level metadata (filter description, "Open in Xxx" URL).
6. **`XxxMutationExecutor`** — implements `IHttpHandler` (REST) or `IGraphQLMutationHandler` (GraphQL). Logs + swallows per-mutation failures.

Plus DI wire-up in `DependencyContainer.cs`, `Application.cs` resolves the new pipeline, `IRulesConfig` / `XmlRulesConfig` get the new method.

## Commit sequence (recommended)

Phase 12.5 hit these in order — keep each commit small and self-contained:

1. **Transport project** — `XxxGraphQL/XxxConnection.cs` + `IXxxGateway.cs` + `.csproj`, sln add.
2. **AppSettings** — add `Xxx:token` reader + placeholder in `appsettings.yaml`.
3. **Rule + YAML parsing** — `XxxRule.cs`, `GetXxxRules` in `YamlRulesConfig`, validation tests in `Tests/YamlConfigTests.cs`.
4. **IssueSource** — fetch + mapping. Tests in `Tests/Xxx/XxxIssueSourceTests.cs` via NSubstitute on `IXxxGateway`.
5. **Supplier + DI wire-up + Application.cs** — make the pipeline routable.
6. **MutationExecutor** — write-side. Tests.
7. **Docs** — `docs/trackers/xxx.md`, MIGRATION entry, update `docs/index.md`'s tracker table.

The split keeps each PR reviewable. A typical phase comes out to 7-9 commits.

## What to imitate

Read these in order before starting:

1. **`Preesta/GithubIssueSource.cs`** — single GraphQL query, clear MapNode method, hidden-email handling.
2. **`Preesta/Data/Supplying/GithubIssueSupplier.cs`** — minimum viable supplier: ctor takes source + jira service + rules + logger, override `GetMutationPackages`, override `Enrich`.
3. **`Preesta/Configuration/Action/GithubRule.cs`** — minimum viable rule.
4. **`Tests/Github/GithubIssueSourceTests.cs`** — 14 tests covering shape, edge cases (hidden email, missing assignee), error envelope.
5. **`Preesta/Configuration/YamlRulesConfig.cs:GetGithubRules`** — the rule converter and its validation.

## What to imitate per-API-style

- **GraphQL tracker** — mirror Linear / GitHub / GitLab. Single `Query()` over the endpoint, raw mutation bodies.
- **REST tracker with search** — mirror Shortcut. `Search()` method on the gateway, raw-string `filter:`.
- **REST tracker without search** — mirror Jira. Tracker-specific query language (JQL) embedded as a string.

If the tracker is REST-only with **no server-side filter chips** — stop. Document the finding and don't ship.

## What not to do

- **Don't invent a DSL for the filter.** Use whatever the tracker's web UI accepts. Users already think in that syntax.
- **Don't add identity to filters.** No `assignee:@me` patterns in examples. The routing layer handles per-recipient.
- **Don't skip the "no unfiltered rules" validator.** A rule with empty filter scans the whole tracker; that's never what the user wants. Drop with `ILogger.Error`. Mirror `GitlabRule`'s `HasAnyField` check.
- **Don't pile on shared-code refactors.** Each new tracker should *add* to shared interfaces (Issue fields, marker fallbacks), not rename or restructure existing ones. The `LinearId ?? GithubNodeId ?? GitlabGlobalId ?? ShortcutId` fallback chain in `ReplaceKnownMarkers` is the canonical pattern.
- **Don't ship without browser verification.** Run the digest live, click every link in the email, confirm the "Open in <tracker> →" lands where you expect and the per-issue links route correctly.

## Test coverage target

Per-tracker: 20-25 unit tests covering Source mapping (one test per non-trivial field), MutationExecutor (happy + GraphQL/HTTP error + empty), YAML config parsing (every required-field validation gets its own test). Plus 1-3 supplier tests for the `Enrich` step. The aggregate across the four current trackers is ~85 tests; aim for the same ballpark.
