# GitLab

Preesta talks to GitLab via the **GraphQL `Query.issues`** field. Unlike GitHub, GitLab has no single human-readable search-string language for issues — the web UI builds queries by stacking filter chips (Assignee, Author, Label, Milestone, State, …). Preesta mirrors that taxonomy directly: each chip is a named field on a structured `filter:` mapping, with names matching `Query.issues` arguments exactly so the parsed filter is forwarded verbatim as GraphQL variables (no DSL translation).

## 1. Personal Access Token

GitLab → top-right user menu → **Edit profile → Access Tokens → Add new token**.

Required scopes:

- **`read_api`** — read-only digests
- **`api`** — only if you also configure `mutations:` (the write side requires the full API scope)

The token format is `glpat-<random>`. Sent as `Authorization: Bearer <token>` — same shape as GitHub.

## 2. Configure

```yaml
Gitlab:
  token: "glpat-yourTokenHere"
  # apiBase: "https://gitlab.example.com/api/graphql"   # only for self-hosted
```

`apiBase` defaults to `https://gitlab.com/api/graphql` — SaaS users leave it empty.

## 3. Write rules — filter chips

```yaml
rules:
  - tracker: gitlab
    tags: morning
    filter:
      state: opened
      labelName: [urgent, blocker]            # AND across labels
      assigneeUsernames: [alice, bob]         # OR across assignees
      milestoneTitle: ["Sprint 12"]
    notify:
      subject: "Urgent GitLab issues"
      mailTo: assignee
```

| Chip | Type | Notes |
|---|---|---|
| `state` | `opened` / `closed` / `all` | |
| `labelName` | string[] | AND across array |
| `assigneeUsernames` | string[] | OR across array |
| `authorUsername` | string | |
| `milestoneTitle` | string[] | |
| `search` | string | free-text in title/description |
| `confidential` | bool | true/false; omit for both |
| `createdAfter` / `createdBefore` | ISO-8601 string | |
| `updatedAfter` / `updatedBefore` | ISO-8601 string | |
| `iids` | string[] | per-project numeric IDs |

**Scope the filter to the instance you're hitting.** Chips are forwarded verbatim as `Query.issues` variables — Preesta doesn't pre-judge how broad the result is. On a busy instance (gitlab.com especially) a filter with only `state` and/or `labelName` asks the server for every matching issue across *all* projects and gets killed with a timeout (`503 Request timed out`). Add a narrowing chip — `assigneeUsernames`, `authorUsername`, `milestoneTitle`, or `iids` — so the server can bound the query. Thin `state`+`labelName` filters are fine only on a small self-hosted instance where "everything" is still a sane set. An over-broad query isn't rejected up front; it fails at fetch time, gets logged as a warning, and the rest of the run continues.

## Issues only, no MRs

Issues only for now. Merge Requests are deferred — GitLab's GraphQL exposes MR listings under `Project.mergeRequests` / `Group.mergeRequests`, which requires a different rule shape with mandatory project/group scope. They'll arrive as a separate rule shape once we tackle them.

## What appears in a GitLab digest

Each item shows the key (`group/project#42`), the title, and any `columns:` you ask for. GitLab-specific notes:

- GitLab has no separate reporter — the issue's author fills both `Reporter` and `Creator` columns.
- Milestone maps to `ProjectKey`.

**Hidden email.** GitLab returns no email when the user hasn't exposed one in profile settings. The author still shows up by display name, but `mailTo: assignee` silently skips that recipient — same as GitHub.

## Mutations

Raw GraphQL bodies against `https://gitlab.com/api/graphql` (or your self-hosted endpoint):

```yaml
- tracker: gitlab
  filter:
    state: opened
    labelName: [stale]
    assigneeUsernames: [alice]   # narrowing scope — see "Scope the filter" above
  mutations:
    - mutation: |
        mutation {
          createNote(input: {
            noteableId: "{{@issueId}}",
            body: "Stale — please update or close."
          }) { note { id } }
        }
    - mutation: |
        mutation {
          updateIssue(input: { id: "{{@issueId}}", stateEvent: CLOSE }) {
            issue { state }
          }
        }
```

`{{@issueId}}` resolves to `GitlabGlobalId` (`gid://gitlab/Issue/N`). Requires the `api` token scope (not `read_api`).

## "Open in GitLab →" round-trip link

The digest header has a clickable link to `https://gitlab.com/dashboard/issues?<chips>` with the same chips URL-encoded as GitLab's web UI format (`state=opened&label_name[]=urgent&assignee_username[]=alice`). GitLab redirects `/dashboard/issues` to `/dashboard/work_items` keeping the filters — verified live.
