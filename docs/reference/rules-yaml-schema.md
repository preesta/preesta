# `rules.yaml` schema

The full grammar of the rules file. For prose-level explanation of each section, see [Rule anatomy](../concepts/rule-anatomy.md).

## Top-level shape

```yaml
rules:
  - type: ...           # required, picks the rule type
    group: ...          # required, schedule group (the CLI argument)
    # ... type-specific fields ...
    notify: { ... }     # optional, but typically present
    mutations: [ ... ]  # optional

# Workspace-level (used by every rule)
redirectionRules: {}          # email → email rerouting (e.g. for dev environments)
telegramUsers: {}             # email → Telegram chat ID
slackUsers: {}                # email → Slack user ID
```

## Common rule fields

Every rule type accepts:

| Field | Type | Required | Description |
|---|---|---|---|
| `type` | string | yes | One of `jql` / `build` / `linear` / `github` / `gitlab` / `shortcut` |
| `group` | string | yes | Schedule group — must match the CLI argument when the rule should fire |
| `active` | bool | no | Default `true`. Set `false` to disable a rule without deleting it |
| `additionalPredicate` | string | no | Name of a C# method in `ExtendedFilteringPredicates` to post-filter matches |
| `notify` | object | typically yes | See below |
| `mutations` | array | no | See below |

## `notify` shape

```yaml
notify:
  subject: "..."                       # required if notify is present
  recommendations: "..."               # optional, one-line intro in digest
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

Two flavours depending on tracker type.

### REST (`jql`, `shortcut`)

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

The two are mutually exclusive: a `linear` / `github` / `gitlab` rule reads `mutation:` and discards any REST `verb`/`urlPattern`/`body` siblings, a `jql` / `shortcut` rule reads the REST shape and discards `mutation:`. Mixing them on one entry is silently truncated to the rule's flavour.

## Per-type fields

### `jql` (Jira)

```yaml
- type: jql
  jql: "project = INFRA AND assignee = currentUser()"
```

| Field | Type | Notes |
|---|---|---|
| `jql` | string | Raw JQL — the same expression the Jira web search bar accepts |

### `build` (Jira release monitoring)

```yaml
- type: build
  mask: "^9\\.0\\.0\\."
  remainingDays: 1
  expiredOnly: false
  projectCode: "INFRA"
```

| Field | Type | Notes |
|---|---|---|
| `mask` | regex string | Match against version names |
| `remainingDays` | int | Fire when version is < N days from release |
| `expiredOnly` | bool | If true, fire only after the release date |
| `projectCode` | string | Jira project filter |

### `linear`

Mutually-exclusive filter mode — exactly one of `filter` / `filterRaw` / `viewId`:

```yaml
- type: linear
  filter: "issues assigned to me, not completed"
# or
- type: linear
  filterRaw:
    state:
      type:
        neq: completed
# or
- type: linear
  viewId: "0e8a3b41-1234-..."
```

### `github`

```yaml
- type: github
  filter: "is:open is:issue org:bigcorp label:urgent"
```

Single `filter:` field, raw GitHub search string. Multi-repo / org / `is:issue` / `is:pr` qualifiers all inside the string.

### `gitlab`

```yaml
- type: gitlab
  filter:
    state: opened
    labelName: [urgent]
    assigneeUsernames: [alice]
```

Structured chip mapping — keys match `Query.issues` GraphQL argument names verbatim. Include a narrowing chip (`assigneeUsernames` / `authorUsername` / `milestoneTitle` / `iids`) so the query stays bounded on busy instances. See [GitLab tracker page](../trackers/gitlab.md#3-write-rules-filter-chips) for the full chip list and the breadth note.

### `shortcut`

```yaml
- type: shortcut
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

- Linear: rules with zero or 2+ of {filter, filterRaw, viewId}
- GitHub / Shortcut: rules with empty/missing `filter:` string
- Any rule: missing `notify` and no mutations (nothing for the rule to do)

GitLab is **not** in this list: filter breadth isn't statically validated (an unscoped query is fine on a small self-hosted instance, times out on gitlab.com). Over-broad GitLab queries fail at fetch time and are logged, not rejected up front — see [GitLab → scope the filter](../trackers/gitlab.md#3-write-rules-filter-chips).

If a digest you expected isn't going out, check the log first — there's almost always an `Error` line naming the rejected rule.
