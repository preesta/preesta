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

Preesta is structured so adding the sixth tracker is a well-scoped piece of work — `git log --grep "^Phase 12.5"` for the GitHub addition is a clean reference. See **[Adding a tracker](../contributing/adding-a-tracker.md)** for the step-by-step.
