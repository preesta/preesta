# Markers

Markers are `{{@…}}` placeholders that the engine substitutes inside mutation bodies (REST and GraphQL alike) and inside notification template recipient lists. They resolve per-issue at dispatch time.

## In `mutations:`

Body templates and URL patterns both go through marker substitution.

| Marker | Resolves to |
|---|---|
| `{{@jiraRoot}}` | The tracker's configured root URI (Jira) |
| `{{@issueKey}}` | Human-readable key — `INFRA-123`, `octo/repo#42`, `sc-26` |
| `{{@issueId}}` | The tracker's native object ID — used by GraphQL mutations (`gid://...` for GitLab, opaque base64 for GitHub, UUID for Linear, integer for Shortcut). One marker, the right format for whichever tracker the rule fires against |
| `{{@title}}` | The issue title / summary |
| `{{@assignee.email}}` | Assignee's email |
| `{{@assignee.key}}` | Assignee's login / username / UUID (tracker-dependent) |
| `{{@assignee.name}}` | Assignee's name |
| `{{@assignee.displayName}}` | Assignee's display name |
| `{{@reporter.email}}` / `.key` / `.name` / `.displayName` | Same shape, for the reporter |
| `{{@creator.email}}` / `.key` / `.name` / `.displayName` | Same shape, for the creator |

Markers that resolve to nothing render as empty strings. There's no conditional template DSL — filter at the rule level to avoid running mutations against issues missing the data you need.

## In recipient lists (`mailTo`, `cc`)

`mailTo` / `cc` accept three kinds of values, comma-separated:

| Value | Meaning |
|---|---|
| literal email (`team-lead@example.com`) | Always added to the digest's To/Cc — one-for-all |
| `assignee` | Replaced with the issue's assignee email per issue; the digest is grouped by that email |
| `reporter` | Same, for the reporter |
| `creator` | Same, for the creator |

The fan-out story is in [Impersonal rules](../concepts/obezlichennye-rules.md#how-the-dispatch-works) and the dispatch path in [Routing model](../concepts/routing-model.md).

## Not markers

These do **not** work — Preesta intentionally doesn't have them:

- `{{@me}}` / `{{@viewer}}` — there's no "current user" concept; the rule processor isn't a person
- `{{@today}}` / `{{@now}}` — date math goes into the tracker's filter syntax (`updated_at__gt`, `created_after`, JQL `now()`, etc.), not the mutation body
- Conditional logic (`{{@if ...}}`) — no template DSL; rules narrow the issue set, mutations run unconditionally on the matches

Need a marker that isn't here? The engineering walkthrough for adding one lives in the repo's `dev-notes/`.
