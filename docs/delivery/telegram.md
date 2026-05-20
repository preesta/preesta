# Telegram

Telegram delivery is **personal DMs** from a workspace bot — same shape as Slack (one bot token, per-rule routing to individual users). Group chats are supported too (a chat ID can be a group ID).

## 1. Create the bot

1. Open Telegram, find [@BotFather](https://t.me/BotFather).
2. `/newbot`, follow prompts. Name (display, mutable), username (immutable, must end with `bot`).
3. BotFather hands you a token like `1234567890:AAEexamplexamplexamplexamplexample`.

## 2. Configure

Add to [`appsettings.secrets.yaml`](../operations/secrets-and-tokens.md) (gitignored, sits next to `appsettings.yaml` in the Preesta install — alongside any `Smtp:` / `Slack:` sections):

```yaml
Telegram:
  botToken: "1234567890:AAEexamplexamplexamplexamplexample"
```

## 3. Find user/chat IDs

Each person who wants Preesta DMs has to "open the bot" first — Telegram doesn't allow bots to message users who haven't initiated contact. Have each user:

1. Click the bot's `t.me/<bot_username>` link or search the bot name in Telegram.
2. Hit *Start*.

Their numeric user ID is the chat ID. To discover it:

```bash
curl https://api.telegram.org/bot<TOKEN>/getUpdates
```

In the response, `result[].message.from.id` is the user's chat ID. (Add the bot to a group and the same call returns the group's negative-prefixed chat ID under `result[].message.chat.id`.)

## 4. Use in rules

Two orthogonal mechanisms — combine as needed.

**Per-rule explicit chat IDs** (always-on, one-for-all):

```yaml
- type: jql
  jql: "..."
  notify:
    subject: "Daily digest"
    mailTo: assignee
    telegramChatId: "12345678,987654321"
```

Every fire of this rule DMs both chat IDs the same digest.

**Workspace-level email→chatId map** (per-recipient fan-out — recommended):

```yaml
# rules.yaml — alongside the `rules:` list
telegramUsers:
  alice@example.com: "12345678"
  bob@example.com:   "987654321"

rules:
  - type: jql
    jql: "..."
    notify:
      subject: "Daily digest"
      mailTo: assignee   # ← marker resolves to email; map turns email into chat ID
```

Each distinct assignee email gets its own DM with its own slice. See [Routing model](../concepts/routing-model.md) for the full mechanics.

## Message format

Telegram-compatible HTML (`<b>`, `<i>`, `<a>`, `<code>`, `<pre>` — same set Telegram's bot API accepts). Each digest is one message; the same plain-text body that goes into the email body is reused. Truncated cleanly if a single message would exceed Telegram's 4096-char limit (rare for normal digests).

## Limits

- 4096 chars per message
- 30 messages/second per bot, 1 message/sec to the same chat — Preesta doesn't batch, so for very-bulk digests across many recipients there's a built-in rate window in `TelegramMessenger` (low overhead in normal use).
- Bots can't initiate conversations with users — recipient has to `/start` the bot first.

## Troubleshooting

- **`Forbidden: bot was blocked by the user`** — recipient blocked the bot. They have to unblock via the chat menu.
- **`Forbidden: bot can't initiate conversation with a user`** — recipient never `/start`-ed. They need to open the bot URL and hit Start.
- **HTML parse errors** — Telegram is strict about HTML. If a custom field rendering injects raw `<` or `&` it can fail; the formatter escapes everything, but if you see HTML errors in the log, file an issue with the failing payload.
