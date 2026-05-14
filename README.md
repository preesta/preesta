# Preesta

[![CI](https://github.com/ValentinLevitov/preesta/actions/workflows/ci.yml/badge.svg)](https://github.com/ValentinLevitov/preesta/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)

> Pre-established rules for your issue tracker

Preesta helps monitor basic tracking and reporting rules for your team in JIRA. For example —

* items assigned to people in a sprint — but still not estimated
* a blocker bug was open a while ago — but still not assigned to a team member
* a task is "In Progress" for a team member — but with no updates for 2+ weeks
* and so on...

Preesta can also perform simple actions on JIRA issues on its own, for example
* if the issue is assigned by unauthorized person, reassign it (or assign depending on day of week)
* set automatically DueDate issue field depending on time of a day
* change issue status depending on linked issue statuses
* and so forth...

Preesta is specifically designed to run in containerized environments.

## Origin

Preesta started in 2019 as "Bender". In 2026 it was rebuilt on .NET 8 and renamed.

## Quick start
Let's say you want to notify your colleagues about their overdue tasks.
First of all, ensure the following JQL works for you (go to your JIRA and check)

    DueDate < startOfDay() AND Resolution is EMPTY

Of course, you can add a restriction on the JIRA project, issue type, and so on.
If it works and returns non empty list, well, let's move on. We need a rules file — place it in some directory

```yaml
# /home/user/preesta-config/rules.yaml
rules:
  - type: jql
    group: notify-all
    jql: "DueDate < startOfDay() AND Resolution is EMPTY"
    notify:
      subject: DueDate of the issue is expired
      mailTo: assignee
      cc: reporter
      recommendations: Please resolve or reschedule the issue
```

Adjust your JQL and let's move on.
Next step — we should point to JIRA instance, email server to use for mailing, let's prepare appsettings.yaml file

```yaml
# /home/user/preesta-config/appsettings.yaml
Application:
  rulesFileName: /app/rules.yaml
  supervisors: enter-your@email.here

Jira:
  rootUri: https://your-jira.server.com/
  userName: jira-user-name
  password: jira-user-password
  maxResults: 300

Smtp:
  Host: smtp.server.com
  Port: 587
  User: smtp-user-name
  Password: smtp-user-password
  From: smtp-from-address
  EnableSsl: true
```

Now we are ready to run the tool using docker or podman

```bash
$ docker run \
    -v /home/user/preesta-config/rules.yaml:/app/rules.yaml:z \
    -v /home/user/preesta-config/appsettings.yaml:/app/appsettings.yaml:z \
    -it ghcr.io/preesta/preesta \
    preesta notify-all
```

If things go well, you will see email `DueDate of the issue is expired` with the list of expired issues.
The same email is sent to all assignees of the expired issues (each addressee gets only the issues where she is assignee or reporter).

Good, but it is not very convenient to run tasks manually, so let things happen on their own by a schedule. We should create one more file describing our schedule

```sh
# /home/user/preesta-config/crontab file
0 9-20 * * MON-FRI preesta notify-all
```
This schedule instructs Preesta to start all rules in group="notify-all" every hour from 9:00 to 20:00 by working days, from Monday to Friday. As a scheduler engine Preesta uses [supercronic](https://github.com/aptible/supercronic) tool.
Let's start Preesta as a daemon with the scheduled job inside
```bash
$ docker run \
    -v /home/user/preesta-config/rules.yaml:/app/rules.yaml:z \
    -v /home/user/preesta-config/appsettings.yaml:/app/appsettings.yaml:z \
    -v /home/user/preesta-config/crontab:/app/crontab:z \
    -it ghcr.io/preesta/preesta \
    supercronic -passthrough-logs /app/crontab
```
From that moment Preesta works automatically by schedule until the docker process is stopped.

So far so good. Now let's suppose we want to enhance a bit some workflow process. We guess that issues with type "Support" should be assigned by authorized persons included in team named "Support-Administrators", all other personnel should not assign issues. Add new rule in your rules.yaml file

```yaml
rules:
  - type: jql
    group: notify-all
    jql: "DueDate < startOfDay() AND Resolution is EMPTY"
    notify:
      subject: DueDate of the issue is expired
      mailTo: assignee
      cc: reporter
      recommendations: Please resolve or reschedule the issue

  - type: jql
    group: auto-processing
    jql: >
      Type = "Support"
      AND Assignee is Not Empty
      AND (Not Assignee Changed by membersOf("Support-Administrators"))
      AND Resolution is Empty
    mutations:
      - verb: PUT
        urlPattern: "{{@jiraRoot}}rest/api/2/issue/{{@issueKey}}"
        body: |
          {
            "update": {
              "assignee": [{"set": {"name": null}}],
              "comment": [{"add": {"body": "Dropping Assignee. Only members of Support-Administrators team may assign Support issues"}}]
            }
          }
```
The rule uses `mutations` action. When called the rule is translated to REST call to the url pointed in `urlPattern`. Supported verbs are PUT and POST. Placeholder `{{@jiraRoot}}` points to property `Jira.rootUri` from appsettings. Placeholder `{{@issueKey}}` points to Issue key found for the specified JQL expression.

Add new schedule to the crontab file to start this action automatically lets say every 10 minutes
```sh
# /home/user/preesta-config/crontab file
0 9-20 * * MON-FRI preesta notify-all
*/10 9-20 * * MON-FRI preesta auto-processing
```
Then stop and start docker process again
```bash
$ docker run \
    ...
    supercronic -passthrough-logs /app/crontab
```

## Configuration formats

Preesta uses YAML as the primary configuration format. Legacy XML format is also supported — the parser is selected automatically by file extension (`.yaml`/`.yml` → YAML, `.xml` → XML).

| File | Primary (YAML) | Legacy (XML/JSON) |
|------|---|---|
| Rules | `rules.yaml` | `rules.xml` |
| App settings | `appsettings.yaml` | `appsettings.json` |
| Secrets | `appsettings.secrets.yaml` | `appsettings.secrets.json` |

## Rules Configuration specification
Supported rule types: `jql` (Jira JQL-based filter), `build` (Jira release/version monitoring), `linear` (Linear issue tracker via GraphQL), `github` (GitHub Issues + Pull Requests via GraphQL search), and `plane` (Plane work items via REST).
See [`Preesta/rules.yaml`](Preesta/rules.yaml) for a full example with `notify` (mailTo / cc / telegramChatId / columns / recommendations) and `mutations` actions.

### Slack notifications

Preesta sends Slack notifications as **personal direct messages** from a workspace bot — same shape as Telegram (one bot token, per-rule routing to individual users). Channels and incoming webhooks are not used.

**1. Create a Slack app + bot token**

1. https://api.slack.com/apps → *Create New App* → *From scratch* → pick your workspace.
2. *OAuth & Permissions* → *Scopes* → *Bot Token Scopes* → add:
   * `chat:write` — required, lets the bot post messages.
   * `users:read.email` — only if you want Preesta to look up Slack user IDs from email (you can also paste IDs directly into rules).
   * `im:write` — required, lets the bot open a DM channel with each user.
3. *Install to Workspace* → copy the **Bot User OAuth Token** (`xoxb-…`).

**2. Configure the token in `appsettings.secrets.yaml`**

```yaml
Slack:
  botToken: "xoxb-1234-5678-yourtoken"
```

(`appsettings.yaml` already declares the `Slack:` section as a placeholder; the real value belongs in the secrets file.)

**3. Use it in rules**

Two orthogonal mechanisms — combine as needed.

* **Per-rule explicit user IDs** — comma-separated list of Slack `Uxxx…` user IDs:

  ```yaml
  - type: jql
    group: notify-all
    jql: "DueDate < startOfDay() AND Resolution is EMPTY"
    notify:
      subject: DueDate expired
      mailTo: assignee
      slackUserId: "U01ABCDEFG,U02HIJKLMN"
  ```

* **Workspace-level email→id map** — once configured, any `assignee` / `reporter` / `creator` marker (or explicit email in `mailTo`/`cc`) that resolves to a known email gets a DM automatically:

  ```yaml
  slackUsers:
    ivanov@ex.com: U01ABCDEFG
    petrov@ex.com: U02HIJKLMN

  rules:
    - type: jql
      group: notify-all
      jql: ...
      notify:
        subject: ...
        mailTo: assignee  # → DM goes to Slack user mapped from assignee email
  ```

The same digest content is delivered as: HTML email (always, when SMTP is configured), Telegram DM (when `Telegram:botToken` is set and `telegramChatId` / `telegramUsers` resolve), Slack DM (when `Slack:botToken` is set and `slackUserId` / `slackUsers` resolve).

Slack-specific formatting: bold `*PRE-7*` issue keys with click-through `<url|key>` links, italic filter description (`_AI filter: "..."_`), `:emoji:` chips for status (`:hourglass_flowing_sand:` In Progress, `:white_check_mark:` Done) and priority (`:red_circle:` Urgent, `:large_orange_circle:` High, etc.).

### GitHub Issues + Pull Requests

A `github` rule fetches issues and pull requests via GitHub's GraphQL `search` API. Selection is one raw GitHub search string — the same syntax you see in the web UI search bar — so multi-repo, org-wide, and PR-vs-issue filters all live inside one human-readable expression.

**1. Create a Personal Access Token**

https://github.com/settings/tokens → *Generate new token (classic)* → scopes (need **both**):
* **`repo`** (or `public_repo` if you only monitor public repositories) — required to read issues/PRs and run mutations
* **`user:email`** (or `read:user`) — **required**: GitHub's GraphQL refuses to return the `user.email` field without this scope, and Preesta needs it to route digests by `assignee` / `reporter` marker through the `slackUsers:` / `telegramUsers:` (email → ID) maps. Without it the whole `search` query returns `INSUFFICIENT_SCOPES` errors.

(Fine-grained PATs also work — give them read access to the repos/orgs you want to monitor, plus the equivalent email-read permission.)

**2. Configure the token in `appsettings.secrets.yaml`**

```yaml
Github:
  token: "ghp_yourClassicTokenHere"
```

**3. Use it in rules**

```yaml
rules:
  # All open urgent issues across an org — one digest per assignee
  - type: github
    filter: "is:open is:issue org:bigcorp label:urgent"
    notify:
      subject: "Urgent GitHub issues"
      mailTo: assignee

  # Stale PRs across two specific repos
  - type: github
    filter: "is:open is:pr repo:foo/api repo:foo/web review:required updated:<2026-05-01"
    notify:
      subject: "Stale PRs waiting on review"
      mailTo: reporter   # i.e. PR author
```

Filter strings are deliberately impersonal — `assignee:@me` or `author:@me` belong in personal saved filters, **not** in shared rules. Per-recipient routing happens at the notification step via the `assignee` / `reporter` markers in `mailTo`, combined with the workspace-level `slackUsers:` / `telegramUsers:` (email→ID) maps. One rule fans out into one digest per distinct assignee.

GitHub returns an empty string for users who have hidden their email; routing simply skips that recipient for those issues (the digest still goes to other assignees as usual).

### GitHub self-update via GraphQL mutations (advanced)

A `github` rule can carry a `mutations:` list of raw GraphQL mutations, executed against `https://api.github.com/graphql` for each matched issue. Same shape as Linear — write the full `mutation { ... }` body and use `{{@issueId}}` (GitHub node ID), `{{@assignee.email}}`, etc. for substitution.

```yaml
- type: github
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

Per-mutation failures are logged and skipped, identical to the Linear path. Node IDs for labels/users/projects are not resolved by Preesta — query them once via GraphQL and paste them into the mutation body.

### Plane

A `plane` rule fetches work items from [Plane](https://plane.so) via its REST API. Plane's list endpoint is project-scoped (there is no org-wide search), so every rule names a single project via `projectId`. Selection inside the project happens through a small `filter:` mapping whose keys are Plane's documented list-issues query params verbatim (`state`, `priority`, `search`, …) — no DSL invented, no JSON blobs in `rules.yaml`.

**1. Get an API key**

Plane web app → *Profile* → *Personal Access Tokens* → *Create token*. The token format is `plane_api_<random>`. Sent as the custom `X-API-Key` header (not `Authorization: Bearer`).

**2. Configure in `appsettings.secrets.yaml`**

```yaml
Plane:
  apiKey: "plane_api_yourTokenHere"
  workspaceSlug: "your-workspace"   # the segment in app.plane.so/<slug>/...
  apiBase: ""                       # leave blank for Plane Cloud; self-hosted: https://plane.example.com/
```

**3. Use it in rules**

```yaml
rules:
  # Open urgent items in one project — one digest per assignee
  - type: plane
    projectId: "550e8400-e29b-41d4-a716-446655440000"
    filter:
      priority: "urgent,high"
    notify:
      subject: "Urgent Plane items"
      mailTo: assignee

  # Free-text search across a project
  - type: plane
    projectId: "550e8400-e29b-41d4-a716-446655440000"
    filter:
      search: "memory leak"
    notify:
      subject: "Memory-leak triage queue"
      mailTo: assignee
```

Omit `filter:` entirely (or leave it empty) to digest every work item in the project — handy for small projects where you want full visibility.

Filter mappings are deliberately impersonal — there is no "me" marker. Per-recipient routing happens at the notification step via the `assignee` / `reporter` markers in `mailTo`, combined with the workspace-level `slackUsers:` / `telegramUsers:` (email→ID) maps. Preesta resolves Plane's UUID-based assignees against the workspace members list (`GET /workspaces/{slug}/members/`) once at startup and caches the UUID → email map for the run.

If the members lookup fails (HTTP error, restricted token, or self-hosted Plane that doesn't expose the endpoint), assignee email routing degrades gracefully — the digest still goes out via any direct addresses or `slackUsers:` map entries the rule set, just without per-assignee fan-out.

### Plane self-update via REST mutations (advanced)

A `plane` rule can carry a `mutations:` list of raw REST requests, executed for each matched work item. Same shape as a Jira `mutations:` entry — verb / urlPattern / body — and the same marker substitution: `{{@issueId}}` resolves to the Plane work-item UUID, `{{@assignee.email}}` etc. work as usual.

```yaml
- type: plane
  projectId: "550e8400-e29b-41d4-a716-446655440000"
  filter:
    priority: "low"
  mutations:
    # Bump priority on all "low" items to "medium"
    - verb: PATCH
      urlPattern: "https://api.plane.so/api/v1/workspaces/your-workspace/projects/550e8400-e29b-41d4-a716-446655440000/work-items/{{@issueId}}"
      body: |
        {"priority": "medium"}
    # Add a comment
    - verb: POST
      urlPattern: "https://api.plane.so/api/v1/workspaces/your-workspace/projects/550e8400-e29b-41d4-a716-446655440000/work-items/{{@issueId}}/comments/"
      body: |
        {"comment_html": "<p>Reprioritised automatically.</p>"}
```

Write the absolute URL — the Plane mutation path doesn't substitute a `{{@root}}` marker (there's no equivalent of Jira's `{{@jiraRoot}}` in this surface). Per-mutation failures are logged and skipped, identical to the Linear / GitHub mutation path. State / label / user IDs are not resolved by Preesta — query them once via Plane's list endpoints (`/states/`, `/labels/`, `/workspaces/{slug}/members/`) and paste them into the body.

### Linear self-update via GraphQL mutations (advanced)

Power-user hook: a `linear` rule can carry a `mutations:` list of raw GraphQL mutation strings, executed against `https://api.linear.app/graphql` for each matched issue. No DSL — write the full `mutation { ... }` body, place markers (`{{@issueId}}`, `{{@assignee.email}}`, etc.) where Preesta should substitute issue context.

```yaml
- type: linear
  filter: "issues in 'Done' with no assignee"
  mutations:
    - mutation: |
        mutation {
          commentCreate(input: {
            issueId: "{{@issueId}}",
            body: "Auto-closed without owner — please add resolution notes"
          }) { success }
        }
    - mutation: |
        mutation {
          issueUpdate(id: "{{@issueId}}", input: { assigneeId: null }) { success }
        }
```

Per-mutation failures (HTTP error or GraphQL `errors` envelope) are logged and skipped — one bad mutation does not stop the others. State / label / user IDs are not resolved by Preesta — bring your own (Linear's GraphQL schema offers `team.states`, `team.labels`, `users(filter:)` queries to look them up).

### Code injection in rule body
*TODO: C# code may be used and placed inside block `<<c#( your-code-here )#>>` in rule bodies.*

## Logging specification
*TODO: [Serilog](https://github.com/serilog) library is used for the logging, specific configuration should be placed at `Logger` section of the appsettings file.*

## Run under Kubernetes, OKD, OpenShift
Use a Helm chart to deploy under popular container orchestrators.
