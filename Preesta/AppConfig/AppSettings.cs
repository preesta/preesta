using System;
using System.Linq;
using Messaging;
using Microsoft.Extensions.Configuration;
using NetEscapades.Configuration.Yaml;
using System.IO;
using static System.String;
using System.Text;

namespace Preesta.AppConfig
{

    internal class AppSettings
    {
        //private readonly string _assemblyDir;
        //private readonly IConfigurationSection _configSection;

        private readonly IConfigurationRoot _configuration = BuildConfiguration();

        private static IConfigurationRoot BuildConfiguration()
        {
            // Anchor every file lookup at the binary's own directory. With `dotnet run`
            // the CWD is the .csproj folder, not the publish output, so a plain
            // File.Exists("appsettings.yaml") used to miss the file copied next to the
            // assembly and fall through to a hard-coded path-by-extension default.
            var baseDir = AppContext.BaseDirectory;
            string? FirstExisting(params string[] names) =>
                names.Select(n => Path.Combine(baseDir, n)).FirstOrDefault(File.Exists);

            var builder = new ConfigurationBuilder()
                .SetBasePath(baseDir)
                .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(
@"
{
    ""Logger"": {
        ""Serilog"": {
            ""Using"": [""Serilog.Sinks.Console""],
            ""WriteTo"": [{""Name"": ""Console""}]
        }
    }
}
"
                )));

            var settings = FirstExisting("appsettings.yaml", "appsettings.yml");
            if (settings != null)
                builder.AddYamlFile(Path.GetFileName(settings), optional: false, reloadOnChange: true);
            else
                builder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            var secrets = FirstExisting(
                "secrets/appsettings.secrets.yaml",
                "secrets/appsettings.secrets.yml");
            if (secrets != null)
                builder.AddYamlFile(Path.GetRelativePath(baseDir, secrets), optional: true);
            else
                builder.AddJsonFile("secrets/appsettings.secrets.json", optional: true);

            return builder.Build();
        }

        internal void Validate()
        {
            // At least one issue source must be configured — none is privileged,
            // Jira included. With no source, there's nothing to digest.
            var hasSource = Jira != null
                            || !string.IsNullOrEmpty(LinearApiKey)
                            || !string.IsNullOrEmpty(GithubToken)
                            || !string.IsNullOrEmpty(GitlabToken)
                            || !string.IsNullOrEmpty(ShortcutApiToken);
            if (!hasSource)
            {
                throw new ArgumentException(
                    "No issue source configured — set at least one of "
                    + "Jira, Linear, Github, Gitlab, or Shortcut.",
                    "Jira/Linear/Github/Gitlab/Shortcut");
            }

            // At least one delivery channel must be configured — otherwise rules
            // would match issues but have nowhere to send them. Smtp / Telegram /
            // Slack are all independent: any one of them is enough; none of them
            // is privileged. See docs/delivery/* for the per-channel setup.
            var hasSmtp     = Smtp is not null;
            var hasTelegram = !string.IsNullOrEmpty(TelegramBotToken);
            var hasSlack    = !string.IsNullOrEmpty(SlackBotToken);
            if (!hasSmtp && !hasTelegram && !hasSlack)
            {
                throw new ArgumentException(
                    "No delivery channel configured — set at least one of "
                    + "Smtp section, Telegram:botToken, or Slack:botToken.",
                    "Smtp/Telegram/Slack");
            }
        }

        public string LogoFileName => _configuration["Application:logoFileName"] ?? Empty;

        public string LocalRulesFileName => _configuration["Application:rulesFileName"] ?? "rules.xml";

        public JiraConfig? Jira => JiraConfigLoader.Load(_configuration.GetSection("Jira"));

        public string[] Supervisors => (_configuration["Application:supervisors"] ?? Empty).Split(',', StringSplitOptions.RemoveEmptyEntries);

        public string[] Maintainers => (_configuration["Application:maintenanceTeam"] ?? Empty).Split(',', StringSplitOptions.RemoveEmptyEntries);

        public string SubjectPrefix => _configuration["Application:subjectPrefix"] ?? Empty;

        public string? TelegramBotToken => _configuration["Telegram:botToken"];

        public string? SlackBotToken => _configuration["Slack:botToken"];

        public string? LinearApiKey => _configuration["Linear:apiKey"];

        public string? LinearWorkspace => _configuration["Linear:workspace"];

        public string? GithubToken => _configuration["Github:token"];

        public string? GitlabToken => _configuration["Gitlab:token"];

        /// <summary>
        /// Base GraphQL endpoint URL for GitLab. Defaults to <c>https://gitlab.com/api/graphql</c>
        /// when not set, so users targeting GitLab.com need only configure the token.
        /// Self-hosted instances point this at <c>https://gitlab.example.com/api/graphql</c>.
        /// </summary>
        public string? GitlabApiBase => _configuration["Gitlab:apiBase"];

        public string? ShortcutApiToken => _configuration["Shortcut:apiToken"];

        public SmtpConfig? Smtp => SmtpConfigLoader.Load(_configuration.GetSection("Smtp"));

        public IConfigurationSection LoggerSection => _configuration.GetSection("Logger");
    }
}
