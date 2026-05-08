namespace JiraRest
{
    public interface IJiraGateway
    {
        dynamic GetIssuesFromJql(string query, int? maxResults, bool includeHistory = false);
        dynamic GetIssue(string issueKey);
        dynamic GetIssueWorklogs(string issueKey);
        dynamic GetIssueComments(string issueKey);
        dynamic GetReleases(string projectCode);
        void HandleRequest(HttpRequest request);
    }
}
