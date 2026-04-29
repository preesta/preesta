namespace JiraRest
{
    public interface IJiraGateway
    {
        dynamic GetIssuesFromJql(string query, int? maxResults, bool includeHistory = false);
        dynamic GetIssue(string issueKey);
        dynamic GetIssueWorklogs(string issueKey);
        dynamic GetIssueComments(string issueKey);
        dynamic GetBuilds(string projectCode);
        string[] GetIssuesInStructure(string structId);
        void HandleRequest(HttpRequest request);
    }
}
