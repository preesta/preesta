# Trackers

Preesta supports five issue trackers as **sources** (which issues to digest) and **mutation targets** (what to write back). Each one has its own page with the token-procurement walkthrough, the rule shape, and the per-tracker gotchas.

| Tracker | Transport | Filter syntax | Mutation transport |
|---|---|---|---|
| [Jira](jira.md) | REST (Server & Cloud) | JQL string | REST (`callRest`) |
| [Linear](linear.md) | GraphQL | AI prompt / raw filter / saved view | GraphQL mutations |
| [GitHub](github.md) | GraphQL (issues + PRs) | raw search string | GraphQL mutations |
| [GitLab](gitlab.md) | GraphQL | structured chips | GraphQL mutations |
| [Shortcut](shortcut.md) | REST | raw search string | REST mutations |

## Designing a new tracker integration

Want to add a sixth tracker? The per-tracker pattern is well-trodden; the engineering walkthrough lives in the repo's `dev-notes/contributing/adding-a-tracker.md`.
