# Quickstart

From "nothing installed" to "first digest in my inbox" in about five minutes. We'll use **Jira** as the source (the canonical Preesta tracker — real status field, real priority, relative-time JQL) and **email** as the only delivery channel; Telegram and Slack are described on their own pages, other trackers on theirs.

## Prerequisites

- **Docker** (the only thing you install — no .NET SDK, no clone, no build).
- A **Jira API token** from <https://id.atlassian.com/manage-profile/security/api-tokens>. For Atlassian Cloud you authenticate with your email + the API token (used as the password in HTTP Basic).
- An **SMTP account** you can send from (Gmail with an [app password](https://support.google.com/accounts/answer/185833) is the simplest).

## 1. Set up a config directory

Create a folder anywhere — two files live in it.

```bash
mkdir preesta && cd preesta
mkdir secrets
```

### `secrets/appsettings.secrets.yaml` — tokens (gitignore this)

```yaml
Smtp:
  Host:     smtp.gmail.com
  From:     you@example.com
  User:     you@example.com
  Password: "your-app-password"     # not your account password — see Gmail link above

Jira:
  rootUri:  "https://your-company.atlassian.net/"
  userName: "you@example.com"
  password: "ATATT3xFfGF0xxxxxxxxxxxxxxxxxxxx"   # the Jira API token
```

### `rules.yaml` — what to digest, to whom

One rule, one notification: every owner of a blocker that's been sitting more than 30 minutes without moving to *In Progress* gets pinged about *their* blockers.

```yaml
rules:
  - type: jql
    group: blocker-watch
    jql: 'priority = Blocker AND resolution = EMPTY AND status != "In Progress" AND assignee is not EMPTY AND updated < -30m'
    notify:
      subject: "Your blocker hasn't been picked up (30+ min)"
      mailTo: assignee
```

Look at what's *not* in the rule: no identity. The JQL says *which issues* (blocker priority, still unresolved, not yet *In Progress*, assigned to someone, last touched more than 30 minutes ago — `resolution = EMPTY` is the canonical Jira "this is still open work, regardless of which custom status it's in" clause, so Closed/Resolved/Done/Duplicate/etc. all fall out), and `mailTo: assignee` is a marker that resolves per matched issue. Preesta groups matches by assignee email and sends each distinct owner their own slice. Add a teammate to the project and **they start receiving their digest the moment they get assigned a stalled blocker** — without you touching `rules.yaml`.

`jql:` is raw JQL — the same expression you type into Jira's advanced search bar. Use whatever query catches a real "this needs eyes today" condition. The marker mechanics (`assignee` / `reporter` / `creator`, mixing literals with markers, email→Telegram/Slack ID maps) are in [Routing model](concepts/routing-model.md).

> Unassigned blockers don't match `mailTo: assignee` and silently drop. Closing that gap — auto-assigning the unowned ones to a triager so the queue can't grow — is a two-rule loop in the cookbook: [Auto-triage blockers](cookbook/auto-triage-blockers.md).

## 2. Run

```bash
docker run --rm \
  -v "$(pwd)/secrets:/app/secrets:ro" \
  -v "$(pwd)/rules.yaml:/app/rules.yaml:ro" \
  ghcr.io/preesta/preesta:latest \
  preesta blocker-watch
```

A log block prints the matches, then one SMTP send per distinct assignee. Within seconds an email lands in your inbox listing **only the blockers actually assigned to you and stalled for 30+ minutes** — each linked to its Jira page, plus an "Open in Jira →" header pointing at the same JQL. Teammates with a visible Jira email get their own slice in parallel.

Sanity check the image first if you like:

```bash
docker run --rm ghcr.io/preesta/preesta:latest preesta --version
```

## 3. Schedule it

The bundled container CMD runs [supercronic](https://github.com/aptible/supercronic) against `/app/preesta-cron` — drop a crontab in there and `docker run -d` without overriding the CMD:

```cron
# /app/preesta-cron — blockers need tight latency
*/10 * * * *  preesta blocker-watch
```

Or use any external scheduler — host cron, systemd timer, Kubernetes CronJob, GitHub Actions on a schedule. Each tick is one `docker run` with the group name as the argument.

## Next steps

- Browse the [Concepts](concepts/architecture.md) section — three short pages cover the rest of the surface area.
- Add another tracker: [Linear](trackers/linear.md), [GitHub](trackers/github.md), [GitLab](trackers/gitlab.md), [Shortcut](trackers/shortcut.md). All four are equal sources — pick the ones you actually use.
- Wire up [Telegram](delivery/telegram.md) or [Slack](delivery/slack.md) — same digest, different channel.
- Copy a realistic rule from the [Cookbook](cookbook/index.md).

## Other install paths

### Self-contained binary

Tagged releases (`vX.Y.Z`) ship binaries for linux-x64/arm64, osx-x64/arm64, win-x64 on the [Releases page](https://github.com/preesta/preesta/releases). Unpack, drop your `secrets/` and `rules.yaml` next to it, run `./preesta <group>`. No Docker, no .NET install — the runtime is bundled.

### From source

For contributors and people running off `main`:

```bash
git clone https://github.com/preesta/preesta.git
cd preesta
dotnet build
cd Preesta/bin/Debug/net8.0
./Preesta blocker-watch
```

Needs the .NET 8 SDK; the build copies the project's `appsettings.yaml` and your `secrets/`, `rules.yaml` into the output directory.
