using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

        public static string ToHtml(
            IEnumerable<Package<NotificationReaction, Issue>> packages,
            string rootUri,
            string? linearWorkspace = null,
            IReadOnlyDictionary<string, string>? customFields = null) =>
            Render(HtmlTemplate.Value, BuildModel(packages, rootUri, linearWorkspace, customFields, htmlMode: true));

        public static string ToText(
            IEnumerable<Package<NotificationReaction, Issue>> packages,
            string rootUri,
            string? linearWorkspace = null,
            IReadOnlyDictionary<string, string>? customFields = null) =>
            Render(TextTemplate.Value, BuildModel(packages, rootUri, linearWorkspace, customFields, htmlMode: false));

        // Phase 10 (Slack): renders the digest as Slack mrkdwn — *bold*, _italic_,
        // <url|label> for links, :emoji: for status/priority. NOT plain Markdown:
        // single-asterisk bold, no double-bracket links. Inline StringBuilder rather
        // than a Scriban template — the format is short, fits one screen of code,
        // and the per-issue chip rendering needs Slack-specific emoji (the existing
        // RenderTextChip uses Unicode dots, which aren't what we want for Slack).
        public static string ToSlackMrkdwn(
            IEnumerable<Package<NotificationReaction, Issue>> packages,
            string rootUri,
            string? linearWorkspace = null,
            IReadOnlyDictionary<string, string>? customFields = null)
        {
            var cf = customFields ?? EmptyMap;
            var sb = new StringBuilder();
            var firstSection = true;
            foreach (var package in packages)
            {
                if (!firstSection) sb.AppendLine("———");
                firstSection = false;

                if (!string.IsNullOrEmpty(package.Reaction.Recommendations))
                    sb.AppendLine(package.Reaction.Recommendations);

                var filterDesc = LinearFilterDescriptionOrNull(package);
                if (!string.IsNullOrEmpty(filterDesc))
                    sb.AppendLine($"_{filterDesc}_");

                sb.AppendLine();

                var columns = ResolveColumns(package.Reaction.Columns, cf);

                foreach (var issue in package.Items)
                {
                    var browseUri = !string.IsNullOrEmpty(issue.Url)
                        ? issue.Url!
                        : new JiraRest.UriBuilder().SetRoot(rootUri).AddRelativePath($"browse/{issue.Key ?? ""}").Build().ToString();

                    var key = issue.Key ?? "";
                    var keyRendered = !string.IsNullOrEmpty(browseUri)
                        ? $"<{browseUri}|{key}>"
                        : key;

                    sb.Append('*').Append(keyRendered).Append("* ").AppendLine(issue.Summary ?? "");

                    var chips = RenderSlackChips(columns, issue, cf);
                    if (chips.Count > 0)
                        sb.Append("  ").AppendLine(string.Join(" · ", chips));
                }
            }
            return sb.ToString();
        }

        private static readonly IReadOnlyDictionary<string, string> EmptyMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Apply column resolution shared by all three render paths:
        //   - "all-non-empty" magic → expand to AllKnownColumns + custom-field display names
        //   - filter out header columns (Key/Summary) and unknown ones (not standard, not a custom field)
        //   - dedupe
        private static string[] ResolveColumns(
            string[]? columns,
            IReadOnlyDictionary<string, string> customFields)
        {
            var requested = columns?.Length > 0 ? columns : DefaultColumns;
            return requested
                .SelectMany(c => string.Equals(c, AllNonEmptyToken, StringComparison.OrdinalIgnoreCase)
                    ? AllKnownColumns.Concat(customFields.Keys)
                    : new[] { c })
                .Where(c => !IsHeaderColumn(c) && (IsKnownColumn(c) || customFields.ContainsKey(c)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string SlackStatusChip(string? status)
        {
            if (string.IsNullOrEmpty(status)) return "";
            var emoji = status.ToLowerInvariant() switch
            {
                "done" or "resolved" or "closed" or "verified" => ":white_check_mark:",
                "in progress" or "in review" => ":hourglass_flowing_sand:",
                "todo" or "to do" or "open" => ":black_square_button:",
                "backlog" => ":open_file_folder:",
                "cancelled" or "canceled" => ":x:",
                "blocked" => ":no_entry:",
                _ => ":grey_question:"
            };
            return $"{emoji} {status}";
        }

        private static string SlackPriorityChip(string? priority)
        {
            if (string.IsNullOrEmpty(priority)) return "";
            var emoji = priority switch
            {
                "Urgent" or "Highest" => ":red_circle:",
                "High" => ":large_orange_circle:",
                "Medium" => ":large_yellow_circle:",
                "Low" => ":large_green_circle:",
                _ => ""
            };
            return string.IsNullOrEmpty(emoji) ? "" : $"{emoji} {priority}";
        }

        private static List<string> RenderSlackChips(string[] columns, Issue issue, IReadOnlyDictionary<string, string> customFields)
        {
            var chips = new List<string>();
            foreach (var col in columns)
            {
                switch (col)
                {
                    case "Status":
                        var s = SlackStatusChip(issue.Status);
                        if (!string.IsNullOrEmpty(s)) chips.Add(s);
                        break;
                    case "Priority":
                        var p = SlackPriorityChip(issue.Priority);
                        if (!string.IsNullOrEmpty(p)) chips.Add(p);
                        break;
                    case "Assignee":
                        chips.Add((issue.Participants.Assignee ?? new User { DisplayName = "UNASSIGNED" }).DisplayName ?? "");
                        break;
                    case "Reporter":
                        chips.Add($"Reported by {(issue.Participants.Reporter ?? new User { DisplayName = "UNKNOWN" }).DisplayName}");
                        break;
                    case "Type":
                        if (!string.IsNullOrEmpty(issue.Type)) chips.Add(issue.Type!);
                        break;
                    case "Components":
                        if (!string.IsNullOrEmpty(issue.Components)) chips.Add(issue.Components!);
                        break;
                    case "Labels":
                        if (!string.IsNullOrEmpty(issue.Labels)) chips.Add(issue.Labels);
                        break;
                    case "Time Spent (hrs)":
                        if (issue.TimeSpent.TotalHours > 0)
                            chips.Add($"{issue.TimeSpent.TotalHours:0.#}h spent");
                        break;
                    case "Affects Versions":
                        var av = string.Join(", ", issue.AffectsVersions ?? Array.Empty<string>());
                        if (!string.IsNullOrEmpty(av)) chips.Add($"Affects {av}");
                        break;
                    case "Fix Versions":
                        var fv = string.Join(", ", issue.FixVersions ?? Array.Empty<string>());
                        if (!string.IsNullOrEmpty(fv)) chips.Add($"Fix {fv}");
                        break;
                    case "Due Date":
                        if (issue.DueDate != null) chips.Add($"Due {issue.DueDate.Value:dd.MM.yyyy}");
                        break;
                    case "Created":
                        chips.Add($"Created {issue.CreatedDate:dd.MM.yyyy}");
                        break;
                    case "Updated":
                        if (issue.UpdatedDate != null) chips.Add($"Updated {issue.UpdatedDate.Value:dd.MM.yyyy}");
                        break;
                    case "Resolution":
                        if (!string.IsNullOrEmpty(issue.Resolution)) chips.Add($"Resolution: {issue.Resolution}");
                        break;
                    case "Project":
                        if (!string.IsNullOrEmpty(issue.ProjectKey)) chips.Add(issue.ProjectKey!);
                        break;
                    default:
                        // Custom field: resolve display name → internal id → JToken, stringify.
                        if (customFields.TryGetValue(col, out var cfId)
                            && issue.CustomFields.TryGetValue(cfId, out var token))
                        {
                            var cfStr = RenderCustomFieldValue(token);
                            if (!string.IsNullOrEmpty(cfStr)) chips.Add($"{col}: {cfStr}");
                        }
                        break;
                }
            }
            return chips;
        }

        private static DigestModel BuildModel(
            IEnumerable<Package<NotificationReaction, Issue>> packages,
            string rootUri,
            string? linearWorkspace,
            IReadOnlyDictionary<string, string>? customFields,
            bool htmlMode)
        {
            var cf = customFields ?? EmptyMap;
            var sections = packages.Select(package =>
            {
                var columns = ResolveColumns(package.Reaction.Columns, cf);

                var items = package.Items.Select(issue =>
                {
                    var chips = columns
                        .Select(col => htmlMode ? RenderHtmlChip(col, issue, cf) : RenderTextChip(col, issue, cf))
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
                    GithubSearchUri = GithubSearchUriOrNull(package),
                    GitlabSearchUri = PropertyOrNull(package, "GitlabSearchUri"),
                    ShortcutSearchUri = PropertyOrNull(package, "ShortcutSearchUri"),
                    FilterDescription = LinearFilterDescriptionOrNull(package),
                    Items = items
                };
            }).ToList();

            return new DigestModel { Sections = sections };
        }

        private const string PillBase = "display:inline-block;padding:2px 9px;border-radius:11px;font-weight:500;font-size:11px;line-height:1.4";
        private const string DotBase = "display:inline-block;width:8px;height:8px;border-radius:50%;vertical-align:middle;margin-right:5px";

        private static string RenderHtmlChip(string column, Issue issue, IReadOnlyDictionary<string, string> customFields)
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
                    // Custom field branch — resolve display name through the map.
                    if (customFields.TryGetValue(column, out var cfId)
                        && issue.CustomFields.TryGetValue(cfId, out var token))
                    {
                        var v = RenderCustomFieldValue(token);
                        return string.IsNullOrEmpty(v) ? "" : $"{E(column)}: {E(v)}";
                    }
                    return "";
            }
        }

        private static string RenderTextChip(string column, Issue issue, IReadOnlyDictionary<string, string> customFields)
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
                    if (customFields.TryGetValue(column, out var cfId)
                        && issue.CustomFields.TryGetValue(cfId, out var token))
                    {
                        var v = RenderCustomFieldValue(token);
                        return string.IsNullOrEmpty(v) ? "" : $"{column}: {v}";
                    }
                    return "";
            }
        }

        /// <summary>
        /// Renders a Jira custom-field JToken as a human-readable string.
        /// Handles common shapes:
        ///   JValue       → ToString()
        ///   JArray of strings → comma-join
        ///   JArray of {name|value} objects → comma-joined names
        ///   JObject with name/value/displayName → that string
        ///   Sprint legacy (JArray of base64-encoded GreenHopper strings) → returned as-is
        ///   Anything else → compact JSON for power users to debug.
        /// </summary>
        private static string RenderCustomFieldValue(JToken? token)
        {
            if (token == null || token.Type == JTokenType.Null) return "";

            switch (token.Type)
            {
                case JTokenType.String:
                case JTokenType.Integer:
                case JTokenType.Float:
                case JTokenType.Boolean:
                case JTokenType.Date:
                    return token.ToString();

                case JTokenType.Array:
                    var arr = (JArray)token;
                    if (arr.Count == 0) return "";
                    // First sniff: array of objects with name/value (multi-select, components-like).
                    if (arr.First!.Type == JTokenType.Object)
                    {
                        var names = arr
                            .OfType<JObject>()
                            .Select(o => (string?)(o["name"] ?? o["value"] ?? o["displayName"]))
                            .Where(s => !string.IsNullOrEmpty(s))
                            .ToArray();
                        return names.Length > 0
                            ? string.Join(", ", names)
                            : arr.ToString(Newtonsoft.Json.Formatting.None);
                    }
                    // Array of scalars (strings, numbers) — or the Sprint legacy case where
                    // each element is a base64-encoded GreenHopper string; render as-is.
                    return string.Join(", ", arr.Select(t => t.ToString()));

                case JTokenType.Object:
                    var obj = (JObject)token;
                    var single = (string?)(obj["name"] ?? obj["value"] ?? obj["displayName"]);
                    return !string.IsNullOrEmpty(single)
                        ? single!
                        : obj.ToString(Newtonsoft.Json.Formatting.None);

                default:
                    return token.ToString(Newtonsoft.Json.Formatting.None);
            }
        }

        private static (string Bg, string Fg) StatusColors(string status) => status?.ToLowerInvariant() switch
        {
            "done" or "resolved" or "closed" or "verified" => ("#E3FCEF", "#006644"),
            "in progress" or "in review" => ("#DEEBFF", "#0747A6"),
            "blocked" => ("#FFEBE6", "#BF2600"),
            _ => ("#DFE1E6", "#42526E")
        };

        // Jira and Linear use different priority label sets:
        //   Jira:   Lowest / Low / Medium / High / Highest
        //   Linear: None  / Low / Medium / High / Urgent
        // We map them onto the same colour ladder so the digest stays consistent.
        private static string PriorityColor(string? priority) => priority switch
        {
            "Urgent" or "Highest" => "#DE350B",
            "High" => "#FF5630",
            "Medium" => "#FFAB00",
            "Low" => "#36B37E",
            "Lowest" => "#57D9A3",
            _ => "#97A0AF"
        };

        private static string PriorityIcon(string? priority) => priority switch
        {
            "Urgent" or "Highest" => "🔴",
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

        // GitHub: build https://github.com/search?q=<filter>&type=issues so the digest
        // includes a one-click round-trip back to the same query.
        private static string? GithubSearchUriOrNull(Package<NotificationReaction, Issue> package)
        {
            if (!package.Properties.TryGetValue("GithubFilter", out var filterObj)) return null;
            var filter = filterObj?.ToString();
            if (string.IsNullOrEmpty(filter)) return null;
            return $"https://github.com/search?q={System.Uri.EscapeDataString(filter)}&type=issues";
        }

        // GitLab / Shortcut: the supplier builds the full deep-link in Enrich
        // (it has the workspace slug or filter-to-querystring logic, which the
        // formatter shouldn't), so we just forward the cached value here.
        private static string? PropertyOrNull(Package<NotificationReaction, Issue> package, string key)
        {
            if (!package.Properties.TryGetValue(key, out var v)) return null;
            var s = v?.ToString();
            return string.IsNullOrEmpty(s) ? null : s;
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
            // GitHub: raw search string — already human-readable, show verbatim.
            if (package.Properties.TryGetValue("GithubFilter", out var ghFilter))
            {
                var s = ghFilter?.ToString();
                if (!string.IsNullOrEmpty(s))
                    return $"Search: {s}";
            }
            // GitLab: pre-stringified chip list ("state=opened  label=urgent  …").
            if (package.Properties.TryGetValue("GitlabFilter", out var glFilter))
            {
                var s = glFilter?.ToString();
                if (!string.IsNullOrEmpty(s))
                    return $"Filter: {s}";
            }
            return null;
        }

        private static bool IsHeaderColumn(string column) =>
            column is "Key" or "Summary";

        // Standard columns we know how to render. Custom fields are NOT in this set —
        // they're handled separately via the per-call customFields map.
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
