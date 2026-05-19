# Preesta

**Rule-based digests for your issue trackers.**

Preesta is a small CLI tool that reads rules from a YAML file, queries one or more issue trackers (Jira, Linear, GitHub, GitLab, Shortcut), groups the matched issues by recipient, and ships each recipient a digest by email, Telegram, and Slack. It can also run write-side actions (comments, status changes, label flips) against the same matches.

It exists because every issue tracker has its own notification preferences screen, every team has its own "what's stale, what's blocking, what's on me today" question, and none of those preference screens lets you answer *yours*. With Preesta you write the question once in a rule, schedule it with cron, and the digest lands in the inbox of the people who actually need to see it.

## Who is this for?

- Engineering managers who want a daily snapshot of blockers/stale tickets/overdue items across the team
- Teams running multiple trackers (Jira for releases, Linear for product, GitHub for code, …)
- Solo engineers tired of email noise from per-tracker notification settings
- Anyone who wants automated comments / status changes on tickets that match a written-down policy

## How it works (one sentence)

A `rules.yaml` file lists rules; each rule says *which tracker, which issues, who to notify, what to do.* Preesta runs once per cron tick, fetches the matches, groups them by recipient (e.g. one digest per `assignee`), and sends.

## What you read next

- **[Quickstart](quickstart.md)** — zero to first digest in 10 minutes.
- **[Concepts](concepts/architecture.md)** — the mental model in three pages.
- **[Trackers](trackers/index.md)** — per-tracker setup walkthroughs.
- **[Cookbook](cookbook/index.md)** — realistic rules you can copy.

## Supported trackers

| Tracker | Read (issues) | Write (mutations) |
|---|---|---|
| Jira (Server & Cloud) | JQL search | REST `callRest` |
| Linear | GraphQL `issues(filter:)`, AI prompt, saved views | GraphQL mutations |
| GitHub | GraphQL `search(type: ISSUE)` — issues + PRs | GraphQL mutations |
| GitLab | GraphQL `Query.issues` chip filter | GraphQL mutations |
| Shortcut | REST `/search/stories` | REST mutations |

(Plane was [evaluated and removed](concepts/architecture.md#why-not-plane) — their public API doesn't support server-side filtering.)

## Delivery channels

Each digest is sent on every channel that has been configured: HTML email (when `Smtp:` is set), Telegram DM (when `Telegram:botToken` is set), Slack DM (when `Slack:botToken` is set). All three are independent and equal — you can run any combination, including Slack-only or Telegram-only. At least one must be set; otherwise the rules would match issues but have nowhere to deliver.
