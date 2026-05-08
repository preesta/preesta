using Preesta.Data;

namespace Preesta
{
    public interface IJiraService
    {
        Issue[] GetIssuesForJql(string query);

        Issue GetIssueById(string issueId);

        Attachment[] GetIssueAttachments(string issueKey);

        Release[] GetReleases(string projectCode);
    }
}