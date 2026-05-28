# Per-team routing

**Goal:** different teams have different rules and different escalation paths — the platform team's "urgent" digest goes to platform leads with a Slack DM; the mobile team's overdue digest goes to the mobile manager via email only.

## The pattern

One rule per team. The rule's filter scopes to the team (label / component / project), the routing scopes to the team's recipients.

```yaml
# rules.yaml — workspace-level maps map every email to its Slack/Telegram ID
slackUsers:
  platform-lead@example.com:  U0PLATFORM
  mobile-lead@example.com:    U0MOBILE
  alice@example.com:          U0ALICE
  bob@example.com:            U0BOB

rules:
  # Platform team: urgent issues, fan out to assignees, copy lead, Slack on
  - tracker: jira
    group: team-alerts
    filter: "labels = 'team-platform' AND priority in (Highest, High) AND resolution is EMPTY"
    notify:
      subject: "Urgent items on platform team"
      mailTo: "assignee, platform-lead@example.com"
      columns: [Status, Priority, Updated]

  # Mobile team: same shape, different recipients
  - tracker: jira
    group: team-alerts
    filter: "labels = 'team-mobile' AND priority in (Highest, High) AND resolution is EMPTY"
    notify:
      subject: "Urgent items on mobile team"
      mailTo: "assignee, mobile-lead@example.com"
      columns: [Status, Priority, Updated]
```

Every assignee on platform gets an email + a Slack DM (because `slackUsers:` maps their email to a Slack ID). The platform lead gets a copy of every email and a Slack DM. Same for mobile.

## Why one rule per team

You *could* write a single rule with `jql: "labels in ('team-platform', 'team-mobile')"` and route to all the leads. It works, but the subject line becomes generic, and the leads now read each other's alerts. Two rules is clearer.

## Slack on, email off

For a team that lives in Slack and never reads email, just omit `mailTo:`:

```yaml
notify:
  subject: "Urgent on mobile"
  slackUserId: "U0MOBILE,U0MOBILE_DEPUTY"
  # no mailTo — Slack-only delivery
```

Note the `slackUserId:` literal: same digest to both IDs, every fire. The fan-out via `slackUsers:` map only works on the email markers in `mailTo`.

## Per-team mutations

Stricter teams can layer auto-mutations on top of their per-team rules:

```yaml
  - tracker: jira
    group: team-alerts
    filter: "labels = 'team-platform' AND status changed BEFORE -7d AND resolution is EMPTY"
    notify:
      subject: "Platform — stale items auto-bumped"
      mailTo: "assignee, platform-lead@example.com"
    mutations:
      - verb: POST
        urlPattern: "{{@jiraRoot}}/rest/api/2/issue/{{@issueKey}}/comment"
        body: |
          {"body": "Auto-bump from Preesta — quiet 7+ days. Update or close."}
```

The platform team gets auto-bumped at 7 days, mobile doesn't. Same structure, different policy per team.

## The non-obvious: rule order is not important

Rules within a group run independently. Two rules tagged for the same team with overlapping filters produce two separate digests (different subjects) — they don't dedupe issues across rules. If "platform urgent" and "platform overdue" both catch the same issue, the assignee gets two emails. Make filters narrow if dedup matters.
