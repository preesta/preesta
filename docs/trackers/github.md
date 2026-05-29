# GitHub

Preesta queries GitHub's GraphQL **search** endpoint. The same query returns issues and pull requests — GitHub treats a PR as an issue subtype — and Preesta tags each item with `Type = "Issue"` or `Type = "PR"` so you can filter or column on it.

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

## What appears in a GitHub digest

Each item shows the key (`owner/repo#42`), the title, and any `columns:` you ask for. GitHub-specific notes:

- `Type` distinguishes `Issue` from `PR` — useful as a column if you mix both in one rule.
- GitHub has no separate reporter field — author fills both `Reporter` and `Creator` columns.
- Milestones map to `ProjectKey`.

**Hidden email.** GitHub returns no email if the user has hidden it in profile settings. The author still shows up by display name, but `mailTo: assignee` (or other email markers) silently skips that recipient — see [Routing model → When the assignee has no email](../concepts/routing-model.md#when-the-assignee-has-no-email).

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
