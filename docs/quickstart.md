# Quickstart

From "nothing installed" to "first digest in my inbox" in about five minutes. We'll use **GitHub Issues** as the source and **email** as the only delivery channel — Telegram and Slack are described on their own pages.

## Prerequisites

- **Docker** (the only thing you install — no .NET SDK, no clone, no build).
- A **GitHub Personal Access Token** with `repo` (or `public_repo`) **and** `user:email` scopes — the email scope is mandatory because Preesta routes digests via the `User.email` GraphQL field.
- An **SMTP account** you can send from (Gmail with an [app password](https://support.google.com/accounts/answer/185833) is the simplest).

## 1. Set up a config directory

Create a folder anywhere — three files live in it.

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
  Password: "your-app-password"   # not your account password — see Gmail link above

Github:
  token: "ghp_xxxxxxxxxxxxxxxxxxxxxxxxxx"
```

### `rules.yaml` — what to digest, to whom

```yaml
rules:
  - type: github
    group: hello-preesta
    filter: "is:open is:issue repo:your-org/your-repo"
    notify:
      subject: "Open issues on you"
      mailTo: assignee
```

Look at what's *not* in that rule: **no identity**. The filter says *which issues* (open issues in one repo), and `mailTo: assignee` is a marker that resolves per matched issue. Preesta groups the matches by assignee email and sends each distinct assignee their own slice. One rule, N digests, one per actual recipient.

Run it solo and you receive only the issues actually assigned to you. Add a teammate to the repo and **they automatically start getting their digest the moment they get assigned** — without you touching `rules.yaml`. Remove them and they stop getting digests the moment they stop being assigned. The rule outlives team membership.

`filter:` is a raw GitHub search query — the same syntax you type into the web search bar. Use whatever queries you actually run by hand. See [Routing model](concepts/routing-model.md) for the full marker mechanics (`assignee` / `reporter` / `creator`, mixing literals with markers, email→Telegram/Slack ID maps).

## 2. Run

```bash
docker run --rm \
  -v "$(pwd)/secrets:/app/secrets:ro" \
  -v "$(pwd)/rules.yaml:/app/rules.yaml:ro" \
  ghcr.io/preesta/preesta:latest \
  preesta hello-preesta
```

A log block prints the matched issues, then one SMTP send per distinct assignee. Within seconds an email lands in your inbox containing **only the issues actually assigned to you**, each linked to its GitHub page, plus an "Open in GitHub →" link in the header pointing at the same search query. Teammates with exposed `User.email` get their own digests in parallel — same rule, different slice each. (GitHub returns `""` for users who've hidden their email; their issues stay in the run but the marker skips routing for them. See [Routing model](concepts/routing-model.md#when-the-assignee-has-no-email).)

Sanity check the image first if you like:

```bash
docker run --rm ghcr.io/preesta/preesta:latest preesta --version
```

## 3. Schedule it

The bundled container CMD runs [supercronic](https://github.com/aptible/supercronic) against `/app/preesta-cron` — drop a crontab in there and `docker run -d` without overriding the CMD:

```cron
# /app/preesta-cron
0 9 * * 1-5  preesta hello-preesta
```

Or use any external scheduler — host cron, systemd timer, Kubernetes CronJob, GitHub Actions on a schedule. Each tick is one `docker run` with the group name as the argument.

## Next steps

- Browse the [Concepts](concepts/architecture.md) section — three short pages cover the rest of the surface area.
- Add a tracker: [Jira](trackers/jira.md), [Linear](trackers/linear.md), [GitLab](trackers/gitlab.md), [Shortcut](trackers/shortcut.md).
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
./Preesta hello-preesta
```

Needs the .NET 8 SDK; the build copies the project's `appsettings.yaml` and your `secrets/`, `rules.yaml` into the output directory.
