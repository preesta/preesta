# Overdue tickets

**Goal:** weekly summary of issues past their due date — one email per assignee + a copy to the team lead.

## Jira flavour

```yaml
- type: jql
  group: weekly-overdue
  jql: "duedate < now() AND resolution is EMPTY ORDER BY duedate ASC"
  notify:
    subject: "Overdue tickets"
    recommendations: "These are past their due date and still open. Triage urgency or move the date."
    mailTo: "assignee, team-lead@example.com"   # lead always gets a copy
    columns: [Status, Priority, DueDate, Updated]
```

`"assignee, team-lead@example.com"` mixes a marker with a literal — see [Markers reference](../reference/markers.md). The lead gets every assignee's digest (CC fan-out); each individual still gets their own slice.

## Linear flavour

```yaml
- type: linear
  group: weekly-overdue
  filterRaw:
    and:
      - state:
          type:
            neq: completed
      - dueDate:
          lt: P0D
  notify:
    subject: "Overdue tickets"
    mailTo: "assignee, team-lead@example.com"
    columns: [Status, Priority, DueDate]
```

`dueDate.lt: P0D` is Linear's "before today" predicate (ISO-8601 duration relative).

## Shortcut flavour

```yaml
- type: shortcut
  group: weekly-overdue
  filter: "!state:completed !is:archived has:deadline deadline:<today"
  notify:
    subject: "Overdue stories"
    mailTo: "assignee, team-lead@example.com"
    columns: [Type, Status, Updated]
```

## Schedule

```cron
0 10 * * 1  /usr/bin/dotnet /opt/preesta/Preesta.dll weekly-overdue
```

Monday 10am is a good slot — fresh week, before standups, after coffee.

## Tuning

- **Want a daily ping until it's resolved?** Move the schedule to `0 10 * * 1-5`. Less polite but more effective.
- **Want the lead to see *only* the lead-relevant items, not everyone's overdue?** Drop the literal email from `mailTo` and write a second rule with `mailTo: team-lead@example.com` and a narrower JQL (`labels = "urgent"`, `priority in (Highest)`).
