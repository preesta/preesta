# Testing

Preesta has ~200 unit and integration tests, all in-process. Running them is `dotnet test`.

## What runs in CI

GitHub Actions on every push and PR:

- `dotnet build` against .NET 8.0
- `dotnet test` — full test suite
- `mkdocs build` — verify the docs site builds (catches broken internal links)

## Test categories

| Category | Where | What it covers |
|---|---|---|
| Unit | `Tests/*.cs` (top-level) | Pure functions: `JTokenConvert`, `FilteringPredicates`, `Grouping`, `IssuePackageConverter`, mailing setup |
| Per-tracker mapping | `Tests/{Github,Gitlab,Linear,Shortcut}/*SourceTests.cs` | Each source's API-response → Issue mapping, via NSubstitute on the gateway interface. Stress edge cases: hidden email, missing assignee, GraphQL errors, empty result |
| Per-tracker mutations | `Tests/{Github,Gitlab,Linear,Shortcut}/*MutationExecutorTests.cs` | Mutation pipeline: happy path, error envelope, HTTP exception, empty input |
| Per-tracker integration | `Tests/{Github,Gitlab,Linear}/*WireMockTests.cs` (where present) | End-to-end with WireMock.Net stubs — tests the HTTP layer, not just mapping |
| YAML config | `Tests/YamlConfigTests.cs` | Per-rule-type parsing + validation (missing required fields, mode conflicts, empty filters) |
| Formatting | `Tests/Formatting/*Tests.cs` | Render output: column expansion, custom field rendering, Slack mrkdwn chips |
| End-to-end | `Tests/End2End/*` | Full pipeline against WireMock stubs |

## Running locally

```bash
dotnet test                                       # whole suite
dotnet test --filter "FullyQualifiedName~Github"  # just GitHub tests
dotnet test --filter "Category!=Slow"             # skip the slow ones (if you mark some [Category("Slow")])
```

## Mock servers

`Tests/Mocks/` has WireMock-based HTTP server stubs for trackers that have real integration tests:

- `MockJiraServer` — handles `/rest/api/2/search` etc.
- `MockLinearServer` — GraphQL responses with `issues` / `customView` envelopes
- `MockSlackServer` — `chat.postMessage` responses

When adding a new tracker, consider adding a `Mock<TrackerName>Server` — it pays off the second time you write a test that needs HTTP behavior, not just mapping.

## NSubstitute vs Moq

The codebase uses **NSubstitute**. The migration from Moq happened in Phase 5; new tests should use NSubstitute. The `StubDelegatingHandler` helper in `Tests/` handles HTTP-level mocking for the few places NSubstitute can't.

## Writing a new tracker's tests

Mirror `Tests/Github/`:

1. `<Tracker>IssueSourceTests.cs` — one test per non-trivial mapping field, plus error-path tests
2. `<Tracker>MutationExecutorTests.cs` — happy + error envelope + HTTP exception + empty input
3. Entries in `YamlConfigTests.cs` — required-field validation, parse correctness, common malformations

Target ~20-25 tests per tracker. Less than 15 → mapping is probably under-tested. More than 30 → look for shared setup that could move to helpers.

## Coverage

There's no code-coverage threshold enforced in CI. The mental rule: every new method gets either a unit test or an integration test before merging. The `IsKnownColumn` style "switch with one case per value" methods don't need tests (the value is its own contract), but anything with branching does.

## Debugging a failing test

`dotnet test --logger "console;verbosity=detailed"` for full output. For a single test:

```bash
dotnet test --filter "FullyQualifiedName=Tests.Github.GithubIssueSourceTests.HiddenEmail_ProducesEmptyString_NotNull"
```

Set a breakpoint in your IDE (VS Code, Rider, VS) and use the IDE's test runner — `dotnet test` doesn't attach a debugger.
