FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS publish
WORKDIR /src
COPY . .
RUN dotnet publish "./Preesta/Preesta.csproj" -c Release -o /app/publish \
  --runtime linux-x64 \
  --self-contained false \
  /p:PublishTrimmed=false \
  /p:PublishSingleFile=false

# ---------------------------------

FROM curlimages/curl AS get-rootless-cron
WORKDIR /tmp
RUN curl -fsSLO https://github.com/aptible/supercronic/releases/download/v0.1.12/supercronic-linux-amd64 \
 && echo "048b95b48b708983effb2e5c935a1ef8483d9e3e  supercronic-linux-amd64" | sha1sum -c -

# ---------------------------------

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
LABEL org.opencontainers.image.source=https://github.com/preesta/preesta
WORKDIR /app
COPY --from=publish /app/publish/ .
RUN ln -s /app/Preesta /usr/local/bin/preesta
COPY --from=get-rootless-cron /tmp/supercronic-linux-amd64 /usr/local/bin/supercronic
RUN chmod +x /usr/local/bin/supercronic /usr/local/bin/preesta
ADD preesta-cron .
RUN adduser \
  --disabled-password \
  --home /app \
  --gecos '' app \
  && chown -R app /app
USER app
CMD supercronic -passthrough-logs /app/preesta-cron
