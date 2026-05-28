# Shortcut

REST-only (no GraphQL), so structurally closer to Jira than to the GraphQL trackers. Selection is a **raw Shortcut search string** — the same syntax the in-app Search Stories box accepts.

## 1. API token

[app.shortcut.com/settings/account/api-tokens](https://app.shortcut.com/settings/account/api-tokens) → *Generate Token* → copy the value.

The Read-only checkbox controls whether the token can run mutations. Leave it **off** if you plan to use `mutations:`, on otherwise.

Format: `sct_rw_<workspace>_<random>` (read-write) or `sct_ro_<workspace>_<random>` (read-only). Sent as `Shortcut-Token: <token>` — a custom header, not `Authorization: Bearer`.

## 2. Configure

```yaml
Shortcut:
  apiToken: "sct_rw_workspace_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
```

The token's workspace is implicit — Preesta calls `GET /api/v3/member` once at startup and reads `workspace2.url_slug` to build `app.shortcut.com/<slug>/…` deep links.

## 3. Write rules

```yaml
rules:
  - tracker: shortcut
    tags: morning
    filter: "state:\"In Progress\" type:bug !is:archived"
    notify:
      subject: "Open Shortcut bugs"
      mailTo: assignee

  - tracker: shortcut
    filter: "team:platform !state:completed has:deadline deadline:<today"
    notify:
      subject: "Overdue Shortcut stories"
      mailTo: reporter
```

`filter:` is Shortcut's own search-operator syntax — `state:"..."`, `type:`, `label:`, `owner:`, `requester:`, `has:`, `is:`, `deadline:`, `team:`, with `!` to negate. The web UI uses exactly the same syntax in its top search bar.

**No identity in filters.** `owner:me` / `requester:me` resolve to the token's owner, which is almost never who the digest is for. Use [`mailTo` markers](../concepts/obezlichennye-rules.md) for per-recipient fan-out.

## Foreign-ID resolution

Shortcut's search response gives you workflow state IDs (integers) and owner IDs (UUIDs), not names or emails. Preesta resolves them via two auxiliary REST calls cached per process:

- `GET /api/v3/workflows` → workflow state ID → human-readable state name (`"In Progress"`, `"Done"`, …)
- `GET /api/v3/members` → owner UUID → `{ name, email }`

Both are fetched lazily on the first `GetIssues` call. Failures (HTTP error, restricted token) log a warning and degrade gracefully — owner email becomes empty, marker routing skips that recipient, the digest still goes to others.

## Issue mapping

| Shortcut field | Preesta `Issue` field |
|---|---|
| `id` (integer) | `Key = "sc-{id}"` (Shortcut's own branch-naming convention) |
| `id` (stringified) | `ShortcutId` — `{{@issueId}}` |
| `name` | `Summary` |
| `app_url` | `Url` |
| `workflow_state_id` → cache lookup | `Status` |
| `story_type` (`feature` / `bug` / `chore`) | `Type` |
| `owner_ids[0]` → cache lookup | `Participants.Assignee` |
| `requested_by_id` → cache lookup | `Participants.Reporter` + `Participants.Creator` |
| `labels[].name` | `Labels` (comma-joined) |
| `deadline` | `DueDate` |
| `created_at` / `updated_at` | `CreatedDate` / `UpdatedDate` (UTC) |

Resolution isn't separately exposed by Shortcut — the workflow state name carries that meaning (`"Completed"`, `"Cancelled"`).

## Mutations

REST `verb` / `urlPattern` / `body` shape, same as Jira:

```yaml
- tracker: shortcut
  filter: "state:\"Ready for Review\" updated:<-7d"
  mutations:
    - verb: POST
      urlPattern: "https://api.app.shortcut.com/api/v3/stories/{{@issueId}}/comments"
      body: |
        { "text": "Stale — please update or close." }
    - verb: PUT
      urlPattern: "https://api.app.shortcut.com/api/v3/stories/{{@issueId}}"
      body: |
        { "archived": true }
```

`{{@issueId}}` is the numeric story ID. Requires a `sct_rw_*` (write-enabled) token. Per-mutation failures are logged + skipped.

## "Open in Shortcut →" round-trip link

The digest header link points at `https://app.shortcut.com/<workspace>/search#<filter>` — Shortcut uses a URL **fragment** (after `#`) for search queries, not a query parameter. Fragment encoding is minimal (only `#` and whitespace escaped) so the filter renders unmodified in the Search Stories box. Verified live in Chrome.
