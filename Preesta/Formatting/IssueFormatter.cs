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

        public static string ToHtml(IEnumerable<Package<NotificationReaction, Issue>> packages, string rootUri, string? linearWorkspace = null) =>
            Render(HtmlTemplate.Value, BuildModel(packages, rootUri, linearWorkspace, htmlMode: true));

        public static string ToText(IEnumerable<Package<NotificationReaction, Issue>> packages, string rootUri, string? linearWorkspace = null) =>
            Render(TextTemplate.Value, BuildModel(packages, rootUri, linearWorkspace, htmlMode: false));

        private static DigestModel BuildModel(IEnumerable<Package<NotificationReaction, Issue>> packages, string rootUri, string? linearWorkspace, bool htmlMode)
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
                    // Prefer the canonical URL from the source (e.g. Linear) over the
                    // reconstructed Jira-style /browse/{key} fallback.
                    var browseUri = !string.IsNullOrEmpty(issue.Url)
                        ? issue.Url!
                        : new JiraRest.UriBuilder().SetRoot(rootUri).AddRelativePath($"browse/{issue.Key ?? ""}").Build().ToString();
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
                    LinearViewUri = LinearViewUriOrNull(package, linearWorkspace),
                    FilterDescription = LinearFilterDescriptionOrNull(package),
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
                    return E((issue.Participants.Assignee ?? new User { DisplayName = "UNASSIGNED" }).DisplayName);
                case "Reporter":
                    return $"Reported by {E((issue.Participants.Reporter ?? new User { DisplayName = "UNKNOWN" }).DisplayName)}";
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
                    return (issue.Participants.Assignee ?? new User { DisplayName = "UNASSIGNED" }).DisplayName ?? "";
                case "Reporter":
                    return $"Reported by {(issue.Participants.Reporter ?? new User { DisplayName = "UNKNOWN" }).DisplayName}";
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

        private static string? JqlUriOrNull(Package<NotificationReaction, Issue> package, string rootUri)
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

        // Linear (Phase 12.2): only viewId-mode rules have a canonical shareable URL —
        // the AI-prompt and raw-filter modes don't, because Linear stores filter state
        // in localStorage. We deliberately return null for those modes; the template
        // skips the "Open in Linear" link entirely (no fallback to "My Issues").
        private static string? LinearViewUriOrNull(Package<NotificationReaction, Issue> package, string? linearWorkspace)
        {
            if (string.IsNullOrEmpty(linearWorkspace)) return null;
            if (!package.Properties.TryGetValue("LinearViewId", out var viewIdObj)) return null;
            var viewId = viewIdObj?.ToString();
            if (string.IsNullOrEmpty(viewId)) return null;
            return $"https://linear.app/{linearWorkspace}/view/{viewId}";
        }

        // Linear (Phase 12.2): renders a one-line, human-readable description of what
        // produced this list — shown under the recommendations in the digest header.
        // Null when no Linear filter property is set (Jira sections, mostly).
        private static string? LinearFilterDescriptionOrNull(Package<NotificationReaction, Issue> package)
        {
            if (package.Properties.TryGetValue("LinearFilter", out var filter))
            {
                var s = filter?.ToString();
                if (!string.IsNullOrEmpty(s))
                    return $"AI filter: «{s}»";
            }
            // filterRaw is intentionally NOT shown in the digest header — it's a
            // power-user escape hatch (the user wrote the GraphQL filter object
            // themselves in rules.yaml, so re-displaying it here just clutters the
            // notification with non-actionable JSON). AI prompt and viewId are still
            // shown because they are human-readable / clickable.
            if (package.Properties.TryGetValue("LinearViewName", out var name))
            {
                var s = name?.ToString();
                if (!string.IsNullOrEmpty(s))
                    return $"View: {s}";
            }
            // Fallback if Enrich somehow set LinearViewId without a name (e.g. the
            // GraphQL fetch failed before populating the cache).
            if (package.Properties.TryGetValue("LinearViewId", out var id))
            {
                var s = id?.ToString();
                if (!string.IsNullOrEmpty(s))
                    return $"View: {s}";
            }
            return null;
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
