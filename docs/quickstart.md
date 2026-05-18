# Quickstart

This walks you from "nothing installed" to "first digest in my inbox" in about ten minutes. We'll use **GitHub Issues** as the source and **email** as the only delivery channel — Telegram and Slack are described on their own pages.

## Prerequisites

- .NET 8 SDK (`dotnet --version` should print 8.x)
- A GitHub Personal Access Token with `repo` (or `public_repo`) **and** `user:email` scopes — the email scope is mandatory because Preesta routes digests using the `User.email` GraphQL field
- An SMTP account you can send from (Gmail with an [app password](https://support.google.com/accounts/answer/185833) is the simplest)

## 1. Clone and build

```bash
git clone https://github.com/preesta/preesta.git
cd preesta
dotnet build
```

## 2. Set your secrets

`Preesta/secrets/appsettings.secrets.yaml` is gitignored and lives next to the public `appsettings.yaml`. Create it:

```yaml
Smtp:
  User: you@example.com
  Password: "your-app-password"
  From:    you@example.com

Github:
  token: "ghp_xxxxxxxxxxxxxxxxxxxxxxxxxx"
```

(The other tracker tokens and `Telegram` / `Slack` bot tokens live in the same file. See [Secrets & tokens](operations/secrets-and-tokens.md) for the full list.)

## 3. Write your first rule

`Preesta/rules.yaml` (you can keep the existing rules — Preesta groups rules by `group:` and you target a group on the command line):

```yaml
rules:
  - type: github
    group: hello-preesta
    filter: "is:open is:issue assignee:@me sort:updated-desc"
    notify:
      subject: "Things on me in GitHub"
      mailTo: you@example.com
```

The `filter:` is a raw GitHub search query — the same syntax you see in the web search bar. Use whatever queries you actually run by hand.

!!! warning "Don't embed `assignee:@me` in shared rules"
    For a personal digest this works because the rule is for you. In team setups you want a rule like `is:open is:issue label:urgent` that fans out: one digest per assignee, addressed via the `mailTo: assignee` marker. See [Routing model](concepts/routing-model.md).

## 4. Run

```bash
cd Preesta
dotnet run -- hello-preesta
```

You should see a `GitHub mutation succeeded`-free log block ending with the SMTP send. Within a few seconds the email lands in your inbox with one row per matched issue, each linked to its GitHub page, plus an "Open in GitHub →" link in the header pointing at the same search query.

## 5. Schedule it

Cron tab:

```cron
# Every weekday at 9:00 send the hello-preesta digest
0 9 * * 1-5  cd /opt/preesta && /usr/bin/dotnet Preesta.dll hello-preesta
```

Or use the [Docker image and the bundled `preesta-cron` wrapper](operations/installation.md). Or any scheduler — Preesta is a single CLI invocation per tick.

## Next steps

- Browse the [Concepts](concepts/architecture.md) section — three short pages that explain the rest of the surface area
- Add a tracker: [Jira](trackers/jira.md), [Linear](trackers/linear.md), [GitLab](trackers/gitlab.md), [Shortcut](trackers/shortcut.md)
- Wire up Telegram or Slack delivery — same digest, different channel
- Copy a realistic rule from the [Cookbook](cookbook/index.md)
