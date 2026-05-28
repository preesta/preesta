# GitHub

Preesta talks to GitHub via the **GraphQL `search(type: ISSUE)`** endpoint. `type: ISSUE` covers both real issues and pull requests (a PR is a subtype of Issue in GitHub's data model); `__typename` discriminates and maps to `Issue.Type` of `"Issue"` or `"PR"`.

Multi-repo / org-wide / PR-vs-issue selection all live **inside the user's filter string** via GitHub's standard search qualifiers (`repo:`, `org:`, `is:issue`, `is:pr`, …). No DSL, no per-field YAML — whatever you would paste into the web UI's search bar works in `filter:`.

## 1. Create a Personal Access Token

[github.com/settings/tokens](https://github.com/settings/tokens) → *Generate new token (classic)*.

Scopes you need **both** of:

- **`repo`** (for private repositories) or **`public_repo`** (sufficient if you only monitor public repos)
- **`user:email`** (or `read:user`) — required: GitHub's GraphQL refuses to return `User.email` without this scope, and Preesta needs that email to route digests by `assignee` / `reporter` markers via `slackUsers:` / `telegramUsers:`. Without it the whole `search` query returns `INSUFFICIENT_SCOPES` and Preesta drops the rule.

Fine-grained PATs work too — give them read access to the repos/orgs you want to monitor, plus the equivalent email-read permission.

## 2. Configure the token

In `appsettings.secrets.yaml`:

```yaml
Github:
  token: "ghp_yourClassicTokenHere"
```

## 3. Write rules

```yaml
rules:
  # All open urgent issues across an org — one digest per assignee
  - tracker: github
    tags: morning
    filter: "is:open is:issue org:bigcorp label:urgent"
    notify:
      subject: "Urgent GitHub issues"
      mailTo: assignee

  # Stale PRs across two specific repos
  - tracker: github
    tags: morning
    filter: "is:open is:pr repo:foo/api repo:foo/web review:required updated:<2026-05-01"
    notify:
      subject: "Stale PRs waiting on review"
      mailTo: reporter   # i.e. PR author
```

The `filter:` is the GitHub search query, verbatim. Multi-repo: `repo:foo/a repo:foo/b`. Whole org: `org:bigcorp`. Whole user: `user:valentinlevitov`. Issues only: `is:issue`. PRs only: `is:pr`. Both: leave the `is:` qualifier out — `type: ISSUE` covers them both.

**No identity in filters.** `assignee:@me` or `author:@me` resolve to the token's owner, which is almost never the right person. Route via the `mailTo` marker layer instead — see [Impersonal rules](../concepts/obezlichennye-rules.md).

## Issue mapping

| GitHub GraphQL field | Preesta `Issue` field |
|---|---|
| `repository.nameWithOwner + "#" + number` | `Key` (e.g. `octo/repo#42`) |
| `id` (GraphQL node id, opaque base64) | `GithubNodeId` — used by `{{@issueId}}` marker in mutations |
| `title` | `Summary` |
| `url` | `Url` |
| `state` (OPEN/CLOSED) → title-case | `Status` |
| CLOSED → `"Closed"` | `Resolution` |
| `__typename` | `Type` = `"Issue"` or `"PR"` |
| `assignees.nodes[0]` | `Participants.Assignee` |
| `author` | `Participants.Reporter` and `Participants.Creator` (GitHub has no separate reporter) |
| `labels.nodes[].name` | `Labels` (comma-joined) |
| `milestone.title` | `ProjectKey` |
| `createdAt` / `updatedAt` | `CreatedDate` / `UpdatedDate` |

**Hidden email.** GitHub returns `""` for `User.email` if the user has hidden their email in profile settings. The User object stays (login → display name shows in the digest) but `Email=""`, and the marker resolver skips routing for that recipient — see [Routing model → When the assignee has no email](../concepts/routing-model.md#when-the-assignee-has-no-email).

## Mutations

A `github` rule can carry a `mutations:` list of raw GraphQL mutations against `https://api.github.com/graphql`:

```yaml
- tracker: github
  filter: "is:open is:issue label:stale"
  mutations:
    - mutation: |
        mutation {
          addComment(input: {
            subjectId: "{{@issueId}}",
            body: "This issue has been quiet for a while — please update or close."
          }) { clientMutationId }
        }
    - mutation: |
        mutation {
          closeIssue(input: { issueId: "{{@issueId}}" }) { clientMutationId }
        }
```

`{{@issueId}}` resolves to `GithubNodeId` (the opaque base64 GraphQL node id). Other markers: `{{@issueKey}}`, `{{@title}}`, `{{@assignee.email}}`, etc. — full list at [Markers reference](../reference/markers.md).

Per-mutation failures (HTTP errors, GraphQL `errors` envelope) are logged at `Error` and skipped. Label / user / project node IDs aren't resolved by Preesta — query them once via GraphQL and paste them into the mutation body.

## "Open in GitHub →" round-trip link

Every digest section gets a clickable header link back to the same search query: `https://github.com/search?q=<filter>&type=issues`. Verified live in Chrome — opens the GitHub search results page with the filter applied.
