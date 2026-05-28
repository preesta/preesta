# Installation

Two supported paths: **Docker** (recommended for production) and **plain .NET 8** (development, custom hosts).

## Docker

The repo ships a `Dockerfile` that builds Preesta on `mcr.microsoft.com/dotnet/runtime:8.0`. Build and run:

=== "Linux / macOS / WSL2 / Git Bash"

    ```bash
    docker build -t preesta:latest .
    docker run --rm \
      -v "$(pwd)/config:/app/config:ro" \
      -e PREESTA_GROUP=daily \
      preesta:latest
    ```

=== "Windows PowerShell"

    ```powershell
    docker build -t preesta:latest .
    docker run --rm `
      -v "${PWD}/config:/app/config:ro" `
      -e PREESTA_GROUP=daily `
      preesta:latest
    ```

The `config/` directory must contain `appsettings.yaml`, `rules.yaml`, and optionally `secrets/appsettings.secrets.yaml`. They're mounted read-only — the container has no other state.

### `preesta-cron`

The repo's `preesta-cron/` directory contains a wrapper image that bundles Preesta + `cron`. Mount your `crontab` file, the rules+secrets, and the image runs them on schedule:

=== "Linux / macOS / WSL2 / Git Bash"

    ```bash
    docker run --rm \
      -v "$(pwd)/config:/app/config:ro" \
      -v "$(pwd)/crontab:/etc/cron.d/preesta:ro" \
      preesta-cron:latest
    ```

=== "Windows PowerShell"

    ```powershell
    docker run --rm `
      -v "${PWD}/config:/app/config:ro" `
      -v "${PWD}/crontab:/etc/cron.d/preesta:ro" `
      preesta-cron:latest
    ```

Example `crontab`:

```cron
0 9 * * 1-5  /usr/bin/dotnet /app/Preesta.dll daily       2>&1 | logger -t preesta
0 14 * * 1-5 /usr/bin/dotnet /app/Preesta.dll standups    2>&1 | logger -t preesta
```

See [Cron and schedules](cron-and-schedules.md) for the full schedule design.

## Kubernetes

> TODO — provide CronJob manifest + Helm chart skeleton.

For now a minimal `CronJob`:

```yaml
apiVersion: batch/v1
kind: CronJob
metadata:
  name: preesta-daily
spec:
  schedule: "0 9 * * 1-5"
  jobTemplate:
    spec:
      template:
        spec:
          restartPolicy: OnFailure
          containers:
            - name: preesta
              image: preesta:latest
              args: ["daily"]
              volumeMounts:
                - name: config
                  mountPath: /app/config
                  readOnly: true
                - name: secrets
                  mountPath: /app/config/secrets
                  readOnly: true
          volumes:
            - name: config
              configMap:
                name: preesta-config
            - name: secrets
              secret:
                secretName: preesta-secrets
```

`preesta-config` ConfigMap holds `appsettings.yaml` + `rules.yaml`; `preesta-secrets` Secret holds `appsettings.secrets.yaml`. Multiple CronJobs (one per schedule group) keep the schedule definition in Kubernetes rather than a separate cron file.

## Plain .NET 8 (development)

```bash
git clone https://github.com/preesta/preesta.git
cd preesta
dotnet build
cd Preesta
dotnet run -- <schedule-group>
```

`bin/Debug/net8.0` is added to `Preesta.csproj` as the working directory for `dotnet run` — that's where the built `appsettings.yaml` / `rules.yaml` end up.

For tests:

```bash
dotnet test
```

200+ tests, all in-process (WireMock for HTTP-server stubs, NSubstitute for unit-level mocks). No external service dependencies.

## Upgrading

See [Upgrading](upgrading.md) for the breaking-change policy and per-version notes.
