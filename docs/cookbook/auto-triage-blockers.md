# Auto-triage blockers

**Goal:** every 10 minutes, hand any unassigned blocker to the triager so it can't sit ownerless, and ping every owner about their blockers that haven't been started. Two rules form a loop that drains the unowned queue and keeps stalled work visible.

## Jira flavour

JQL has the native `status`, `priority`, and relative-time vocabulary this pattern was built around. Rule 2 only fires on real stagnation (last update older than 30 minutes), not on a blocker that was assigned ten seconds ago.

```yaml
rules:
  # 1. Unresolved blocker, no owner → assign to the triager so it can't sit ownerless.
  - type: jql
    group: blocker-watch
    jql: 'priority = Blocker AND resolution = EMPTY AND assignee is EMPTY'
    notify:
      subject: "Unassigned blocker — auto-assigned to you"
      mailTo: triager@example.com
    mutations:
      - verb: PUT
        urlPattern: "{{@jiraRoot}}rest/api/2/issue/{{@issueKey}}/assignee"
        body: '{"name": "triager-jira-username"}'

  # 2. Unresolved owned blocker that hasn't moved into In Progress within 30 minutes.
  - type: jql
    group: blocker-watch
    jql: 'priority = Blocker AND resolution = EMPTY AND status != "In Progress" AND assignee is not EMPTY AND updated < -30m'
    notify:
      subject: "Your blocker hasn't been picked up (30+ min)"
      mailTo: assignee
```

`resolution = EMPTY` is the canonical Jira clause for "this is still open work, regardless of which custom status it's in" — without it `status != "In Progress"` would happily match Closed / Resolved / Duplicate / Done and ship a digest of finished tickets.

## The loop

1. An unowned blocker matches rule 1: the `PUT /assignee` mutation hands it to the triager, and the triager gets an email noting the auto-assignment.
2. Within the next 30 minutes that same blocker matches rule 2 (it now has an assignee — the triager — and still isn't *In Progress*), so the triager sees it in their stalled-blocker digest.
3. The triager either starts it (clears it from the rule-2 set the moment they move to *In Progress*) or re-assigns to the right owner — who then picks it up in their own rule-2 digest on the next tick.

The unowned queue can't quietly grow: every cron tick converts it into the triager's stalled queue. Nothing relies on anyone manually scanning a backlog.

## Schedule

Blocker latency matters — 10 minutes is a reasonable upper bound for "how long can an unowned blocker sit?".

```cron
*/10 * * * *  preesta blocker-watch
```

## GitHub flavour

GitHub Issues have no native `status` field — only `open` / `closed`. The pattern still works if your team has a convention of applying an `in-progress` label when work starts. The unowned half uses GitHub's `addAssigneesToAssignable` GraphQL mutation:

```yaml
rules:
  # 1. Blocker exists, nobody owns it — auto-assign to the triager.
  - type: github
    group: blocker-watch
    filter: "is:open is:issue repo:your-org/your-repo label:blocker no:assignee"
    notify:
      subject: "Unassigned blocker — auto-assigned to you"
      mailTo: triager@example.com
    mutations:
      - mutation: |
          mutation {
            addAssigneesToAssignable(input: {
              assignableId: "{{@issueId}}",
              assigneeIds: ["U_kgDO_TRIAGER_NODE_ID"]
            }) { assignable { ... on Issue { number url } } }
          }

  # 2. Blocker has an owner but isn't moving yet — ping the owner.
  - type: github
    group: blocker-watch
    filter: "is:open is:issue repo:your-org/your-repo label:blocker -label:in-progress"
    notify:
      subject: "Your blocker hasn't been picked up"
      mailTo: assignee
```

> Two caveats specific to GitHub:
>
> - `-label:in-progress` is a **team convention**, not a native status. If your team doesn't apply that label, swap it for a proxy like `-linked:pr` (no PR linked yet) or drop the `-label:` term.
> - `assigneeIds` takes GitHub's GraphQL node ID (`U_kgDO…`), not a username. Get the triager's with `gh api graphql -f query='query { user(login: "triager-username") { id } }'`.

## Why two rules instead of one

A single rule with `mailTo: assignee` would silently drop unassigned matches (no recipient → nothing sent), so blockers without an owner would never surface. Splitting by `assignee is EMPTY` vs `assignee is not EMPTY` (or `no:assignee` vs `-label:in-progress` on GitHub) makes the two cases visible to two different audiences — and lets each one act with the right tool (mutation for triage, notification for ownership).
