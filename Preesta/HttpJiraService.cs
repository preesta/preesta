using System;
using System.Collections.Generic;
using System.Net.Http;
using JiraRest;
using Preesta.Data;
using System.Linq;
using Preesta.Data.Convert;
using Serilog;

namespace Preesta
{
    public class HttpJiraService : IJiraService, IHttpHandler
    {
        public int MaxIssueCount { get; set; } = 50;
        internal IJiraGateway Connection { get; set; }
        internal ILogger? Logger { get; set; }

        public HttpJiraService(string rootUri, string user, string password, int maxIssueCount = 50, HttpClient? httpClient = null, ILogger? logger = null)
        {
            Connection = IsCloudUri(rootUri)
                ? (IJiraGateway)new CloudConnection(rootUri, user, password, httpClient)
                : new Connection(rootUri, user, password, httpClient);
            MaxIssueCount = maxIssueCount;
            Logger = logger;
        }

        public HttpJiraService(string rootUri, string bearerToken, int maxIssueCount = 50, HttpClient? httpClient = null, ILogger? logger = null)
        {
            Connection = new Connection(rootUri, bearerToken, httpClient);
            MaxIssueCount = maxIssueCount;
            Logger = logger;
        }

        private static bool IsCloudUri(string rootUri)
        {
            return rootUri.Contains("atlassian.net", StringComparison.OrdinalIgnoreCase);
        }

        public virtual Issue[] GetIssuesForJql(string query)
        {
            return CallFuncInConnectionContext(
                jira =>
                ((IEnumerable<dynamic>)
                 jira
                     .GetIssuesFromJql(query, MaxIssueCount == 0 ? null : (int?)MaxIssueCount)
                     .issues)
                    .Select(JToken.ToIssue)
                    .ToArray()
                );
        }

        public virtual Issue GetIssueById(string issueId)
        {
            return CallFuncInConnectionContext(jira => JToken.ToIssue(jira.GetIssue(issueId)));
        }

        public virtual Attachment[] GetIssueAttachments(string issueKey)
        {
            return CallFuncInConnectionContext(
                jira =>
                ((IEnumerable<dynamic>)
                 jira
                     .GetIssue(issueKey)
                     .fields
                     .attachment)
                    .Select(JToken.ToAttachment)
                    .ToArray()
                );
        }

        public virtual Release[] GetReleases(string projectCode)
        {
            return CallFuncInConnectionContext(jira =>
                ((IEnumerable<dynamic>)
                    jira.GetReleases(projectCode))
                    .Select(JToken.ToRelease)
                    .Where(b => b != null)
                    .Cast<Release>()
                    .ToArray()
                );
        }

        /// <summary>
        /// Calls Jira's <c>/rest/api/?/field</c> once and builds a case-insensitive
        /// map from display name → internal id for fields whose id starts with
        /// <c>customfield_</c>. Duplicate display names log a warning and keep the
        /// first id encountered. Any failure (HTTP error, network) is swallowed —
        /// an empty map is returned so the rest of the pipeline keeps working.
        /// </summary>
        public virtual IReadOnlyDictionary<string, string> GetCustomFieldMap()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var fields = Connection.GetFields();
                foreach (var f in (IEnumerable<dynamic>)fields)
                {
                    string id = f.id;
                    string name = f.name;
                    if (string.IsNullOrEmpty(id) || !id.StartsWith("customfield_", StringComparison.Ordinal))
                        continue;
                    if (string.IsNullOrEmpty(name))
                        continue;
                    if (map.TryGetValue(name, out var existingId))
                    {
                        Logger?.Warning(
                            "Ambiguous custom field display name {Name} — multiple ids found ({First}, {Other}); keeping the first",
                            name, existingId, id);
                        continue;
                    }
                    map[name] = id;
                }
            }
            catch (Exception ex)
            {
                Logger?.Warning(ex,
                    "Failed to discover Jira custom fields via /rest/api/?/field — custom-field columns will be unavailable");
            }
            return map;
        }

        private T CallFuncInConnectionContext<T>(Func<IJiraGateway, T> func)
        {
            return func(Connection);
        }

        public virtual void HandleAll(IEnumerable<HttpRequest> requests)
        {
            foreach (var request in requests)
            {
                Connection.HandleRequest(request);
            }
        }
    }
}