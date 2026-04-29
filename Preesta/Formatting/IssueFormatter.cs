using System.Collections.Generic;
using System.Linq;
using System.Text;
using Preesta.Data;
using Preesta.Data.Supplying;

namespace Preesta.Formatting
{
    internal static class IssueFormatter
    {
        private static readonly string[] DefaultColumns =
            { "Type", "Key", "Summary", "Assignee", "Status", "Priority" };

        public static string ToHtml(IEnumerable<Package<SendsNotification, Issue>> packages, string rootUri)
        {
            var sb = new StringBuilder();
            sb.Append("<div style=\"max-width:640px;margin:0 auto;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif\">\n");

            var packageList = packages.ToArray();
            for (int i = 0; i < packageList.Length; i++)
            {
                var package = packageList[i];
                var columns = (package.Reaction.Columns?.Length > 0
                                  ? package.Reaction.Columns
                                  : DefaultColumns)
                    .Where(IsKnownColumn).ToArray();

                var hasRecom = !string.IsNullOrEmpty(package.Reaction.Recommendations);
                var hasJql = package.Properties.ContainsKey("Jql");
                var topMargin = i == 0 ? "0" : "48px";
                string? jqlUri = hasJql ? BuildJqlUri(rootUri, package.Properties["Jql"]?.ToString() ?? "") : null;

                if (hasRecom && hasJql)
                {
                    sb.Append($"<div style=\"border-left:3px solid #0052CC;background:#F4F5F7;margin:{topMargin} 0 12px;border-radius:0 4px 4px 0\">\n");
                    sb.Append("  <table style=\"width:100%;border-collapse:collapse\">\n");
                    sb.Append("    <tr>\n");
                    sb.Append($"      <td style=\"padding:10px 0 10px 14px;color:#172B4D;vertical-align:top\">{package.Reaction.Recommendations}</td>\n");
                    sb.Append($"      <td style=\"padding:10px 14px 10px 16px;text-align:right;white-space:nowrap;vertical-align:top\"><a href=\"{jqlUri}\" style=\"color:#0052CC;text-decoration:none\">Open in Jira →</a></td>\n");
                    sb.Append("    </tr>\n");
                    sb.Append("  </table>\n");
                    sb.Append("</div>\n");
                }
                else if (hasRecom)
                {
                    sb.Append($"<div style=\"border-left:3px solid #0052CC;background:#F4F5F7;padding:10px 14px;margin:{topMargin} 0 12px;color:#172B4D;border-radius:0 4px 4px 0\">{package.Reaction.Recommendations}</div>\n");
                }
                else if (hasJql)
                {
                    sb.Append($"<p style=\"text-align:right;margin:{topMargin} 0 8px;font-size:12px\"><a href=\"{jqlUri}\" style=\"color:#0052CC;text-decoration:none\">Open in Jira →</a></p>\n");
                }
                else if (i > 0)
                {
                    sb.Append("<div style=\"margin-top:48px\"></div>\n");
                }

                sb.Append("<div style=\"overflow-x:auto\">\n");
                sb.Append("<table style=\"width:100%;border-collapse:collapse;font-size:13px;color:#172B4D\">\n");
                sb.Append("<tr style=\"background:#F4F5F7;font-weight:600;text-align:left\">\n");
                foreach (var col in columns)
                    sb.Append($"  <th style=\"padding:10px 8px;border-bottom:2px solid #DFE1E6\">{col}</th>\n");
                sb.Append("</tr>\n");

                foreach (var issue in package.Items)
                {
                    sb.Append("<tr>\n");
                    foreach (var col in columns)
                        sb.Append($"  <td style=\"padding:8px;border-bottom:1px solid #EBECF0;vertical-align:top\">{RenderHtmlCell(col, issue, rootUri)}</td>\n");
                    sb.Append("</tr>\n");
                }
                sb.Append("</table>\n</div>\n");
            }

            sb.Append("<p style=\"color:#97A0AF;font-size:11px;margin-top:24px;text-align:center\">Sent by Preesta</p>\n");
            sb.Append("</div>\n");
            return sb.ToString();
        }

        public static string ToText(IEnumerable<Package<SendsNotification, Issue>> packages, string rootUri)
        {
            var sb = new StringBuilder();
            var packageList = packages.ToArray();
            for (int i = 0; i < packageList.Length; i++)
            {
                var package = packageList[i];
                if (i > 0)
                    sb.AppendLine("———");

                if (!string.IsNullOrEmpty(package.Reaction.Recommendations))
                    sb.AppendLine($"<i>{package.Reaction.Recommendations}</i>");

                if (package.Properties.ContainsKey("Jql"))
                    sb.AppendLine($"<a href=\"{BuildJqlUri(rootUri, package.Properties["Jql"]?.ToString() ?? "")}\">Open in Jira</a>");

                sb.AppendLine();
                foreach (var issue in package.Items)
                {
                    var browseUri = new JiraRest.UriBuilder().SetRoot(rootUri).AddRelativePath($"browse/{issue.Key ?? ""}").Build();
                    var assignee = (issue.Staff.Assignee ?? new User { DisplayName = "UNASSIGNED" }).DisplayName ?? "";
                    sb.AppendLine($"{PriorityIcon(issue.Priority)} <a href=\"{browseUri}\">{issue.Key}</a> — {issue.Summary}");
                    sb.AppendLine($"  {issue.Type ?? ""} · {issue.Status ?? ""} · {assignee}");
                    if (issue.DueDate != null)
                        sb.AppendLine($"  Due: {issue.DueDate.Value:dd.MM.yyyy}");
                    sb.AppendLine();
                }
            }
            return sb.ToString().TrimEnd();
        }

        private static string PriorityIcon(string? priority) => priority switch
        {
            "Highest" => "🔴",
            "High" => "🟠",
            "Medium" => "🟡",
            "Low" => "🟢",
            "Lowest" => "⚪",
            _ => "⚫"
        };

        private static string BuildJqlUri(string rootUri, string jql) =>
            new JiraRest.UriBuilder()
                .SetRoot(rootUri)
                .AddRelativePath("issues/")
                .AddParam("jql", jql, true)
                .Build()
                .ToString()
                .Replace("\"", "%22");

        private static bool IsKnownColumn(string column) => column switch
        {
            "Type" or "Key" or "Summary" or "Assignee" or "Reporter"
                or "Status" or "Priority" or "Components" or "Labels"
                or "Time Spent (hrs)" or "Build Found" or "Build Fixed"
                or "Due Date" or "Created" => true,
            _ => false
        };

        private static string RenderHtmlCell(string column, Issue issue, string rootUri)
        {
            switch (column)
            {
                case "Type":
                    return issue.Type ?? "";
                case "Key":
                    var browseUri = new JiraRest.UriBuilder().SetRoot(rootUri).AddRelativePath($"browse/{issue.Key ?? ""}").Build();
                    return $"<a href=\"{browseUri}\" style=\"color:#0052CC;text-decoration:none;font-weight:600\">{issue.Key ?? ""}</a>";
                case "Summary":
                    return issue.Summary ?? "";
                case "Assignee":
                    return (issue.Staff.Assignee ?? new User { DisplayName = "UNASSIGNED" }).DisplayName ?? "";
                case "Reporter":
                    return (issue.Staff.Reporter ?? new User { DisplayName = "UNKNOWN" }).DisplayName ?? "";
                case "Status":
                    return issue.Status ?? "";
                case "Priority":
                    return string.IsNullOrEmpty(issue.Priority)
                        ? ""
                        : $"{PriorityIcon(issue.Priority)} {issue.Priority}";
                case "Components":
                    return issue.Components ?? "";
                case "Labels":
                    return issue.Labels ?? "";
                case "Time Spent (hrs)":
                    return issue.TimeSpent.TotalHours.ToString();
                case "Build Found":
                    return string.Join(", ", issue.BuildFound ?? System.Array.Empty<string>());
                case "Build Fixed":
                    return string.Join(", ", issue.BuildFixed ?? System.Array.Empty<string>());
                case "Due Date":
                    return issue.DueDate != null ? issue.DueDate.Value.ToString("dd.MM.yyyy") : "";
                case "Created":
                    return issue.CreatedDate.ToString("dd.MM.yyyy");
                default:
                    return "";
            }
        }
    }
}
