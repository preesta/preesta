# Multi-tracker digest

**Goal:** one schedule group fires rules across Jira + Linear + GitHub, each producing one section in a single per-assignee digest. The result: a recipient who's active in all three trackers gets one email with three sections, each headed by an "Open in &lt;tracker&gt; →" link.

![Three trackers, three sections, one recipient](../assets/screenshots/email-multi-tracker.png)

## How the merge happens

Packages are grouped by `(To, Cc, Subject)`. Use the same `subject:` across the three rules and a recipient with work in all three gets one email with three sections; use different subjects and they fan out into separate emails. Per-tracker "Open in &lt;tracker&gt; →" headers and tracker-specific chips keep each section identifiable inside the merged digest.

The Telegram and Slack DMs follow the same merge — the bot posts one combined message with the same `———` separator between sections:

![Same merged digest delivered via Telegram](../assets/screenshots/tg-multi-tracker.png)

![Same merged digest delivered via Slack](../assets/screenshots/slack-multi-tracker.png)

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
      subject: "Cross-tracker digest"
      mailTo: assignee
      columns: [Status, Priority, Updated]

  # Jira: assigned tickets with no resolution
  - type: jql
    group: morning-roundup
    jql: "assignee in (membersOf('engineering')) AND resolution is EMPTY"
    notify:
      subject: "Cross-tracker digest"
      mailTo: assignee
      columns: [Status, Priority, DueDate]

  # GitHub: open issues and PRs across the org
  - type: github
    group: morning-roundup
    filter: "is:open org:bigcorp"
    notify:
      subject: "Cross-tracker digest"
      mailTo: assignee
      columns: [Type, Status, Updated]
```

## Schedule

```cron
30 8 * * 1-5  /usr/bin/dotnet /opt/preesta/Preesta.dll morning-roundup
```

One CLI invocation, three tracker fetches in parallel (each pipeline runs as its own `Task`), one batch of merged per-assignee emails sent in one SMTP session.

## Verifying

Run with `Verbose` logging once to see all three pipelines firing:

```
INFO 4 rules of type linear found in schedule group 'morning-roundup'
INFO 2 rules of type jql found in schedule group 'morning-roundup'
INFO 1 rules of type github found in schedule group 'morning-roundup'
INFO Sent 6 email messages
```

The recipient list per email comes from the rule's own `mailTo` — alice can be Linear-only, bob can be Jira+GitHub. With a shared subject, packages bound for the same recipient merge into one email; with distinct subjects, they stay separate.

## Adding GitLab and Shortcut

Same pattern. Each tracker gets its own rule entry under `morning-roundup`. The schedule group is just a CLI selector — there's no upper bound on rules per group.

```yaml
  - type: gitlab
    group: morning-roundup
    filter: { state: opened, assigneeUsernames: [...] }
    notify: { subject: "Cross-tracker digest", mailTo: assignee }

  - type: shortcut
    group: morning-roundup
    filter: "!state:completed !is:archived owner:..."
    notify: { subject: "Cross-tracker digest", mailTo: assignee }
```
