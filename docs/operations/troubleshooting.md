# Troubleshooting

The 80/20 of "why isn't my digest going out?". Sorted roughly by frequency.

## The digest didn't arrive at all

In order of likelihood:

1. **Rule was dropped at parse time.** Check the log for `Rule conversion failed` or `<TrackerName> rule must …`. Common causes:
   - GitLab rule with no filter chips
   - Linear rule with 0 or 2+ of {filter, filterRaw, viewId}
   - GitHub / Shortcut rule with empty `filter:`
2. **No matches.** Preesta sends nothing for empty result sets — by design. Log will have `0 rules with tracker=X found for tags [Y]` if no rules fired, or no further lines if rules fired but matched zero issues. Run the same filter directly in the tracker's web UI to confirm.
3. **Recipient resolution skipped everything.** `mailTo: assignee` with a tracker that returned empty emails for every assignee (GitHub hidden-email, GitLab `publicEmail = null`) produces a package with `To: ""` and the channels silently skip it. Add `Reporter` columns to the digest temporarily — if the issues render but no email goes out, that's the symptom.
4. **SMTP authentication failed.** Look for `MailKit.Net.Smtp.SmtpCommandException`. Gmail's "wrong password" usually means you used the account password where an [app password](../delivery/email.md#gmail) is required.
5. **SMTP send queued but blocked.** Some providers (notably Gmail) silently drop messages that look spammy when `From:` is a domain you don't control. Check the SMTP provider's outbound dashboard.

## The link in the digest goes to the wrong place

- **"Open in <tracker> →" lands on an unauthorized page** — usually a session issue in the browser (you're logged into account A, link is for workspace B). Re-test in an incognito window.
- **Per-issue link 404s** — the underlying issue was deleted between fetch and digest send. Rare; not actionable.
- **GitLab dashboard link is empty** — the dashboard filters by *the viewing user*, not by the token owner. If you click as someone who has no issues matching the chips, you see "no work items". Try in your tracker UI with the same chips manually.

## Mutations didn't run

- **Permission error.** GitHub: missing `repo` scope. Linear: API key was created in a workspace where the user doesn't have write. GitLab: token has `read_api` but not `api`. Shortcut: token was created with `Read-only` checked (`sct_ro_*` prefix instead of `sct_rw_*`).
- **Mutation body has a missing marker.** `{{@issueId}}` resolved to `""` because the rule type doesn't populate that ID flavour (e.g. `LinearId` is null on a GitHub-type rule). Check the rendered body in the log.
- **GraphQL `errors` envelope** — even with a 200 response, GraphQL APIs can return `{errors: [...]}` for partial failures. Preesta logs them at `Error` and moves on.

## The digest is duplicated

- **Two cron invocations of the same tag ran concurrently.** Add `flock` to the cron command, or set `concurrencyPolicy: Forbid` on the Kubernetes CronJob.
- **Two cron entries fire the same tag.** Check the cron tab.
- **Both `mailTo:` and `cc:` list the same email** — Preesta dedupes within a single package, but if two rules sharing a tag target the same person with different subjects, they'll get two emails. That's by design — one rule, one digest, one email.

## Logs are noisy

The default `Logger` level is `Verbose`. Production should usually run at `Information`:

```yaml
Logger:
  Serilog:
    MinimumLevel: Information
```

Set per-namespace overrides for noisier modules if needed. Verbose is only useful when debugging rule conversion or recipient resolution.

## Still stuck

- Re-run with `Verbose` logging and grep for `Error` and `Warning` lines.
- Run the same filter directly in the tracker's web UI — if no matches there either, the rule is doing the right thing (just nothing to send).
- Open an issue with the rule (redacted) and the relevant log section.
