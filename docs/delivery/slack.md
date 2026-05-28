# Slack

Slack delivery is **personal direct messages** from a workspace bot. Channels and incoming webhooks are not used — the per-rule routing model wants one DM per recipient, which webhooks can't do.

![Example Slack digest](../assets/screenshots/slack-single-tracker.png)

## 1. Create the Slack app

1. [api.slack.com/apps](https://api.slack.com/apps) → *Create New App* → *From scratch* → pick your workspace.
2. **OAuth & Permissions → Scopes → Bot Token Scopes**, add:
   - `chat:write` — required, lets the bot post messages
   - `im:write` — required, lets the bot open a DM channel with each user
   - `users:read.email` — optional, only if you want Preesta to map emails to Slack user IDs automatically (otherwise you paste IDs into `slackUsers:` yourself)
3. **Install to Workspace** → copy the **Bot User OAuth Token** (`xoxb-…`).

## 2. Configure

Add to [`appsettings.secrets.yaml`](../operations/secrets-and-tokens.md) (gitignored, sits next to `appsettings.yaml` in the Preesta install — alongside any `Smtp:` / `Telegram:` sections):

```yaml
Slack:
  botToken: "xoxb-EXAMPLE-EXAMPLE-replaceme"
```

## 3. Use in rules

Two orthogonal mechanisms — combine as needed.

**Per-rule explicit user IDs** (always-on, one-for-all):

```yaml
- tracker: jira
  filter: "DueDate < startOfDay() AND Resolution is EMPTY"
  notify:
    subject: "Due-date expired"
    mailTo: assignee
    slackUserId: "U01ABCDEFG,U02HIJKLMN"
```

Every fire DMs both user IDs the same digest.

**Workspace-level email→SlackId map** (per-recipient fan-out — recommended):

```yaml
# rules.yaml — alongside the rules: list
slackUsers:
  alice@example.com: U01ABCDEFG
  bob@example.com:   U02HIJKLMN

rules:
  - tracker: jira
    filter: "..."
    notify:
      mailTo: assignee   # marker → email → Slack ID via the map above
```

See [Routing model](../concepts/routing-model.md) for the dispatch path.

## Finding Slack user IDs

In the Slack desktop app: click a user's name → *...* → *Copy member ID*. Format: `U` + 8-10 chars. The bot doesn't need to be added to a channel to DM a user — `im:write` covers it.

## Message format

Slack mrkdwn (note: **not** standard Markdown):

- `*bold*` — single asterisks, not `**bold**`
- `_italic_`
- `<https://example.com|label>` — links with `<url|label>` syntax
- `` `code` `` — backticks
- `:emoji_name:` — emoji shortcodes (Slack auto-renders)

Preesta renders status (`:hourglass_flowing_sand:` In Progress, `:white_check_mark:` Done, …), priority (`:red_circle:` Urgent, `:large_orange_circle:` High, …), and other meta chips in mrkdwn natively.

The format is built inline (`StringBuilder` in `IssueFormatter.ToSlackMrkdwn`), not via a Scriban template — Slack-specific enough to be worth keeping close to the code.

## Failure handling

Slack's `chat.postMessage` returns HTTP 200 with `{ok: false, error: "..."}` for application-level failures. Preesta treats those as failures: logs at `Error` with the Slack error code, swallows. One bad user ID never aborts the digest run. Common errors:

- `channel_not_found` — invalid user ID (typo or user is in a different workspace)
- `not_in_channel` — user has disabled DMs from the app
- `account_inactive` — workspace token revoked
- `ratelimited` — too many messages too fast (rare in normal digest cadence)

HTTP-level failures (timeouts, DNS errors) and JSON parse errors are logged + swallowed the same way.

## Why personal DMs, not a `#channel`

Two reasons:

1. **Per-recipient routing matters.** A `#bugs` channel post can only contain one global view, but a rule with `mailTo: assignee` gives each assignee their slice. Channels lose that.
2. **No incoming webhooks.** Webhooks tie to a specific channel and don't accept user-targeting. Bot tokens do.

If you really want a channel digest, configure the bot to post into the channel (`chat:write.public` scope, channel ID in `slackUserId:` — Slack accepts channel IDs in `chat.postMessage`'s `channel` field interchangeably with user IDs). It works but you lose the per-recipient story.
