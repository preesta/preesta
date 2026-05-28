# Rule anatomy

Every rule has four sections. The shape changes a little per tracker, but the four roles are the same.

```yaml
- tracker: github            # ① WHICH TRACKER
  group: morning          #    schedule group (CLI argument)

  filter: "..."           # ② WHICH ISSUES
                          #    shape depends on tracker — see below

  notify:                 # ③ WHO TO TELL & WHAT THEY GET
    subject: "..."
    followup: "..."
    mailTo: assignee
    cc: ""
    telegramChatId: "..."
    slackUserId: "..."
    columns: [Status, Priority, Updated]

  mutations:              # ④ OPTIONAL: WHAT TO DO TO MATCHED ISSUES
    - mutation: |         #    GraphQL bodies for Linear/GitHub/GitLab
        mutation { ... }
    - verb: POST          #    or REST verb/url/body for Jira/Shortcut
      urlPattern: "..."
      body: "..."
```

## ① `tracker` and `group`

`tracker:` picks the source — one of `jira` / `linear` / `github` / `gitlab` / `shortcut`.

`group:` is purely organizational. Rules with the same group run together when you invoke `preesta <group>`. Use it to cluster rules by schedule (`morning`, `nightly`, `weekly`), by team, by anything — it has no semantic meaning beyond "ran in the same CLI invocation".

## ② `filter` — which issues

The shape is per-tracker because each tracker's native query language is different and we don't invent a DSL. **Use what the tracker's own web UI search bar takes.**

| Tracker | `filter` shape | Example |
|---|---|---|
| `jira` | JQL string | `"project = INFRA AND status = 'In Progress'"` |
| `linear` | one of three modes (mutually exclusive) | `filter: "issues assigned to me"` (AI prompt), `filterRaw: {...}` (raw GraphQL), `viewId: "..."` (saved view) |
| `github` | raw GitHub search string | `"is:open is:issue org:bigcorp label:urgent"` |
| `gitlab` | structured chip mapping | `{ state: opened, labelName: [urgent], assigneeUsernames: [alice] }` |
| `shortcut` | raw Shortcut search string | `"state:\"In Progress\" type:bug !is:archived"` |

The reasoning behind each choice is in the per-tracker page; the short version is "whatever the user already types into the web UI search bar". GitLab is the odd one out because GitLab has no single-string search for issues — its UI builds queries from chips, so Preesta accepts the chip names directly.

**No identity in filters.** `assignee:@me`, `author:@me`, etc. resolve to the API token's owner, which is almost never the right person to notify. Filter on shared attributes (label, team, state, milestone), let the [routing layer](routing-model.md) fan out per-recipient.

## ③ `notify` — who gets the digest

```yaml
notify:
  subject: "Urgent items on you"        # email subject + digest header
  followup: "Triage by EOD."     # one-line intro shown in the digest
  mailTo: assignee                      # primary recipients
  cc: ""                                # carbon copy
  telegramChatId: "12345678"           # literal Telegram chat ID (one-for-all)
  slackUserId: "U0ABC123"               # literal Slack user ID (one-for-all)
  columns: [Status, Priority, Updated]  # which metadata to render per item
```

`mailTo` and `cc` accept comma-separated values; each value is either a literal email address or a [marker](obezlichennye-rules.md#markers) (`assignee` / `reporter` / `creator`). The marker resolves once per issue, the grouping happens on the resolved value, and each recipient gets exactly their slice.

`telegramChatId` and `slackUserId` are **literal IDs** — they don't resolve. The same digest goes to every listed ID, every time the rule fires. For per-recipient Telegram/Slack fan-out, configure the workspace-level [`telegramUsers:` / `slackUsers:` email→ID maps](routing-model.md) instead and rely on the markers in `mailTo`.

`columns` controls the per-issue metadata chips. Supported values: `Status`, `Priority`, `Type`, `Resolution`, `Assignee`, `Reporter`, `Components`, `Labels`, `Affects Versions`, `Fix Versions`, `Time Spent (hrs)`, `Due Date`, `Created`, `Updated`, `Project`, plus the magic `all-non-empty` (renders every populated field), plus Jira custom field display names ([Custom Fields](../reference/rules-yaml-schema.md#custom-fields)). The header (Key + Summary) is always there; columns add meta below it.

## ④ `mutations` — write side (optional)

After dispatching notifications, Preesta walks `rule.mutations` and runs each one against every matched issue. The shape is per-tracker:

**GraphQL trackers (Linear, GitHub, GitLab)** — each entry is a `mutation:` key with a raw GraphQL body. Markers (`{{@issueId}}`, `{{@issueKey}}`, `{{@title}}`, `{{@assignee.email}}`, etc.) substitute issue context before the body goes out:

```yaml
mutations:
  - mutation: |
      mutation {
        addComment(input: {
          subjectId: "{{@issueId}}",
          body: "Stale — bumping. Please update or close."
        }) { clientMutationId }
      }
```

**REST trackers (Jira, Shortcut)** — each entry is a Jira-style `verb` / `urlPattern` / `body`:

```yaml
mutations:
  - verb: POST
    urlPattern: "https://api.app.shortcut.com/api/v3/stories/{{@issueId}}/comments"
    body: |
      { "text": "Stale — please update." }
```

Per-mutation failures (HTTP errors, GraphQL `errors` envelope) are logged at `Error` and skipped — one bad mutation never stops the others.

See **[Markers reference](../reference/markers.md)** for the full substitution list.

## Validation

The YAML converter drops malformed rules with an `ILogger.Error` log line — Preesta keeps going for the rest of the file:

- Mutually-exclusive filter modes set together (Linear)
- Empty/missing `filter:` string (GitHub, Shortcut)
- Non-string filter where a string is expected, or vice-versa

Filter *breadth* is not validated — a too-broad query (e.g. an unscoped GitLab filter on gitlab.com) isn't rejected up front; it fails at fetch time, gets logged, and the run continues.

If something is silently not happening, check the log — there's almost always an `Error` line explaining which rule got dropped and why.
