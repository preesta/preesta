using System;
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
            var builder = new ConfigurationBuilder()
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

            if (File.Exists("appsettings.yaml"))
                builder.AddYamlFile("appsettings.yaml", optional: false, reloadOnChange: true);
            else if (File.Exists("appsettings.yml"))
                builder.AddYamlFile("appsettings.yml", optional: false, reloadOnChange: true);
            else
                builder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            if (File.Exists("secrets/appsettings.secrets.yaml"))
                builder.AddYamlFile("secrets/appsettings.secrets.yaml", optional: true);
            else if (File.Exists("secrets/appsettings.secrets.yml"))
                builder.AddYamlFile("secrets/appsettings.secrets.yml", optional: true);
            else
                builder.AddJsonFile("secrets/appsettings.secrets.json", optional: true);

            return builder.Build();
        }

        internal void Validate()
        {
            if(JiraRootUri == null)
            {
                throw new ArgumentException("Required parameter Jira.rootUri is not configured in appsettings.json file", "Jira.rootUri");
            }
            
            if(!SmtpSection.Exists())
            {
                throw new ArgumentException("Required configuration section 'Smtp' is absent in appsettings.json file", "Smtp");
            }
        }

        public string LogoFileName => _configuration["Application:logoFileName"] ?? "logo.jpg";

        public string LocalRulesFileName => _configuration["Application:rulesFileName"] ?? "rules.xml";

        public string JiraRootUri  => _configuration["Jira:rootUri"];

        public string UserName => _configuration["Jira:userName"];

        public string Password => _configuration["Jira:password"];

        public string[] Supervisors => (_configuration["Application:supervisors"] ?? Empty).Split(',', StringSplitOptions.RemoveEmptyEntries);

        public string[] Maintainers => (_configuration["Application:maintenanceTeam"] ?? Empty).Split(',', StringSplitOptions.RemoveEmptyEntries);

        public int MaxResults => _configuration.GetValue<int?>("Jira:maxResults") ?? 50;

        public string SubjectPrefix => _configuration["Application:subjectPrefix"] ?? Empty;

        public IConfigurationSection SmtpSection => _configuration.GetSection("Smtp");

        public IConfigurationSection LoggerSection => _configuration.GetSection("Logger");
    }
}
