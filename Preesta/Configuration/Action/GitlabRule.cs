using System;

namespace Preesta.Configuration.Action
{
    /// <summary>
    /// Wires <see cref="GitlabIssueSource"/> into the notification pipeline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// GitLab — unlike GitHub — does not expose a single human-readable search-string
    /// language for issues. Instead the web UI lets users build a query by stacking
    /// filter chips (Assignee, Author, Label, Milestone, State, ...). We mirror that
    /// UI taxonomy directly into YAML: each chip becomes a named field on
    /// <see cref="GitlabFilter"/>, e.g.
    /// <code>
    /// filter:
    ///   state: opened
    ///   labelName: [urgent, blocker]
    ///   assigneeUsernames: [alice, bob]
    ///   search: "checkout flow"
    /// </code>
    /// </para>
    /// <para>
    /// Internally the filter object is forwarded to GitLab's GraphQL <c>Query.issues</c>
    /// arguments — the same names the GraphQL schema uses. This keeps the surface
    /// human-readable while making the mapping trivial (no DSL translation).
    /// </para>
    /// <para>
    /// Rules are deliberately obezlichennye (impersonal): no <c>assignee = currentUser()</c>
    /// equivalent in the filter. Per-recipient routing happens later in the notification
    /// step via the standard <c>assignee</c> / <c>reporter</c> markers in <c>mailTo</c>.
    /// </para>
    /// <para>
    /// At least one of {<see cref="GitlabFilter.State"/>, <see cref="GitlabFilter.LabelName"/>,
    /// <see cref="GitlabFilter.AssigneeUsernames"/>, <see cref="GitlabFilter.AuthorUsername"/>,
    /// <see cref="GitlabFilter.MilestoneTitle"/>, <see cref="GitlabFilter.Search"/>,
    /// <see cref="GitlabFilter.Iids"/>} must be set — GitLab's GraphQL <c>issues</c> query
    /// refuses to scan the entire database without at least one filter. The YAML parser
    /// enforces this and drops empty rules with an Error log.
    /// </para>
    /// </remarks>
    public class GitlabRule : Rule
    {
        /// <summary>Structured filter that GitLab's web UI builds chip-by-chip. Required.</summary>
        public GitlabFilter Filter { get; set; } = new GitlabFilter();

        /// <summary>
        /// Raw GraphQL mutations to execute for each matched issue. Power-user hook —
        /// the rule author writes the full <c>mutation { ... }</c> body and places markers
        /// (<c>{{@issueId}}</c>, <c>{{@assignee.email}}</c>, etc.) where Preesta should
        /// substitute issue context. No DSL, no ID resolution — bring your own
        /// state/user/label IDs. GitLab's GraphQL IDs are global IDs (gid://) returned in
        /// each Issue's <c>id</c> field; <c>{{@issueId}}</c> resolves to that string.
        /// </summary>
        public GraphQLMutationSpec[] GraphQLMutations { get; set; } = new GraphQLMutationSpec[] { };
    }

    /// <summary>
    /// Mirrors the filter chips users see in GitLab's web UI. Field names match the
    /// GraphQL <c>Query.issues</c> argument names exactly so the source can forward
    /// the object straight through as <c>$variables</c> with no name remapping.
    /// </summary>
    /// <remarks>
    /// Empty / null fields are omitted from the request — only set what you want to
    /// filter on. Power users wanting a chip not exposed here can lobby for a new
    /// field rather than reaching for a raw escape hatch; GitLab's filter taxonomy is
    /// small enough that "add a property" stays cheap.
    /// </remarks>
    public class GitlabFilter
    {
        /// <summary><c>opened</c>, <c>closed</c>, <c>all</c>. Defaults to GitLab's own default (<c>opened</c>) when null.</summary>
        public string? State { get; set; }

        /// <summary>Issues with all of these labels (AND).</summary>
        public string[]? LabelName { get; set; }

        /// <summary>Issues assigned to any of these usernames (OR).</summary>
        public string[]? AssigneeUsernames { get; set; }

        /// <summary>Issues authored by this username.</summary>
        public string? AuthorUsername { get; set; }

        /// <summary>Issues attached to a milestone with this title.</summary>
        public string[]? MilestoneTitle { get; set; }

        /// <summary>Free-text search in title/description.</summary>
        public string? Search { get; set; }

        /// <summary>Created on or after this ISO-8601 timestamp.</summary>
        public string? CreatedAfter { get; set; }

        /// <summary>Created on or before this ISO-8601 timestamp.</summary>
        public string? CreatedBefore { get; set; }

        /// <summary>Updated on or after this ISO-8601 timestamp.</summary>
        public string? UpdatedAfter { get; set; }

        /// <summary>Updated on or before this ISO-8601 timestamp.</summary>
        public string? UpdatedBefore { get; set; }

        /// <summary>Only confidential (true) / only non-confidential (false) — leave null for both.</summary>
        public bool? Confidential { get; set; }

        /// <summary>Issue iids (per-project numeric ids). String to preserve leading zeros if any.</summary>
        public string[]? Iids { get; set; }

        /// <summary>
        /// True iff at least one filter field is set. Used by the YAML converter to drop
        /// empty rules — GitLab's <c>Query.issues</c> refuses unfiltered scans.
        /// </summary>
        public bool HasAnyField =>
            !string.IsNullOrEmpty(State)
            || (LabelName != null && LabelName.Length > 0)
            || (AssigneeUsernames != null && AssigneeUsernames.Length > 0)
            || !string.IsNullOrEmpty(AuthorUsername)
            || (MilestoneTitle != null && MilestoneTitle.Length > 0)
            || !string.IsNullOrEmpty(Search)
            || !string.IsNullOrEmpty(CreatedAfter)
            || !string.IsNullOrEmpty(CreatedBefore)
            || !string.IsNullOrEmpty(UpdatedAfter)
            || !string.IsNullOrEmpty(UpdatedBefore)
            || Confidential.HasValue
            || (Iids != null && Iids.Length > 0);

        /// <summary>
        /// Human-readable one-liner for the digest header (mirror of GitHub's
        /// "Search: …" line). Skips null/empty fields, joins arrays with commas.
        /// </summary>
        public string ToHumanString()
        {
            var parts = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(State)) parts.Add($"state={State}");
            if (LabelName != null && LabelName.Length > 0) parts.Add($"label={string.Join(",", LabelName)}");
            if (AssigneeUsernames != null && AssigneeUsernames.Length > 0) parts.Add($"assignee={string.Join(",", AssigneeUsernames)}");
            if (!string.IsNullOrEmpty(AuthorUsername)) parts.Add($"author={AuthorUsername}");
            if (MilestoneTitle != null && MilestoneTitle.Length > 0) parts.Add($"milestone={string.Join(",", MilestoneTitle)}");
            if (!string.IsNullOrEmpty(Search)) parts.Add($"search=\"{Search}\"");
            if (!string.IsNullOrEmpty(CreatedAfter)) parts.Add($"createdAfter={CreatedAfter}");
            if (!string.IsNullOrEmpty(CreatedBefore)) parts.Add($"createdBefore={CreatedBefore}");
            if (!string.IsNullOrEmpty(UpdatedAfter)) parts.Add($"updatedAfter={UpdatedAfter}");
            if (!string.IsNullOrEmpty(UpdatedBefore)) parts.Add($"updatedBefore={UpdatedBefore}");
            if (Confidential.HasValue) parts.Add($"confidential={Confidential.Value.ToString().ToLowerInvariant()}");
            if (Iids != null && Iids.Length > 0) parts.Add($"iid={string.Join(",", Iids)}");
            return string.Join("  ", parts);
        }

        /// <summary>
        /// Builds the query string for GitLab's <c>/dashboard/issues</c> page so the
        /// digest can include a one-click round-trip link. Array fields use the
        /// <c>name[]=value</c> repeated form GitLab expects, URL-encoded.
        /// </summary>
        public string ToDashboardQueryString()
        {
            var parts = new System.Collections.Generic.List<string>();
            void Add(string key, string? value)
            {
                if (string.IsNullOrEmpty(value)) return;
                parts.Add($"{key}={System.Uri.EscapeDataString(value)}");
            }
            void AddArray(string key, string[]? values)
            {
                if (values == null) return;
                foreach (var v in values)
                    if (!string.IsNullOrEmpty(v))
                        parts.Add($"{System.Uri.EscapeDataString(key + "[]")}={System.Uri.EscapeDataString(v)}");
            }
            Add("state", State);
            AddArray("label_name", LabelName);
            AddArray("assignee_username", AssigneeUsernames);
            Add("author_username", AuthorUsername);
            // Dashboard accepts a single milestone_title param; if the user listed
            // several, use the first (rendering all of them isn't supported by the
            // dashboard UI either).
            Add("milestone_title", MilestoneTitle?.Length > 0 ? MilestoneTitle[0] : null);
            Add("search", Search);
            Add("created_after", CreatedAfter);
            Add("created_before", CreatedBefore);
            Add("updated_after", UpdatedAfter);
            Add("updated_before", UpdatedBefore);
            if (Confidential.HasValue) Add("confidential", Confidential.Value.ToString().ToLowerInvariant());
            return string.Join("&", parts);
        }
    }
}
