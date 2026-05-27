# Auto-triage blockers

**Goal:** every 10 minutes, hand any unassigned blocker to the triager so it can't sit ownerless, and ping every owner about their blockers that haven't been started yet. Two rules form a loop that drains the unowned queue and keeps stalled work visible.

## GitHub flavour

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

## The loop

1. An unowned blocker matches rule 1: the mutation assigns it to the triager, and the triager gets an email noting the auto-assignment.
2. On the next tick that same blocker matches rule 2 (it now has an assignee — the triager — but still isn't `in-progress`), so the triager sees it in their stalled-blocker digest.
3. The triager either starts it (clears it from the rule-2 set the moment they add `in-progress`) or re-assigns to the right owner — who then picks it up in their own rule-2 digest on the next tick.

The unowned queue can't quietly grow: every cron tick converts it into the triager's stalled queue. Nothing relies on anyone manually scanning a backlog.

> `assigneeIds` takes GitHub's GraphQL node ID (`U_kgDO…`), not a username. Get the triager's with `gh api graphql -f query='query { user(login: "triager-username") { id } }'`.

## Schedule

Blocker latency matters — 10 minutes is a reasonable upper bound for "how long can an unowned blocker sit?".

```cron
*/10 * * * *  preesta blocker-watch
```

## Jira flavour

JQL has the relative-time vocabulary GitHub search lacks, so you can layer staleness on top of the same loop. For example, only ping owners whose blocker hasn't moved in 30 minutes:

```yaml
rules:
  # 1. Unassigned blockers → assign to the triager.
  - type: jql
    group: blocker-watch
    jql: 'priority = Blocker AND status = "Open" AND assignee is EMPTY'
    notify:
      subject: "Unassigned blocker — auto-assigned to you"
      mailTo: triager@example.com
    mutations:
      - verb: PUT
        urlPattern: "{{@jiraRoot}}rest/api/2/issue/{{@issueKey}}/assignee"
        body: '{"name": "triager-jira-username"}'

  # 2. Owned blockers that haven't moved into In Progress within 30 minutes.
  - type: jql
    group: blocker-watch
    jql: 'priority = Blocker AND status != "In Progress" AND assignee is not EMPTY AND updated < -30m'
    notify:
      subject: "Your blocker hasn't been picked up (30+ min)"
      mailTo: assignee
```

Same loop, tighter signal: rule 2 only fires on real stagnation, not on a blocker that was assigned ten seconds ago.

## Why two rules instead of one

A single rule with `mailTo: assignee` would silently drop unassigned matches (no recipient → nothing sent), so blockers without an owner would never surface. Splitting by `no:assignee` vs `-label:in-progress` (or `assignee is EMPTY` vs `assignee is not EMPTY` in JQL) makes the two cases visible to two different audiences — and lets each one act with the right tool (mutation for triage, notification for ownership).
