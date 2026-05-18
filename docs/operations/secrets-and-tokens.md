# Secrets and tokens

Everything Preesta needs to authenticate lives in **one gitignored file**: `Preesta/secrets/appsettings.secrets.yaml`. It overlays `appsettings.yaml` at load time — anything you set here wins.

## Full reference

```yaml
Jira:
  apiToken: "ATATT3xFf..."                 # Atlassian API token
  # userName: "you@example.com"            # Server fallback
  # password: "..."

Linear:
  apiKey: "lin_api_..."

Github:
  token: "ghp_..."                         # scopes: repo + user:email

Gitlab:
  token: "glpat-..."                       # scopes: read_api (+ api for mutations)

Shortcut:
  apiToken: "sct_rw_workspace_..."         # sct_ro_* for read-only

Smtp:
  User:     "you@example.com"
  Password: "app-password"
  From:     "you@example.com"

Telegram:
  botToken: "12345:AAEV..."

Slack:
  botToken: "xoxb-..."
```

## Rotation

When you rotate a token, replace the value and run Preesta. No restart of any persistent process — it's a CLI, every invocation re-reads config.

If you use cron and a token expires mid-cycle, the affected pipeline starts logging `Error` lines until you replace the value. The other pipelines stay healthy.

## Storage in production

- **Plain file** — fine for self-hosted single-server deployments. Permissions: `chmod 600`, owned by the user running cron.
- **Docker secrets** — mount the file as a secret, not a config:
  ```yaml
  services:
    preesta:
      image: preesta:latest
      secrets:
        - source: preesta_secrets
          target: /app/secrets/appsettings.secrets.yaml
  secrets:
    preesta_secrets:
      file: ./secrets/appsettings.secrets.yaml
  ```
- **Kubernetes Secret** — mount as a volume at `/app/secrets/appsettings.secrets.yaml`. See [Installation → Kubernetes](installation.md#kubernetes).
- **Vault / Doppler / 1Password / SOPS** — `appsettings.secrets.yaml` is plain YAML, so it round-trips through any templating system that emits files. Render before invocation.

## What if I leak a token

1. Revoke at the tracker UI immediately. URLs:
   - Jira: [id.atlassian.com/manage-profile/security/api-tokens](https://id.atlassian.com/manage-profile/security/api-tokens)
   - Linear: Settings → Account → Security & Access → API
   - GitHub: [github.com/settings/tokens](https://github.com/settings/tokens)
   - GitLab: User → Edit profile → Access Tokens
   - Shortcut: [app.shortcut.com/settings/account/api-tokens](https://app.shortcut.com/settings/account/api-tokens)
2. Generate a replacement.
3. Update `appsettings.secrets.yaml`.
4. Audit recent activity in each tracker for unexpected actions (mutations especially).
5. If the token was in a git commit, rotate is *not* enough — the old value lives in git history forever. After revoking, do a `git filter-repo` pass if the repo is public, or just accept it (the token is dead, the history is harmless) if private.

## Audit

There's no separate audit log — Preesta logs every mutation it issues at `Information` level with the truncated body. If you suspect an unauthorized run, grep your log sink for `mutation succeeded` and check timestamps + bodies.
