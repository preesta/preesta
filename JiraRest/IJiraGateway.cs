namespace JiraRest
{
    public interface IJiraGateway
    {
        dynamic GetIssuesFromJql(string query, int? maxResults, bool includeHistory = false);
        dynamic GetIssue(string issueKey);
        dynamic GetIssueWorklogs(string issueKey);
        dynamic GetIssueComments(string issueKey);
        dynamic GetReleases(string projectCode);
        /// <summary>
        /// Returns the full field metadata list from Jira (<c>GET /rest/api/?/field</c>).
        /// Used to discover custom-field display names → internal ids without manual config.
        /// </summary>
        dynamic GetFields();
        void HandleRequest(HttpRequest request);
    }
}
