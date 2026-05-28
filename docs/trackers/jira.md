# Jira

The original target. Works against both Jira Server and Jira Cloud.

## 1. Authentication

Two auth modes — `apiToken` (recommended, Cloud + Server 9.x) or username/password (Server only).

```yaml
Jira:
  rootUri: https://yourcompany.atlassian.net/      # or your self-hosted host
  apiToken: "ATATT3xFfGF0..."                      # preferred
  # userName: you@example.com                      # fallback for Server <9
  # password: "..."
```

API token for Cloud: [id.atlassian.com/manage-profile/security/api-tokens](https://id.atlassian.com/manage-profile/security/api-tokens). Pick "Classic" — full-access token if you want `callRest` mutations, read-only if you only want digests.

## 2. Rule shape

```yaml
- tracker: jira
  group: daily
  filter: "project = INFRA AND assignee = currentUser() AND resolution is EMPTY ORDER BY priority DESC"
  notify:
    subject: "Open INFRA tickets on you"
    mailTo: assignee
    columns: [Status, Priority, DueDate]
```

`filter:` is a raw JQL expression — the same syntax the Jira web search bar uses. The standard `currentUser()`, `now()`, `endOfDay()` etc. functions all work.

## 3. Custom fields

Preesta auto-discovers Jira custom fields at startup via `GET /rest/api/?/field`. Reference them in `columns:` by display name — no `customfield_NNNNN` ids in config:

```yaml
notify:
  columns: [Status, Priority, Severity, "Story point estimate"]
```

See [Rules YAML schema → Custom fields](../reference/rules-yaml-schema.md#custom-fields) for the value-rendering details.

## Mutations — `callRest`

Jira mutations use the REST `verb` / `urlPattern` / `body` shape:

```yaml
- tracker: jira
  filter: "..."
  mutations:
    - verb: POST
      urlPattern: "{{@jiraRoot}}/rest/api/2/issue/{{@issueKey}}/comment"
      body: |
        {"body": "Auto-comment from Preesta"}
    - verb: PUT
      urlPattern: "{{@jiraRoot}}/rest/api/2/issue/{{@issueKey}}"
      body: |
        {"fields": {"labels": ["stale"]}}
```

`{{@jiraRoot}}` resolves to the configured `Jira:rootUri`. `{{@issueKey}}` is the human key (`INFRA-123`). Per-mutation failures are logged + skipped, identical to GraphQL trackers.

## Issue mapping

Standard Jira fields map to the obvious `Issue` properties. Anything else lives in `Issue.CustomFields` (`Dictionary<string, JToken?>` keyed by `customfield_NNNNN`) and renders via the auto-discovered name map.
