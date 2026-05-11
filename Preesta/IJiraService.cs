using System.Collections.Generic;
using Preesta.Data;

namespace Preesta
{
    public interface IJiraService
    {
        Issue[] GetIssuesForJql(string query);

        Issue GetIssueById(string issueId);

        Attachment[] GetIssueAttachments(string issueKey);

        Release[] GetReleases(string projectCode);

        /// <summary>
        /// Discovers the workspace's custom-field display names and returns a
        /// case-insensitive map from <c>name</c> to internal id
        /// (e.g. <c>"Severity" → "customfield_10001"</c>). Empty map on failure.
        /// </summary>
        IReadOnlyDictionary<string, string> GetCustomFieldMap();
    }
}