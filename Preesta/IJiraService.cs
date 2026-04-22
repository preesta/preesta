using Preesta.Data;

namespace Preesta
{
    public interface IJiraService
    {
        Issue[] GetIssuesForJql(string query);
        
        string[] GetIssuesInStructure(string structId);

        Issue GetIssueById(string issueId);

        Attachment[] GetIssueAttachments(string issueKey);

        Build[] GetBuilds(string projectCode);
    }
}