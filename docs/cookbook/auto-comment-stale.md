# Auto-comment on stale issues

**Goal:** every Monday, find issues labeled `stale` that haven't moved in 14 days, post a polite "bumping" comment, and notify the assignee.

## GitHub flavour

```yaml
- tracker: github
  group: weekly-stale
  filter: "is:open is:issue label:stale updated:<-14d"
  notify:
    subject: "Stale issues — auto-bumped"
    followup: "These have been quiet 14+ days. Auto-comment added; please update or close."
    mailTo: assignee
  mutations:
    - mutation: |
        mutation {
          addComment(input: {
            subjectId: "{{@issueId}}",
            body: "👋 Bump from Preesta — this issue has been quiet for 2+ weeks. Update status or close if no longer relevant."
          }) { clientMutationId }
        }
```

Two things happen per matched issue: (1) the auto-comment posts via the GraphQL mutation, (2) the assignee gets an email digest listing the issues that just got commented on.

## Jira flavour

```yaml
- tracker: jira
  group: weekly-stale
  filter: "labels = stale AND resolution is EMPTY AND updated < -14d"
  notify:
    subject: "Stale Jira tickets — auto-bumped"
    mailTo: assignee
  mutations:
    - verb: POST
      urlPattern: "{{@jiraRoot}}/rest/api/2/issue/{{@issueKey}}/comment"
      body: |
        {"body": "Auto-bump from Preesta — quiet for 2+ weeks. Update or close."}
```

## Linear flavour

```yaml
- tracker: linear
  group: weekly-stale
  filterRaw:
    and:
      - labels:
          name:
            eq: "stale"
      - state:
          type:
            neq: completed
      - updatedAt:
          lt: P-14D
  notify:
    subject: "Stale Linear issues — auto-bumped"
    mailTo: assignee
  mutations:
    - mutation: |
        mutation {
          commentCreate(input: {
            issueId: "{{@issueId}}",
            body: "Auto-bump from Preesta — quiet for 2+ weeks. Update or close."
          }) { success }
        }
```

## Be polite

Three rules of thumb for write-side automation:

1. **Make it obvious.** "Auto-bump from Preesta" or "Posted by Preesta automation" — readers should immediately know it isn't a human. Don't disguise.
2. **Narrow the filter.** A 14-day staleness window catches a few items per week. A 1-day window catches everything that didn't move yesterday — too noisy, people start ignoring comments.
3. **Avoid mutation loops.** Don't trigger on `updated < -14d` and post a comment that itself bumps `updated` — that's fine here because the next 14-day cycle catches a different item, but if you ever rule on `updated < -1d` you create a runaway.

## Schedule

```cron
0 10 * * 1   /usr/bin/dotnet /opt/preesta/Preesta.dll weekly-stale
```

Monday morning, before standups — the assignee sees both the email and the in-tracker comment when they triage.
