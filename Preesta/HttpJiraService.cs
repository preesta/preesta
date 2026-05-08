using System;
using System.Collections.Generic;
using JiraRest;
using Preesta.Data;
using System.Linq;
using Preesta.Data.Convert;

namespace Preesta
{
    public class HttpJiraService : IJiraService, IHttpHandler
    {
        public int MaxIssueCount { get; set; } = 50;
        internal IJiraGateway Connection { get; set; }

        public HttpJiraService(string rootUri, string user, string password, int maxIssueCount = 50)
        {
            Connection = IsCloudUri(rootUri)
                ? (IJiraGateway)new CloudConnection(rootUri, user, password)
                : new Connection(rootUri, user, password);
            MaxIssueCount = maxIssueCount;
        }

        public HttpJiraService(string rootUri, string bearerToken, int maxIssueCount = 50)
        {
            Connection = new Connection(rootUri, bearerToken);
            MaxIssueCount = maxIssueCount;
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

        public virtual string[] GetIssuesInStructure(string structId)
        {
            return CallFuncInConnectionContext(jira => jira.GetIssuesInStructure(structId));
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