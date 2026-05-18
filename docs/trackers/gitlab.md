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
  - type: gitlab
    group: morning
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

**At least one chip must be set.** GitLab's GraphQL refuses unfiltered scans, and Preesta drops empty rules with an `Error` log — see [Impersonal rules → Filters are impersonal too](../concepts/obezlichennye-rules.md#the-non-obvious-follow-on-filters-are-impersonal-too).

## Issues only, no MRs

Phase 13 covers Issues only. Merge Requests are deferred — GitLab's GraphQL exposes MR listings under `Project.mergeRequests` / `Group.mergeRequests`, which requires a different rule shape with mandatory project/group scope. Once added it will be a separate `type: gitlab-mr` rule.

## Issue mapping

| GitLab field | Preesta `Issue` field |
|---|---|
| `reference(full: true)` (`group/project#42`) | `Key` |
| `id` (`gid://gitlab/Issue/N`) | `GitlabGlobalId` — `{{@issueId}}` |
| `title` | `Summary` |
| `webUrl` | `Url` |
| `state` (opened/closed) → title-case | `Status` |
| closed → `"Closed"` | `Resolution` |
| `author` | `Reporter` + `Creator` |
| `assignees.nodes[0]` | `Assignee` |
| `labels.nodes[].title` | `Labels` (note: GitLab uses `title`, not `name`) |
| `milestone.title` | `ProjectKey` |
| `createdAt` / `updatedAt` | `CreatedDate` / `UpdatedDate` |

**Hidden email.** GitLab returns `null` for `User.publicEmail` when the user hasn't exposed it in profile settings. Same skip-on-empty behaviour as GitHub.

## Mutations

Raw GraphQL bodies against `https://gitlab.com/api/graphql` (or your self-hosted endpoint):

```yaml
- type: gitlab
  filter:
    state: opened
    labelName: [stale]
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
