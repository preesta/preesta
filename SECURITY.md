# Security policy

## Reporting a vulnerability

Please use GitHub's **private vulnerability reporting**:

→ <https://github.com/preesta/preesta/security/advisories/new>

That keeps the report between you and the maintainer until a fix is ready. Public issues are fine for non-security bug reports.

## What's in scope

- The Preesta CLI itself and the published Docker image (`ghcr.io/preesta/preesta`)
- The released self-contained binaries from <https://github.com/preesta/preesta/releases>
- The MkDocs site at <https://preesta.dev/>

## What's out of scope

- Vulnerabilities in upstream tracker APIs (Jira, Linear, GitHub, GitLab, Shortcut) — report directly to the vendor
- Misconfiguration in the operator's own `appsettings.secrets.yaml` (leaked tokens, weak SMTP creds) — that's deployment hygiene, not a Preesta defect
- DoS from rules that fetch unbounded result sets — `rules.yaml` is operator-controlled config
