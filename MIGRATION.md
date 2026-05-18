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

### Phase 13: Plane support — dropped
- Briefly merged then ripped out after live review. Plane's public REST `list-issues` endpoint deliberately doesn't support server-side filter chips (verified against developers.plane.so/api-reference/issue/list-issues and `makeplane/plane` source `apps/api/plane/api/views/issue.py`). The endpoint accepts only `cursor` / `per_page` / `expand` / `external_id` / `external_source` / `fields` / `order_by` — `priority`, `state`, `assignees`, `labels`, `search` are silently dropped. Live verification: `?priority=urgent` returned the full project on a workspace where only 1 of 7 items was urgent
- Full filtering happens client-side in Plane's own web app via a separate internal API that requires session cookies, not a PAT. Supporting Plane via PAT would require either downloading every work item in a project and filtering in-process (unacceptable at any non-toy scale) or wiring through Plane Views — a parallel rule-shape exclusive to one tracker
- Plane has ~49k GitHub stars but the audience is OSS users running the web app, not integrators. Decision: not worth a tracker-specific workaround. Revert in full

### Phase 12.5: GitHub Issues support ✅
- Third tracker alongside Jira and Linear. New `GithubGraphQL/` project (mirror of `LinearGraphQL/`) with `IGithubGateway` + `GithubConnection` — single `Query()` method, `Authorization: Bearer <token>` (unlike Linear's raw header), required `User-Agent` header
- `GithubRule.Filter` is a **single raw GitHub search string** (e.g. `"is:open is:issue org:bigcorp label:urgent"`) — no AI/raw/view trichotomy because GitHub's search syntax already covers multi-repo, org-wide, and PR-vs-issue distinctions in one human-readable expression. Multi-repo and org-wide selection live inside the string via `repo:` / `org:` / `user:` qualifiers
- `mutations:` parsed as raw GraphQL bodies (same shape as Linear) — power-user hook with `{{@issueId}}` / `{{@assignee.email}}` markers for substitution
- `GithubIssueSource` issues one `search(query, type: ISSUE, first: 100)` GraphQL request per rule. `type: ISSUE` covers both real issues and pull requests (a PR is an Issue subtype in GitHub's model); `__typename` discriminates and maps to `Issue.Type` of `"Issue"` or `"PR"`
- Issue mapping: `repository.nameWithOwner + "#" + number` → `Key` (e.g. `octo/repo#42`), `id` → new `Issue.GithubNodeId` (mutation target — `{{@issueId}}` marker now falls back `LinearId ?? GithubNodeId`), `assignees.nodes[0]` → `Participants.Assignee`, `author` → `Participants.Reporter` + `Participants.Creator` (no separate reporter, mirror Linear), `labels.nodes[].name` → `Labels`, `milestone.title` → `ProjectKey`, `state` (OPEN/CLOSED) → `Status` + `Resolution="Closed"` when closed
- Hidden email handling: GitHub returns empty string when user has hidden their email. We keep the `User` object (login + display name) but set `Email=""` so marker-resolution skips that recipient cleanly instead of producing a `To: ` line with an empty address
- Obezlichennye-rules contract preserved — filter strings are impersonal (no `assignee:@me` / `author:@me`); per-recipient routing lives in `notification.mailTo: assignee` + the `slackUsers:` / `telegramUsers:` (email→ID) maps. Verified by `LinearGroupingByAssigneeTests` (3 tests, transferable assumption — `IssueSupplier<TRule>` is shared)
- `ILinearMutationHandler` renamed to `IGraphQLMutationHandler` — the interface was already source-agnostic, so the GitHub executor plugs into the same `ReactionPipeline` slot via the renamed property
- `GithubIssueSupplier` extends `IssueSupplier<GithubRule>` exactly like Linear's; `GithubMutationExecutor` mirrors `LinearMutationExecutor` against the new gateway
- DI registers a `"Github"` keyed pipeline iff `Github:token` is set; `Application.cs` resolves it via the same `TryResolveNotificationPipe` pattern as Linear. No behaviour change when token is absent
- Tests: 126 → 150 (3 Linear grouping regression + 3 YAML config parse + 14 IssueSource mapping + 4 MutationExecutor)
- **Required PAT scopes** (live-discovered): `repo` (or `public_repo`) + `user:email` (or `read:user`). GitHub's GraphQL fails the entire `search` query with `INSUFFICIENT_SCOPES` if `user.email` is requested without the email scope — there is no per-field fallback. Documented in README; secrets stub in `appsettings.yaml` carries the same hint

### Phase 13: GitLab Issues support ✅
- Fourth tracker alongside Jira, Linear, and GitHub. New `GitlabGraphQL/` project (mirror of `GithubGraphQL/`) with `IGitlabGateway` + `GitlabConnection` — single `Query()` method, `Authorization: Bearer <token>` header. Default endpoint `https://gitlab.com/api/graphql`; self-hosted instances override via `Gitlab:apiBase`
- **Filter shape: structured chips, not a search string.** GitLab — unlike GitHub — does not expose a single human-readable search-string language for issues. The web UI builds queries by stacking filter chips (state, label, assignee, milestone, …); we mirror that taxonomy directly into YAML as a sub-mapping where each chip is a named field on `GitlabFilter` (`state`, `labelName`, `assigneeUsernames`, `authorUsername`, `milestoneTitle`, `search`, `confidential`, `createdAfter`/`Before`, `updatedAfter`/`Before`, `iids`). Field names match GraphQL's `Query.issues` argument names exactly so the parsed filter object is forwarded verbatim as GraphQL variables — no DSL translation step. At least one chip must be set: GitLab refuses unfiltered scans
- **Issue vs MR**: covers **Issues only** in this phase. GitLab's GraphQL has no top-level `Query.mergeRequests` field — MR listings live under `Project.mergeRequests` / `Group.mergeRequests`, which would require a different rule shape (mandatory `groupFullPath:` etc.). Deferred to a follow-up so the MVP lands cleanly with one query + one rule shape. `Issue.Type` always `"Issue"` for `type: gitlab` rules
- `mutations:` parsed as raw GraphQL bodies (same shape as Linear / GitHub) — power-user hook with `{{@issueId}}` falling back through `LinearId ?? GithubNodeId ?? GitlabGlobalId`. GitLab's global IDs are `gid://gitlab/Issue/N` strings returned by `Issue.id`
- `GitlabIssueSource` issues one `issues(...)` GraphQL request per rule with only the configured chip fields materialised into `$variables` (no `null` clutter). Issue mapping:
  - `reference(full: true)` → `Key` as `group/project#42` (mirror of GitHub's `owner/repo#number`)
  - `webUrl` → `Url`; `state` → `Status` (title-case, "Opened" / "Closed"); closed → `Resolution="Closed"`
  - `id` (gid://) → `Issue.GitlabGlobalId` for `{{@issueId}}` mutations
  - `author` → `Reporter` + `Creator` (no separate reporter in GitLab); `assignees.nodes[0]` → `Assignee`
  - `labels.nodes[].title` → `Labels` (note: GitLab's `Label` type uses `title`, not `name`)
  - `milestone.title` → `ProjectKey`
- **Hidden email handling**: GitLab returns `null` for `User.publicEmail` when the user hasn't exposed it in profile settings. We keep the User object (login/displayName useful for the digest header) but set `Email=""` so the marker resolver simply skips routing for that issue — same contract as GitHub's hidden-email behaviour
- Obezlichennye-rules contract preserved — filter chips are impersonal (no `assignee:@me` equivalent); per-recipient routing lives in `notification.mailTo: assignee` + the workspace-level `slackUsers:` / `telegramUsers:` (email→ID) maps. `IssueSupplier<TRule>` grouping is shared with Jira/Linear/GitHub so no extra regression test needed
- `GitlabIssueSupplier` extends `IssueSupplier<GitlabRule>` exactly like Linear / GitHub; `GitlabMutationExecutor` mirrors `LinearMutationExecutor` / `GithubMutationExecutor` against the new gateway
- DI registers a `"Gitlab"` keyed pipeline iff `Gitlab:token` is set; `Application.cs` resolves it via the same `TryResolveNotificationPipe` pattern as Linear / GitHub. Self-hosted root URI for the fallback "Open in GitLab" link derived from `Gitlab:apiBase` (strip `/api/graphql`). No behaviour change when token is absent
- Digest header gets a `Filter: state=opened  label=urgent  assignee=alice  …` chip line via `GitlabFilter.ToHumanString()` (mirror of GitHub's `Search: …` and Linear's `AI filter: «…»`)
- `YamlRuleEntry.Filter` loosened from `string?` to `object?` so the same `filter:` YAML key carries either a scalar (Linear AI prompt / GitHub search string) or a mapping (GitLab chips); Linear/GitHub paths cast to string and treat non-string as missing
- Required PAT scope: `read_api` for read-only digests; `api` if `mutations:` are also configured. `Gitlab:apiBase` defaults to `https://gitlab.com/api/graphql` so SaaS users only configure the token
- Tests: 150 → 175 (16 IssueSource mapping + 4 MutationExecutor + 5 YAML config parsing)

### Phase 13: Shortcut Issues support ✅
- Fourth tracker alongside Jira, Linear and GitHub. New `ShortcutRest/` project — REST-only (unlike Linear/GitHub which are GraphQL), with `IShortcutGateway` + `ShortcutConnection` covering search/members/workflows reads plus a generic `Send(verb, path, body)` for mutations
- Auth: `Shortcut-Token: <token>` custom header — neither `Authorization: Bearer …` (GitHub) nor the raw-value `Authorization: …` (Linear). Token generated at `app.shortcut.com/settings/account/api-tokens`; one token per workspace
- `ShortcutRule.Filter` is a **single raw Shortcut search string** (e.g. `state:"In Progress" type:bug !is:archived`) — Shortcut's own search-operator syntax already covers projects, teams, labels, owners and dates in one human-readable expression, so no AI/raw/view trichotomy needed (mirror of GitHub's design choice)
- `mutations:` parsed as raw REST (same `verb` / `urlPattern` / `body` shape as Jira) — power-user hook with `{{@issueId}}` / `{{@title}}` / `{{@assignee.email}}` markers. Unlike Linear/GitHub there is no GraphQL alternative — Shortcut doesn't ship one
- `ShortcutIssueSource` issues one `GET /api/v3/search/stories?query=…&page_size=100&detail=slim` per rule. `detail=slim` drops description + comments from each result (we don't render either); slim keeps responses light when a filter matches dozens of stories
- **Foreign-ID resolution**: Shortcut returns integer workflow state IDs and UUID member IDs on each story. Resolving them to readable names + emails takes two auxiliary REST roundtrips (`GET /api/v3/workflows`, `GET /api/v3/members`); both are run lazily on first `GetIssues` via `Lazy<T>` and cached for the lifetime of the source instance. Failure to fetch either falls back to raw ID as display name with empty email so obezlichennye-rules routing (`mailTo: assignee`) skips cleanly rather than producing a `To: <uuid>` line
- Issue mapping: `id` → `Key = "sc-{id}"` (Shortcut's own branch-naming convention) + new `Issue.ShortcutId` (mutation target — `{{@issueId}}` marker fallback now `LinearId ?? GithubNodeId ?? ShortcutId`), `app_url` → `Url`, `workflow_state_id` resolved via cache → `Status`, `story_type` (feature/bug/chore) → `Type`, `owner_ids[0]` → `Participants.Assignee`, `requested_by_id` → `Participants.Reporter` + `Participants.Creator` (no separate reporter, mirror Linear/GitHub), `labels[].name` → `Labels`, `deadline` → `DueDate`, `created_at` / `updated_at` → UTC dates
- `ShortcutIssueSupplier` extends `IssueSupplier<ShortcutRule>` exactly like Jira's — no `GetMutationPackages` override needed because the inherited default emits `Package<SelfUpdate, Issue>` from `Rule.Mutations`, which is exactly what Shortcut REST needs
- `ShortcutMutationExecutor` implements the existing `IHttpHandler` (not `IGraphQLMutationHandler`) — Shortcut goes through the same `Package<SelfUpdate, Issue>` → `HttpRequest` path Jira already uses. Extracts path+query from the absolute URI on each `HttpRequest` and routes through the same Shortcut-Token connection as the read path; per-request failures logged at Error and swallowed
- DI registers a `"Shortcut"` keyed pipeline iff `Shortcut:apiToken` is set; `Application.cs` resolves it via the same `TryResolveNotificationPipe` pattern as Linear and GitHub. No behaviour change when token is absent
- Tests: 150 → 175 (17 IssueSource mapping + 4 MutationExecutor + 3 YAML config + 1 marker fallback chain)
- Open design decisions documented in code: Story.Key = `sc-{id}` (Shortcut doesn't expose project abbreviation on search-result payload); owner-email resolution is a one-time members fetch at first use, not per-story — large workspaces stay within rate limits because workflows + members are cached for the supplier's lifetime

### Custom Fields (Jira) ✅
- User just writes `columns: [Status, Priority, Severity, "Story point estimate"]` in `rules.yaml` — no `customfield_NNNNN` ids in any config. Auto-discovery via `GET /rest/api/?/field` at startup builds a case-insensitive display-name → id map
- `Issue.CustomFields: Dictionary<string, JToken?>` carries the raw payload (shape preserved — scalar/array/object) so the formatter can decide rendering. Linear-sourced issues leave it empty
- `JToken.ToIssue` filters `issue.fields.Properties()` by `customfield_` prefix
- New `IssueFormatter.RenderCustomFieldValue` helper handles common Jira shapes: scalar, `JArray<string>` (commaj-oined), `JArray<JObject>` with `name`/`value`/`displayName` keys (multi-select), single-select `JObject`, fallback compact JSON
- `all-non-empty` magic expansion now also includes discovered custom field names; rule columns referencing custom field names by display name resolve through the map; empty/missing values render as nothing (no crash)
- Server Jira parity: `Connection.GetIssuesFromJql` now explicit `fields=*all` (Cloud already had this in POST body)
- Duplicate display names: log warning, first id wins. Endpoint failure (HTTP error / network): log warning, empty map, pipeline keeps working
- Tests: 114 → 126 (CustomFieldDiscoveryTests x3, CustomFieldRenderingTests x7, JTokenConvertTests x1, IssueFormatter e2e x1)
- Live-validated against `valevitov.atlassian.net` — SCRUM-7's `Story point estimate` shows up in the e2etest digest after a one-time auto-discovery, no user configuration required

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
