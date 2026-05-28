# Daily standup digest

**Goal:** every weekday at 8:30 each team member gets an email + Slack DM listing the issues they're working on today — what's on them, what's blocked, what's overdue.

## Linear flavour

```yaml
- tracker: linear
  tags: morning-standup
  filterRaw:
    and:
      - cycle:
          isActive:
            eq: true
      - state:
          type:
            neq: completed
      - or:
          - hasBlockedByRelations:
              eq: false
          - dueDate:
              lte: P0D
  notify:
    subject: "Morning standup"
    followup: "Tickets in the active cycle that are on you today — focus list."
    mailTo: assignee
    columns: [Status, Priority, DueDate, Updated]
```

What this says:

- `cycle.isActive: true` — only the current sprint
- `state.type ≠ completed` — drop already-done
- The `or:` clause catches *(not blocked) OR (due today or earlier)* — so things that are due RIGHT NOW even if blocked still show up
- `mailTo: assignee` fans out one digest per assignee

We use `filterRaw` instead of `filter` (AI prompt) because the `OR` boolean is exactly the kind of logic Linear's AI prompt tends to flip. See [Linear → Filter modes](../trackers/linear.md#3-filter-modes-pick-one).

## Jira flavour

```yaml
- tracker: jira
  tags: morning-standup
  filter: "assignee in (currentUser()) AND resolution is EMPTY AND (priority in (Highest, High) OR duedate <= now())"
  notify:
    subject: "Morning standup"
    mailTo: assignee
    columns: [Status, Priority, DueDate]
```

The shape is the same — assignee fan-out, focus on high-priority or due-now items. `currentUser()` in JQL works fine here because the per-rule routing layer adds the marker on top.

## Schedule

```cron
30 8 * * 1-5  /usr/bin/dotnet /opt/preesta/Preesta.dll morning-standup
```

## Tuning

- **Too quiet?** Drop the priority filter — see everything assigned, not just high-priority. People glance at the email; volume is rarely a problem at the per-individual level.
- **Too noisy?** Add `updatedDate >= -7d` to drop items that haven't moved in a week (those go to the [overdue digest](overdue-tickets.md) instead).
- **Want Slack DMs too?** Add `slackUsers:` map (email → Slack ID) at the top of `rules.yaml`. The same `mailTo: assignee` automatically fans out to Slack as well. See [Routing model](../concepts/routing-model.md).
