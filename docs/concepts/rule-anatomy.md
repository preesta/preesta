# Rule anatomy

Every rule has four sections. The shape changes a little per tracker, but the four roles are the same.

```yaml
- tracker: github            # ① WHICH TRACKER
  tags: [morning, standup]   #    optional CLI tag filter (any string)

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

## ① `tracker` and `tags`

`tracker:` picks the source — one of `jira` / `linear` / `github` / `gitlab` / `shortcut`.

`tags:` is **optional** and uses lefthook-style positive selection:

- A rule with no `tags:` runs whenever you invoke `preesta` **without arguments**.
- A rule with `tags:` (scalar `tags: morning`, comma-string `tags: "morning, standup"`, or list `tags: [morning, standup]`) runs whenever any of its tags is in the CLI args.
- `preesta morning` runs every rule whose tags include `morning`. Untagged rules **drop out** the moment you pass a tag — that's the point of being explicit.
- Multiple CLI tags OR-match: `preesta morning release` runs anything tagged either way.

Use it for ad-hoc schedule slices (`morning`, `nightly`), team scopes (`backend`, `frontend`), or specialised runs (`q3-rollout`). The value is opaque — Preesta doesn't interpret it.

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

If a rule is malformed, Preesta logs an error for that rule and keeps processing the rest. Common cases:

- Mutually-exclusive Linear filter modes set together (`filter` + `filterRaw`, etc.)
- Empty or missing `filter:` on a GitHub or Shortcut rule
- Filter value in the wrong shape — e.g. a string where the tracker expects a chip mapping

If a digest you expected isn't going out, check the log — there's almost always one line naming the rule that got dropped and why.
