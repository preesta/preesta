using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Preesta.Data;
using Preesta.Data.Supplying;
using Scriban;
using Scriban.Runtime;

namespace Preesta.Formatting
{
    internal static class IssueFormatter
    {
        private static readonly string[] DefaultColumns = { "Status", "Priority", "Assignee" };

        private static readonly string[] AllKnownColumns =
        {
            "Project", "Type", "Status", "Priority", "Resolution",
            "Assignee", "Reporter",
            "Components", "Labels",
            "Affects Versions", "Fix Versions",
            "Time Spent (hrs)", "Due Date", "Created", "Updated"
        };

        private const string AllNonEmptyToken = "all-non-empty";

        private static readonly Lazy<Template> HtmlTemplate =
            new(() => LoadTemplate("IssueDigest.scriban-html"));

        private static readonly Lazy<Template> TextTemplate =
            new(() => LoadTemplate("IssueDigest.scriban-text"));

        public static string ToHtml(IEnumerable<Package<SendsNotification, Issue>> packages, string rootUri) =>
            Render(HtmlTemplate.Value, BuildModel(packages, rootUri, htmlMode: true));

        public static string ToText(IEnumerable<Package<SendsNotification, Issue>> packages, string rootUri) =>
            Render(TextTemplate.Value, BuildModel(packages, rootUri, htmlMode: false));

        private static DigestModel BuildModel(IEnumerable<Package<SendsNotification, Issue>> packages, string rootUri, bool htmlMode)
        {
            var sections = packages.Select(package =>
            {
                var columns = (package.Reaction.Columns?.Length > 0 ? package.Reaction.Columns : DefaultColumns)
                    .SelectMany(c => string.Equals(c, AllNonEmptyToken, System.StringComparison.OrdinalIgnoreCase)
                        ? AllKnownColumns
                        : new[] { c })
                    .Where(c => !IsHeaderColumn(c) && IsKnownColumn(c))
                    .Distinct()
                    .ToArray();

                var items = package.Items.Select(issue =>
                {
                    var chips = columns
                        .Select(col => htmlMode ? RenderHtmlChip(col, issue) : RenderTextChip(col, issue))
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToList();
                    var browseUri = new JiraRest.UriBuilder().SetRoot(rootUri).AddRelativePath($"browse/{issue.Key ?? ""}").Build().ToString();
                    return new DigestItem
                    {
                        Key = issue.Key ?? "",
                        Summary = issue.Summary ?? "",
                        BrowseUri = browseUri,
                        MetaChips = chips,
                        MetaText = string.Join(" · ", chips)
                    };
                }).ToList();

                return new DigestSection
                {
                    Recommendations = package.Reaction.Recommendations,
                    JqlUri = JqlUriOrNull(package, rootUri),
                    Items = items
                };
            }).ToList();

            return new DigestModel { Sections = sections };
        }

        private const string PillBase = "display:inline-block;padding:2px 9px;border-radius:11px;font-weight:500;font-size:11px;line-height:1.4";
        private const string DotBase = "display:inline-block;width:8px;height:8px;border-radius:50%;vertical-align:middle;margin-right:5px";

        private static string RenderHtmlChip(string column, Issue issue)
        {
            string E(string? s) => WebUtility.HtmlEncode(s ?? "");
            switch (column)
            {
                case "Status":
                    if (string.IsNullOrEmpty(issue.Status)) return "";
                    var (bg, fg) = StatusColors(issue.Status);
                    return $"<span style=\"{PillBase};background:{bg};color:{fg}\">{E(issue.Status)}</span>";
                case "Priority":
                    if (string.IsNullOrEmpty(issue.Priority)) return "";
                    return $"<span style=\"{DotBase};background:{PriorityColor(issue.Priority)}\"></span>{E(issue.Priority)}";
                case "Assignee":
                    return E((issue.Staff.Assignee ?? new User { DisplayName = "UNASSIGNED" }).DisplayName);
                case "Reporter":
                    return $"Reported by {E((issue.Staff.Reporter ?? new User { DisplayName = "UNKNOWN" }).DisplayName)}";
                case "Type":
                    return string.IsNullOrEmpty(issue.Type) ? "" : $"<span style=\"{PillBase};background:#F4F5F7;color:#42526E\">{E(issue.Type)}</span>";
                case "Components":
                    return E(issue.Components);
                case "Labels":
                    return E(issue.Labels);
                case "Time Spent (hrs)":
                    return issue.TimeSpent.TotalHours > 0 ? $"{issue.TimeSpent.TotalHours:0.#}h spent" : "";
                case "Affects Versions":
                    var av = string.Join(", ", issue.AffectsVersions ?? Array.Empty<string>());
                    return string.IsNullOrEmpty(av) ? "" : $"Affects {E(av)}";
                case "Fix Versions":
                    var fv = string.Join(", ", issue.FixVersions ?? Array.Empty<string>());
                    return string.IsNullOrEmpty(fv) ? "" : $"Fix {E(fv)}";
                case "Due Date":
                    return issue.DueDate != null ? $"Due {issue.DueDate.Value:dd.MM.yyyy}" : "";
                case "Created":
                    return $"Created {issue.CreatedDate:dd.MM.yyyy}";
                case "Updated":
                    return issue.UpdatedDate != null ? $"Updated {issue.UpdatedDate.Value:dd.MM.yyyy}" : "";
                case "Resolution":
                    return string.IsNullOrEmpty(issue.Resolution) ? "" : $"Resolution: {E(issue.Resolution)}";
                case "Project":
                    return string.IsNullOrEmpty(issue.ProjectKey) ? "" : E(issue.ProjectKey);
                default:
                    return "";
            }
        }

        private static string RenderTextChip(string column, Issue issue)
        {
            switch (column)
            {
                case "Status":
                    return string.IsNullOrEmpty(issue.Status) ? "" : $"[{issue.Status}]";
                case "Priority":
                    return string.IsNullOrEmpty(issue.Priority) ? "" : $"{PriorityIcon(issue.Priority)} {issue.Priority}";
                case "Assignee":
                    return (issue.Staff.Assignee ?? new User { DisplayName = "UNASSIGNED" }).DisplayName ?? "";
                case "Reporter":
                    return $"Reported by {(issue.Staff.Reporter ?? new User { DisplayName = "UNKNOWN" }).DisplayName}";
                case "Type":
                    return string.IsNullOrEmpty(issue.Type) ? "" : $"[{issue.Type}]";
                case "Components":
                    return issue.Components ?? "";
                case "Labels":
                    return issue.Labels ?? "";
                case "Time Spent (hrs)":
                    return issue.TimeSpent.TotalHours > 0 ? $"{issue.TimeSpent.TotalHours:0.#}h spent" : "";
                case "Affects Versions":
                    var av = string.Join(", ", issue.AffectsVersions ?? Array.Empty<string>());
                    return string.IsNullOrEmpty(av) ? "" : $"Affects {av}";
                case "Fix Versions":
                    var fv = string.Join(", ", issue.FixVersions ?? Array.Empty<string>());
                    return string.IsNullOrEmpty(fv) ? "" : $"Fix {fv}";
                case "Due Date":
                    return issue.DueDate != null ? $"Due {issue.DueDate.Value:dd.MM.yyyy}" : "";
                case "Created":
                    return $"Created {issue.CreatedDate:dd.MM.yyyy}";
                case "Updated":
                    return issue.UpdatedDate != null ? $"Updated {issue.UpdatedDate.Value:dd.MM.yyyy}" : "";
                case "Resolution":
                    return string.IsNullOrEmpty(issue.Resolution) ? "" : $"Resolution: {issue.Resolution}";
                case "Project":
                    return issue.ProjectKey ?? "";
                default:
                    return "";
            }
        }

        private static (string Bg, string Fg) StatusColors(string status) => status?.ToLowerInvariant() switch
        {
            "done" or "resolved" or "closed" or "verified" => ("#E3FCEF", "#006644"),
            "in progress" or "in review" => ("#DEEBFF", "#0747A6"),
            "blocked" => ("#FFEBE6", "#BF2600"),
            _ => ("#DFE1E6", "#42526E")
        };

        private static string PriorityColor(string? priority) => priority switch
        {
            "Highest" => "#DE350B",
            "High" => "#FF5630",
            "Medium" => "#FFAB00",
            "Low" => "#36B37E",
            "Lowest" => "#57D9A3",
            _ => "#97A0AF"
        };

        private static string PriorityIcon(string? priority) => priority switch
        {
            "Highest" => "🔴",
            "High" => "🟠",
            "Medium" => "🟡",
            "Low" => "🟢",
            "Lowest" => "⚪",
            _ => "⚫"
        };

        private static string? JqlUriOrNull(Package<SendsNotification, Issue> package, string rootUri)
        {
            if (!package.Properties.ContainsKey("Jql"))
                return null;
            return new JiraRest.UriBuilder()
                .SetRoot(rootUri)
                .AddRelativePath("issues/")
                .AddParam("jql", package.Properties["Jql"]?.ToString() ?? "", true)
                .Build()
                .ToString()
                .Replace("\"", "%22");
        }

        private static bool IsHeaderColumn(string column) =>
            column is "Key" or "Summary";

        private static bool IsKnownColumn(string column) => column switch
        {
            "Type" or "Assignee" or "Reporter" or "Status" or "Priority"
                or "Components" or "Labels" or "Time Spent (hrs)"
                or "Affects Versions" or "Fix Versions"
                or "Due Date" or "Created" or "Updated"
                or "Resolution" or "Project" => true,
            _ => false
        };

        private static Template LoadTemplate(string name)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Templates", name);
            return Template.Parse(File.ReadAllText(path));
        }

        private static string Render(Template template, object model)
        {
            var ctx = new TemplateContext();
            var script = new ScriptObject();
            script.Import(model, renamer: m => StandardMemberRenamer.Default(m));
            ctx.PushGlobal(script);
            return template.Render(ctx);
        }
    }
}
