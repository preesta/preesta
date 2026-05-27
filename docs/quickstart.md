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

Two rules in one group answer the morning-standup question *"what blockers need eyes today?"* — one for blockers that have no owner yet, one for blockers an owner has but hasn't started.

```yaml
rules:
  # 1. Blocker exists, nobody owns it — surface to the team lead and
  #    drop an automated comment so the issue can't quietly rot.
  - type: github
    group: daily-blockers
    filter: "is:open is:issue repo:your-org/your-repo label:blocker no:assignee"
    notify:
      subject: "Unassigned blocker — needs an owner"
      mailTo: team-lead@example.com
    mutations:
      - mutation: |
          mutation {
            addComment(input: {
              subjectId: "{{@issueId}}",
              body: "This blocker has no assignee. Please pick it up or hand it off."
            }) { clientMutationId }
          }

  # 2. Blocker has an owner but isn't moving yet — ping the owner.
  - type: github
    group: daily-blockers
    filter: "is:open is:issue repo:your-org/your-repo label:blocker -label:in-progress"
    notify:
      subject: "Your blocker hasn't been picked up"
      mailTo: assignee
```

Look at what's *not* in either rule: no identity. Rule 1 routes the unassigned set to a literal address (the lead, who triages) **and** runs a GraphQL mutation against each match. Rule 2 routes the owned-but-stalled set with the `assignee` marker — Preesta groups matches by assignee email and sends each distinct owner their own slice. One rule, one digest per actual recipient.

Add a teammate to the repo and **they start receiving their digest the moment they get assigned a stalled blocker** — without you touching `rules.yaml`. Remove them and they stop. The rule outlives team membership.

`filter:` is a raw GitHub search query — the same syntax you type into the web search bar. Compose any combination of labels, age, milestone, etc. that captures a real "this needs eyes today" condition. GitHub search doesn't do relative-time staleness ("not touched for 30 minutes"); for that pattern use Jira — JQL has `updated < -30m`, `due < now()`, `status != "In Progress"` and friends. Same architectural shape across trackers: impersonal filter + `mailTo: assignee` (+ optional `mutations:`). The marker mechanics are in [Routing model](concepts/routing-model.md); the full mutation surface is on the per-tracker pages.

## 2. Run

```bash
docker run --rm \
  -v "$(pwd)/secrets:/app/secrets:ro" \
  -v "$(pwd)/rules.yaml:/app/rules.yaml:ro" \
  ghcr.io/preesta/preesta:latest \
  preesta daily-blockers
```

The log lists matches per rule, then sends. Substitute your own address for `team-lead@example.com` and you receive **two emails**:

1. From rule 1 — the unassigned-blocker triage list. Each matched issue also gets an automated GitHub comment from the rule's mutation.
2. From rule 2 — only the stalled blockers actually assigned to you.

Teammates with exposed `User.email` get their own slice of rule 2 in parallel. (GitHub returns `""` for users who've hidden their email; those issues stay in the matched set but the marker skips routing — see [Routing model](concepts/routing-model.md#when-the-assignee-has-no-email).) Each email links every issue to its GitHub page plus an "Open in GitHub →" header pointing at the same search query.

Sanity check the image first if you like:

```bash
docker run --rm ghcr.io/preesta/preesta:latest preesta --version
```

## 3. Schedule it

The bundled container CMD runs [supercronic](https://github.com/aptible/supercronic) against `/app/preesta-cron` — drop a crontab in there and `docker run -d` without overriding the CMD:

```cron
# /app/preesta-cron
0 9 * * 1-5  preesta daily-blockers
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
./Preesta daily-blockers
```

Needs the .NET 8 SDK; the build copies the project's `appsettings.yaml` and your `secrets/`, `rules.yaml` into the output directory.
