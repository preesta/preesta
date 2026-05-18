# Multi-tracker digest

**Goal:** one schedule group fires rules across Jira + Linear + GitHub, each producing its own per-assignee digest. The result: a recipient who's active in all three trackers gets three emails, each scoped to one tracker.

## Why three emails, not one combined

Preesta groups by `(To, Cc, Subject, Rule)` — different rules don't merge. This is intentional:

- Different subjects say where the items came from at a glance
- "Open in <tracker> →" links go to the right tracker
- Mutation outcomes are logged per-rule, not blended

If you want a single "everything on me" email, write a wrapper script that runs three groups and concatenates — Preesta won't merge across rules.

## The rules.yaml

```yaml
rules:
  # Linear: in-flight sprint work
  - type: linear
    group: morning-roundup
    filterRaw:
      and:
        - cycle: { isActive: { eq: true } }
        - state: { type: { neq: completed } }
    notify:
      subject: "Linear — sprint work"
      mailTo: assignee
      columns: [Status, Priority, Updated]

  # Jira: assigned tickets with no resolution
  - type: jql
    group: morning-roundup
    jql: "assignee in (membersOf('engineering')) AND resolution is EMPTY"
    notify:
      subject: "Jira — open tickets"
      mailTo: assignee
      columns: [Status, Priority, DueDate]

  # GitHub: open issues and PRs across the org
  - type: github
    group: morning-roundup
    filter: "is:open org:bigcorp"
    notify:
      subject: "GitHub — items on you"
      mailTo: assignee
      columns: [Type, Status, Updated]
```

## Schedule

```cron
30 8 * * 1-5  /usr/bin/dotnet /opt/preesta/Preesta.dll morning-roundup
```

One CLI invocation, three tracker fetches in parallel (each pipeline runs as its own `Task`), three batches of per-assignee emails sent in one SMTP session.

## Verifying

Run with `Verbose` logging once to see all three pipelines firing:

```
INFO 4 rules of type linear found in schedule group 'morning-roundup'
INFO 2 rules of type jql found in schedule group 'morning-roundup'
INFO 1 rules of type github found in schedule group 'morning-roundup'
INFO Sent 12 email messages
```

The recipient list per email comes from the rule's own `mailTo`, not from the union — alice can be Linear-only, bob can be Jira+GitHub, the digests fan out independently.

## Adding GitLab and Shortcut

Same pattern. Each tracker gets its own rule entry under `morning-roundup`. The schedule group is just a CLI selector — there's no upper bound on rules per group.

```yaml
  - type: gitlab
    group: morning-roundup
    filter: { state: opened, assigneeUsernames: [...] }
    notify: { subject: "GitLab — open issues", mailTo: assignee }

  - type: shortcut
    group: morning-roundup
    filter: "!state:completed !is:archived owner:..."
    notify: { subject: "Shortcut — open stories", mailTo: assignee }
```
