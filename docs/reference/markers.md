# Markers

Markers are `{{@…}}` placeholders that the engine substitutes inside mutation bodies (REST and GraphQL alike) and inside notification template recipient lists. They resolve per-issue at dispatch time.

## In `mutations:`

Body templates and URL patterns both go through marker substitution.

| Marker | Resolves to |
|---|---|
| `{{@jiraRoot}}` | Configured `Jira:rootUri` (or whichever tracker's root URI is in scope) |
| `{{@issueKey}}` | `Issue.Key` — human key like `INFRA-123`, `octo/repo#42`, `sc-26` |
| `{{@issueId}}` | First non-null of `LinearId` ?? `GithubNodeId` ?? `GitlabGlobalId` ?? `ShortcutId`. The fallback chain means one mutation body works across the three GraphQL trackers as long as the rule type matches what populates the field |
| `{{@title}}` | `Issue.Summary` |
| `{{@assignee.email}}` | `Issue.Participants.Assignee.Email` |
| `{{@assignee.key}}` | `Issue.Participants.Assignee.Key` (login / username / UUID, tracker-dependent) |
| `{{@assignee.name}}` | `Issue.Participants.Assignee.Name` |
| `{{@assignee.displayName}}` | `Issue.Participants.Assignee.DisplayName` |
| `{{@reporter.email}}` / `.key` / `.name` / `.displayName` | `Issue.Participants.Reporter.*` |
| `{{@creator.email}}` / `.key` / `.name` / `.displayName` | `Issue.Participants.Creator.*` |

Markers that resolve to null produce empty strings — a malformed mutation body is the user's responsibility to avoid (`if (issue.assignee) { … }` style logic isn't supported; filter at the rule level instead).

## In recipient lists (`mailTo`, `cc`)

`mailTo` / `cc` accept three kinds of values, comma-separated:

| Value | Meaning |
|---|---|
| literal email (`team-lead@example.com`) | Always added to the digest's To/Cc — one-for-all |
| `assignee` | Replaced with `issue.Participants.Assignee.Email` per issue, then the package is grouped by that email |
| `reporter` | Same with `Reporter.Email` |
| `creator` | Same with `Creator.Email` |

The fan-out story is in [Impersonal rules](../concepts/obezlichennye-rules.md#how-the-dispatch-works) and the dispatch path in [Routing model](../concepts/routing-model.md).

## Not markers

These do **not** work — Preesta intentionally doesn't have them:

- `{{@me}}` / `{{@viewer}}` — there's no "current user" concept; the rule processor isn't a person
- `{{@today}}` / `{{@now}}` — date math goes into the tracker's filter syntax (`updated_at__gt`, `created_after`, JQL `now()`, etc.), not the mutation body
- Conditional logic (`{{@if ...}}`) — no template DSL; rules narrow the issue set, mutations run unconditionally on the matches

Add a missing marker by extending `IssuePackageConverter.ReplaceKnownMarkers`. New markers should resolve to a string (or empty string for null) and never throw.
