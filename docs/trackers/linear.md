# Linear

Linear was Preesta's first non-Jira tracker (Phase 12). It's the reference implementation of the GraphQL pattern — single `Query()` over a GraphQL endpoint, raw-mutation write side, three different ways to express a filter depending on how the user thinks about the issue list.

## 1. Get the API key

Linear web app → bottom-left avatar → **Settings → Account → Security & Access → API → Personal API Keys** → *New API key*.

The key looks like `lin_api_<random>`. Linear's auth is unusual — the key goes **raw** in the `Authorization` header, **not** prefixed with `Bearer`:

```http
Authorization: lin_api_xxxxxxxxxxxxxxxxxxxxxxxxxx
```

`LinearConnection` handles this automatically.

## 2. Configure the key

```yaml
Linear:
  apiKey: "lin_api_yourKeyHere"
  workspace: "your-workspace-slug"   # the segment in linear.app/<slug>/...
```

`workspace:` is used to build "Open in Linear →" URLs for saved views (the only filter mode that has a canonical sharable URL — see below).

## 3. Filter modes — pick one

Linear rules support **three mutually-exclusive** ways to say "which issues":

### a) AI prompt (`filter:`) — primary

```yaml
- tracker: linear
  filter: "issues assigned to me, not completed"
```

Internally a two-hop GraphQL fetch: Linear's `issueFilterSuggestion(prompt:)` API translates the prompt into a filter object, then `issues(filter:)` runs it. The advantage is human-readable rules. The disadvantage is the AI sometimes misreads boolean logic — for "not blocked OR overdue" type queries it tends to flip ANDs and ORs. For those, use `filterRaw`.

### b) Raw GraphQL filter (`filterRaw:`) — escape hatch

```yaml
- tracker: linear
  filterRaw:
    and:
      - state:
          type:
            neq: completed
      - or:
          - hasBlockedByRelations:
              eq: false
          - dueDate:
              lte: P0D
```

The mapping goes verbatim into `issues(filter:)`. You write the filter exactly as Linear's GraphQL schema documents it. Used when AI prompt produces wrong logic, or when you want unambiguous review-able filters.

### c) Saved view (`viewId:`) — escape hatch

```yaml
- tracker: linear
  viewId: "0e8a3b41-1234-4321-aaaa-bbbbbbbbbbbb"
```

You build the view in Linear's UI, copy its UUID from the URL, paste here. Preesta uses `customView(id:){ issues { nodes { ... } } }` and gets back exactly what the view evaluates to. Bonus: `viewId` mode is the only mode with a canonical shareable URL, so the digest header gets a real "Open in Linear →" link to `linear.app/<workspace>/view/<id>`. For the other two modes the link is omitted (Linear stores AI prompts and raw filters in localStorage, not URL).

Exactly one of the three must be set. Rules with zero or 2+ are dropped and an error appears in the log.

## Issue mapping

| Linear GraphQL field | Preesta `Issue` field |
|---|---|
| `identifier` (e.g. `PRE-42`) | `Key` |
| `id` (UUID) | `LinearId` — used by `{{@issueId}}` marker in mutations |
| `title` | `Summary` |
| `url` | `Url` |
| `state.name` | `Status` |
| `state.type == "completed"` ? `state.name` : null | `Resolution` |
| `priorityLabel` (`Urgent` / `High` / …) | `Priority` |
| `assignee` | `Participants.Assignee` |
| `creator` | `Participants.Reporter` **and** `Participants.Creator` (Linear has no separate reporter) |
| `labels.nodes[].name` | `Labels` |
| `project.name` | `ProjectKey` |
| `dueDate`, `createdAt`, `updatedAt` | `DueDate`, `CreatedDate`, `UpdatedDate` |

## Mutations

Raw GraphQL bodies against `https://api.linear.app/graphql`:

```yaml
- tracker: linear
  filter: "issues in 'Done' with no assignee"
  mutations:
    - mutation: |
        mutation {
          commentCreate(input: {
            issueId: "{{@issueId}}",
            body: "Auto-closed without owner — please add resolution notes."
          }) { success }
        }
    - mutation: |
        mutation {
          issueUpdate(id: "{{@issueId}}", input: { assigneeId: null }) { success }
        }
```

`{{@issueId}}` resolves to `LinearId` (the UUID). State / label / user IDs aren't resolved by Preesta — Linear's GraphQL schema offers `team.states`, `team.labels`, `users(filter:)` to look them up; paste the IDs into the mutation body once.

Per-mutation failures (HTTP errors, GraphQL `errors` envelope) are logged at `Error` and skipped — one bad mutation never stops the others.
