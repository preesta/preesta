# Impersonal rules

This is the single most important idea in Preesta. Everything else follows from it.

## The principle

**A rule says *which issues*, never *who they're for.*** Identity lives in a separate layer.

The wrong way (which is how most tracker notification systems work):

```yaml
# Don't do this
- type: github
  filter: "is:open is:issue assignee:alice"
  notify:
    mailTo: alice@example.com
```

That rule has Alice's name in two places. To produce the same digest for Bob you copy the rule and change both. For ten people, ten copies. When Alice leaves you delete one rule out of ten. The rule grows the team.

The Preesta way:

```yaml
- type: github
  filter: "is:open is:issue label:urgent"
  notify:
    mailTo: assignee
```

One rule. It says *"all open urgent issues assigned to anyone"* and *"send each assignee their own subset"*. Add a teammate — they automatically get their digest the moment they get assigned anything. Remove a teammate — they stop getting digests the moment they stop being assigned. The rule doesn't change.

## How the dispatch works

When Preesta processes that rule:

1. It pulls the matching issues. Say 8 issues come back, assigned to {alice, bob, alice, carol, bob, alice, bob, dave}.
2. For each issue it resolves the `assignee` marker into a concrete email by reading `issue.Participants.Assignee.Email`.
3. It groups the issues by the resolved email. Result: 4 packages — alice (3 issues), bob (3), carol (1), dave (1).
4. Each package is rendered as its own digest. Alice sees her three issues, Bob sees his three, etc.

The grouping key is `(To, Cc, Subject, Rule)`, so two different rules with the same recipient still produce two separate digests (different subjects, different items, different "Open in <tracker> →" links).

## Markers

Anywhere the YAML schema accepts a recipient list, a *marker* is the magic value that resolves at runtime:

| Marker | Resolves to |
|---|---|
| `assignee` | `issue.Participants.Assignee.Email` |
| `reporter` | `issue.Participants.Reporter.Email` |
| `creator` | `issue.Participants.Creator.Email` |

Markers and literal emails can mix:

```yaml
notify:
  mailTo: "assignee, team-lead@example.com"
```

For each issue, the literal `team-lead@example.com` is added to the digest's recipient list, plus that issue's actual assignee. The team lead gets *every* assignee's digest (CC'd in). The per-assignee fan-out still works.

## Why this matters

Three concrete payoffs:

**Onboarding is automatic.** Adding a new team member to GitHub doesn't require touching rules.yaml. As soon as they get assigned an issue matching a rule, they get the digest. As soon as they stop being assigned, they stop getting it.

**Workflow stays in the tracker.** When the team's blocker policy changes ("urgent items now also get assigned to the on-call"), you change the on-call assignment in the tracker — Preesta picks it up on the next tick. You don't co-edit rules.yaml.

**Filters are reviewable.** "Which issues does this rule catch?" is just running the filter in the tracker's web UI. You don't have to mentally subtract out per-person clauses. That's also why every "Open in <tracker> →" link in the digest header points at the same filtered view — the rule and the link are the same query.

## The non-obvious follow-on: filters are impersonal too

The mirror of "rules are impersonal" is: *don't put identity in the filter either.* Avoid filters like `is:open assignee:@me` — the `@me` resolves to whoever owns the API token, which is almost never who the digest is for. If the rule should catch "open urgent issues on the platform team", filter on `team:platform`, not on a specific person, and let the routing layer fan out.

Validators in the YAML converters enforce this where they can:

- GitLab and (now) Plane reject rules with zero user-facing filter chips — an empty filter is a "scan everything" rule and is universally a mistake
- Linear's three filter modes (`filter`, `filterRaw`, `viewId`) are mutually exclusive — the rule has exactly one source of truth for what it matches

## See also

- **[Routing model](routing-model.md)** — how email markers become Slack/Telegram messages (workspace-level `slackUsers:` / `telegramUsers:` maps)
- **[Rule anatomy](rule-anatomy.md)** — the four sections of a rule
- **[Cookbook → per-team routing](../cookbook/per-team-routing.md)** — realistic team-scoped rules
