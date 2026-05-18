# Cron and schedules

Preesta has no built-in scheduler. The CLI is one-shot; **cron, systemd timers, Kubernetes CronJobs, or any other periodic invoker** are how you make it fire repeatedly.

## The `group:` tag is your schedule selector

Every rule has a `group:`. The CLI takes one schedule group as its sole argument. Rules with matching `group:` fire; everything else is silent.

This means **the schedule lives in the cron tab, the membership lives in `rules.yaml`**. Add a rule to a schedule by setting its `group:`; move it between schedules by changing the group. The cron tab itself is small — usually one line per group.

## Typical layout

```cron
# /etc/cron.d/preesta — pick the schedule that fits each rule cluster.
#
# Weekday morning standups
30 8  * * 1-5  preesta  cd /opt/preesta && /usr/bin/dotnet Preesta.dll morning-standup

# Every hour during business hours — stale-PR check
0  9-17 * * 1-5  preesta  cd /opt/preesta && /usr/bin/dotnet Preesta.dll stale-prs

# Once a week — overdue tickets digest
0  10 * * 1  preesta  cd /opt/preesta && /usr/bin/dotnet Preesta.dll weekly-overdue
```

The `preesta` username on the third field is the cron user; pick whichever local user owns `/opt/preesta` and has read on the secrets file (and write on the log destination).

## Kubernetes CronJob

One `CronJob` per schedule group is cleaner than packing everything into a single cron file:

```yaml
apiVersion: batch/v1
kind: CronJob
metadata:
  name: preesta-morning
spec:
  schedule: "30 8 * * 1-5"
  jobTemplate:
    spec:
      template:
        spec:
          restartPolicy: OnFailure
          containers:
            - name: preesta
              image: preesta:latest
              args: ["morning-standup"]
              # ... volume mounts for config + secrets ...
```

Benefits over a cron sidecar: independent retries per group, k8s native observability, easier to scale (add a new CronJob = add a new schedule).

## Schedule design guidelines

- **Pick the slowest frequency that's still useful.** A 5-minute cron firing a "stale PR" rule that's polling GitHub every 5 minutes is noisy and burns API quota; once an hour is plenty for stale checks.
- **Spread the load.** Don't schedule six groups at `0 9 * * *` — stagger by 5-10 minutes (`5 9`, `10 9`, …). Each group is one CLI process with all its tracker fetches in parallel anyway, but staggering smooths SMTP outbound and reduces simultaneous tracker load.
- **One rule, one group, one schedule.** Resist the urge to put multiple unrelated schedules into one group "because the rules are similar". Logs become hard to read when a group does too many things.
- **Test new rules in a `dev` group first.** Schedule it daily, point its recipients at yourself only, observe a week of digests before promoting to a team-facing group.

## Time zones

Preesta has no opinion about time zones — it runs whenever it's invoked. Cron, systemd, and Kubernetes all default to the host's local time; if you need UTC-anchored schedules, configure the underlying scheduler. The digest itself renders dates in the formatter's locale (which is the .NET default, currently `Russian (Russia)` — see `dd.MM.yyyy` format strings — so deployments outside RU may want to override in a future config knob).

## Failure modes

- **Cron silent failure** — if the cron command fails to start (missing `dotnet`, missing rules.yaml, …) cron mails the local user and that's it. Watch `/var/spool/mail/<user>` or pipe `2>&1 | logger -t preesta` to make failures visible.
- **Overlapping invocations** — Preesta is stateless, so a long-running invocation followed immediately by another isn't a correctness problem (they don't share state). But two invocations of the same group running concurrently will double every email. Use cron's `flock` or k8s's `concurrencyPolicy: Forbid` if your runs can exceed the interval.
- **Empty digests** — Preesta sends nothing if no rule produced any matches. This is intentional — no noise on quiet days. If you want a "nothing today" heartbeat, write a rule with no filter that always matches and route it to yourself.
