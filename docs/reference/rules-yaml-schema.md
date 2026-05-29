# `rules.yaml` schema

The full grammar of the rules file. For prose-level explanation of each section, see [Rule anatomy](../concepts/rule-anatomy.md).

## Top-level shape

```yaml
rules:
  - tracker: ...           # required, picks the tracker
    tags: [foo, bar]       # optional, lefthook-style CLI tag selector
    # ... tracker-specific fields ...
    notify: { ... }        # optional, but typically present
    mutations: [ ... ]     # optional

# Workspace-level (used by every rule)
mailAliases: {}         # sendmail-style email-only forwarding/expansion
telegramUsers: {}       # email → Telegram chat ID
slackUsers: {}          # email → Slack user ID
```

## Common rule fields

Every rule accepts:

| Field | Type | Required | Description |
|---|---|---|---|
| `tracker` | string | yes | One of `jira` / `linear` / `github` / `gitlab` / `shortcut` |
| `tags` | string \| list | no | Lefthook-style tag selector. Accepts a scalar (`tags: morning`), a comma-separated scalar (`tags: "morning, standup"`), or a list (`tags: [morning, standup]`). A rule runs when the CLI invocation has no tag args (no filter), or when any of the rule's tags appears in the CLI args (OR-match). Untagged rules are skipped the moment any tag is requested. |
| `active` | bool | no | Default `true`. Set `false` to disable a rule without deleting it |
| `notify` | object | typically yes | See below |
| `mutations` | array | no | See below |

## `notify` shape

```yaml
notify:
  subject: "..."                       # required if notify is present
  followup: "..."               # optional, one-line intro in digest
  mailTo: "assignee, lead@example.com" # comma-separated; markers or literals
  cc: "..."                            # same shape as mailTo
  telegramChatId: "ID,ID"              # literal IDs only, no markers
  slackUserId: "Uxxxx,Uxxxx"           # literal IDs only, no markers
  columns: [Status, Priority, ...]     # meta chips per item
```

Recipient resolution: see [Markers](markers.md) and [Routing model](../concepts/routing-model.md).

### `columns` values

Built-in: `Project`, `Type`, `Status`, `Priority`, `Resolution`, `Assignee`, `Reporter`, `Components`, `Labels`, `Affects Versions`, `Fix Versions`, `Time Spent (hrs)`, `Due Date`, `Created`, `Updated`.

Magic: `all-non-empty` — expands to every populated standard field plus all discovered Jira custom field display names.

Custom fields (Jira only): any custom field's display name (e.g. `Severity`, `"Story point estimate"`) auto-resolves via the field map populated at startup. See [Custom fields](#custom-fields) below.

## `mutations` shape

Two flavours depending on the tracker.

### REST (`jira`, `shortcut`)

```yaml
mutations:
  - verb: POST              # HTTP verb
    urlPattern: "..."       # URL template, marker-substituted
    body: |                 # request body, marker-substituted
      {"...": "..."}
```

### GraphQL (`linear`, `github`, `gitlab`)

```yaml
mutations:
  - mutation: |
      mutation { ... }      # raw GraphQL body, marker-substituted
```

The two are mutually exclusive: a `linear` / `github` / `gitlab` rule reads `mutation:` and discards any REST `verb`/`urlPattern`/`body` siblings, a `jira` / `shortcut` rule reads the REST shape and discards `mutation:`. Mixing them on one entry is silently truncated to the rule's flavour.

## Per-tracker fields

### `jira`

```yaml
- tracker: jira
  filter: "project = INFRA AND assignee = currentUser()"
```

| Field | Type | Notes |
|---|---|---|
| `filter` | string | Raw JQL — the same expression the Jira web search bar accepts |

### `linear`

Mutually-exclusive filter mode — exactly one of `filter` / `filterRaw` / `viewId`:

```yaml
- tracker: linear
  filter: "issues assigned to me, not completed"
# or
- tracker: linear
  filterRaw:
    state:
      type:
        neq: completed
# or
- tracker: linear
  viewId: "0e8a3b41-1234-..."
```

### `github`

```yaml
- tracker: github
  filter: "is:open is:issue org:bigcorp label:urgent"
```

Single `filter:` field, raw GitHub search string. Multi-repo / org / `is:issue` / `is:pr` qualifiers all inside the string.

### `gitlab`

```yaml
- tracker: gitlab
  filter:
    state: opened
    labelName: [urgent]
    assigneeUsernames: [alice]
```

Structured chip mapping — keys match `Query.issues` GraphQL argument names verbatim. Include a narrowing chip (`assigneeUsernames` / `authorUsername` / `milestoneTitle` / `iids`) so the query stays bounded on busy instances. See [GitLab tracker page](../trackers/gitlab.md#3-write-rules-filter-chips) for the full chip list and the breadth note.

### `shortcut`

```yaml
- tracker: shortcut
  filter: "state:\"In Progress\" type:bug !is:archived"
```

Single `filter:` field, raw Shortcut search-operator syntax.

## Custom fields

Jira only. At startup Preesta calls `GET /rest/api/?/field` and builds a case-insensitive `display name → customfield_NNNNN` map. Reference a custom field in `columns:` by its display name:

```yaml
notify:
  columns: [Status, Priority, "Story point estimate", Severity]
```

Render shapes (auto-detected per value):

| Shape | Render |
|---|---|
| scalar (string / number) | as-is |
| `JArray<string>` | comma-joined |
| `JArray<JObject>` with `name`/`value`/`displayName` keys | extract that field per element, comma-joined (multi-select fields) |
| single-select `JObject` with `value` or `name` | extract that field |
| anything else | compact JSON |

Empty / missing values render as nothing — no crash.

## Validation behaviour

Malformed rules are dropped with `ILogger.Error` and Preesta keeps going for the rest of the file:

- Linear: rules with zero or 2+ of {`filter`, `filterRaw`, `viewId`}
- GitHub / Shortcut: rules with empty/missing `filter:` string
- Any rule: missing `notify` and no mutations (nothing for the rule to do)

GitLab is **not** in this list: filter breadth isn't statically validated (an unscoped query is fine on a small self-hosted instance, times out on gitlab.com). Over-broad GitLab queries fail at fetch time and are logged, not rejected up front — see [GitLab → scope the filter](../trackers/gitlab.md#3-write-rules-filter-chips).

If a digest you expected isn't going out, check the log first — there's almost always an `Error` line naming the rejected rule.
