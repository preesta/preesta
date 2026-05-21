using System;
using Microsoft.Extensions.Configuration;

namespace Preesta.AppConfig
{
    /// <summary>
    /// Adapter from <see cref="IConfigurationSection"/> to
    /// <see cref="JiraConfig"/>. Returns <c>null</c> when the <c>Jira:</c>
    /// section is absent — Jira is one of five equal Sources and a deployment
    /// targeting only Linear / GitHub / GitLab / Shortcut needs no Jira block
    /// at all (see <c>docs/concepts/architecture.md</c>).
    /// </summary>
    internal static class JiraConfigLoader
    {
        public static JiraConfig? Load(IConfigurationSection section)
        {
            var rootUri = NullIfEmpty(section["rootUri"]);
            if (rootUri == null) return null;

            var apiToken = NullIfEmpty(section["apiToken"]);
            var userName = NullIfEmpty(section["userName"]);
            var password = NullIfEmpty(section["password"]);

            var hasApiToken = apiToken != null;
            var hasBasicPair = userName != null && password != null;
            if (!hasApiToken && !hasBasicPair)
                throw new InvalidOperationException(
                    "Jira credentials missing: set Jira:apiToken, or both Jira:userName and Jira:password.");

            var maxResults = section.GetValue<int?>("maxResults") ?? 50;
            return new JiraConfig(rootUri, apiToken, userName, password, maxResults);
        }

        private static string? NullIfEmpty(string? raw) =>
            string.IsNullOrEmpty(raw) ? null : raw;
    }
}
