# Email

Email is one of three delivery channels — [Telegram](telegram.md) and [Slack](slack.md) are the others. Each digest is rendered once and dispatched on every channel that has credentials configured; if your deployment only has `Slack:botToken` set, only Slack DMs go out, and the same goes for any other combination.

## SMTP setup

Configure in `appsettings.secrets.yaml`:

```yaml
Smtp:
  Host:     smtp.gmail.com
  Port:     465
  User:     you@example.com
  Password: "app-password-here"     # NOT your account password
  From:     you@example.com
  EnableSsl: true
```

### Gmail

Gmail blocks regular-password SMTP. Generate an [App Password](https://support.google.com/accounts/answer/185833): account → Security → 2-Step Verification (must be on) → App passwords → generate. Use that as `Password`. `Host: smtp.gmail.com`, `Port: 465`, `EnableSsl: true`.

### Other providers

Any SMTP-compliant server works — Office 365, Mailgun, SendGrid, Postmark, your company relay. Match the provider's documented `host:port` + auth.

### Testing locally

Run [MailHog](https://github.com/mailhog/MailHog) in Docker (`docker run -p 1025:1025 -p 8025:8025 mailhog/mailhog`), point Preesta at `Host: localhost`, `Port: 1025`, `EnableSsl: false`, and browse sent messages at `http://localhost:8025`. No real outbound traffic.

## What gets sent

A single email per `(To, Cc, Subject, Rule)` package — see [Routing model](../concepts/routing-model.md). Each one is:

- **HTML body** — the styled digest with status pills, priority dots, "Open in <tracker> →" headers
- **Plain-text body** — same content rendered for text-only clients (Telegram also uses this)
- **Subject** — `<SubjectPrefix> + rule.notify.subject`. The prefix lives in `Application:subjectPrefix` in `appsettings.yaml` — useful for `[PREESTA]` or `[DEV]` style tagging
- **From** — `Smtp:From`. Multiple Preesta deployments sharing one inbox should set distinct `From` addresses

## Recipients

`mailTo:` and `cc:` accept comma-separated values, each either a literal email or a [marker](../concepts/obezlichennye-rules.md#markers) (`assignee` / `reporter` / `creator`). Literals stay literal. Markers resolve per issue and are how Preesta produces one digest per distinct assignee.

## Why MailKit, not `System.Net.Mail.SmtpClient`

The .NET built-in `SmtpClient` is officially obsoleted by Microsoft (`SmtpClient` documentation explicitly says don't use it for new code). Preesta uses [MailKit](https://github.com/jstedfast/MailKit) — actively maintained, RFC-compliant, supports modern auth methods (XOAUTH2, NTLM). The integration point is `Messaging/SmtpClient.cs`.

## Troubleshooting

Common failure modes:

- **Authentication failed (5.7.8)** — wrong password or, for Gmail, you used your account password instead of an app password.
- **Relay denied (5.7.1)** — `From` address is a domain the SMTP server isn't authorized to send for.
- **Connection refused** — wrong host/port, or SSL/TLS mismatch (`EnableSsl: true` for port 465, `false` for 25/587 with STARTTLS — MailKit auto-negotiates STARTTLS).
- **No email but no error in logs either** — check `Application:subjectPrefix` and the recipient list resolution: `mailTo: assignee` with a tracker that returned empty `Email` for the assignee produces a package with no recipients, which SMTP silently skips. See [Routing model → When the assignee has no email](../concepts/routing-model.md#when-the-assignee-has-no-email).
