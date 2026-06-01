# Quickstart

From "nothing installed" to "first digest in my inbox" in about five minutes. We'll use **Jira** as the source (the canonical Preesta tracker — real status field, real priority, relative-time JQL) and **email** as the only delivery channel; Telegram and Slack are described on their own pages, other trackers on theirs.

## Prerequisites

- **Docker**
- **Jira API token** — get one at <https://id.atlassian.com/manage-profile/security/api-tokens>
- **SMTP account** — Gmail with an [app password](https://support.google.com/accounts/answer/185833) is the simplest

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
  - tracker: jira
    tags: blocker-watch
    filter: 'priority = Blocker AND status = "Open" AND assignee is not EMPTY AND updated < -30m'
    notify:
      subject: "Your blocker hasn't been picked up (30+ min)"
      mailTo: assignee
```

Look at what's *not* in the rule: no identity. The JQL says *which issues* (blocker priority, still in *Open* status, assigned to someone, last touched more than 30 minutes ago), and `mailTo: assignee` is a marker that resolves per matched issue. Preesta groups matches by assignee email and sends each distinct owner their own slice. Add a teammate to the project and **they start receiving their digest the moment they get assigned a stalled blocker** — without you touching `rules.yaml`.

`filter:` is raw JQL — the same expression you type into Jira's advanced search bar. Use whatever query catches a real "this needs eyes today" condition. The marker mechanics (`assignee` / `reporter` / `creator`, mixing literals with markers, email→Telegram/Slack ID maps) are in [Routing model](concepts/routing-model.md).

> Unassigned blockers don't match `mailTo: assignee` and silently drop. Closing that gap — auto-assigning the unowned ones to a triager so the queue can't grow — is a two-rule loop in the cookbook: [Auto-triage blockers](cookbook/auto-triage-blockers.md).

## 2. Run

=== "Linux / macOS / WSL2 / Git Bash"

    ```bash
    docker run --rm \
      -v "$(pwd)/secrets:/app/secrets:ro" \
      -v "$(pwd)/rules.yaml:/app/rules.yaml:ro" \
      ghcr.io/preesta/preesta:latest \
      preesta blocker-watch
    ```

=== "Windows PowerShell"

    ```powershell
    docker run --rm `
      -v "${PWD}/secrets:/app/secrets:ro" `
      -v "${PWD}/rules.yaml:/app/rules.yaml:ro" `
      ghcr.io/preesta/preesta:latest `
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

Or use any external scheduler — host cron, systemd timer, Kubernetes CronJob, GitHub Actions on a schedule. Each tick is one `docker run`, optionally passing tags as arguments to limit which rules fire (`preesta blocker-watch` runs every rule tagged `blocker-watch`; `preesta` with no arguments runs every rule in the file).

## Next steps

- Browse the [Concepts](concepts/rule-anatomy.md) section — three short pages cover the rest of the surface area.
- Add another tracker: [Linear](trackers/linear.md), [GitHub](trackers/github.md), [GitLab](trackers/gitlab.md), [Shortcut](trackers/shortcut.md). All four are equal sources — pick the ones you actually use.
- Wire up [Telegram](delivery/telegram.md) or [Slack](delivery/slack.md) — same digest, different channel.
- Copy a realistic rule from the [Cookbook](cookbook/index.md).

## Self-contained binary alternative

Tagged releases (`vX.Y.Z`) ship binaries for linux-x64/arm64, osx-x64/arm64, win-x64 on the [Releases page](https://github.com/preesta/preesta/releases). Unpack, drop your `secrets/` and `rules.yaml` next to it, run `./preesta` (or `./preesta <tag>` to filter). No Docker — the runtime is bundled.
