using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using static System.String;

namespace Preesta.Data
{
    public class Issue
    {
        public string Key { get; set; } = Empty;
        public string Summary { get; set; } = Empty;
        /// <summary>
        /// Canonical "Open in tracker" URL. Populated by sources that return it
        /// directly in the API payload (e.g. Linear). For Jira issues this is left
        /// null and the formatter reconstructs the URL from the rootUri + key.
        /// </summary>
        public string? Url { get; set; }
        public IssueParticipants Participants { get; set; } = new IssueParticipants();
        public string? Type { get; set; }
        public string? Status { get; set; }
        public string? Priority { get; set; }
        public string? Components { get; set; }
        public string Labels { get; set; } = Empty;
        public TimeSpan TimeSpent { get; set; }
        public string[]? AffectsVersions { get; set; }
        public string[]? FixVersions { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public string? Resolution { get; set; }
        public string? ProjectKey { get; set; }

        /// <summary>
        /// Tracker-internal UUID. Populated by sources that issue mutations against
        /// an internal-id-based API (Linear's GraphQL <c>issueUpdate(id:)</c>,
        /// <c>commentCreate(input: {issueId})</c>, etc.). Jira leaves this null —
        /// Jira REST identifies issues by the human key.
        /// Used by <c>{{@issueId}}</c> marker in mutation templates.
        /// </summary>
        public string? LinearId { get; set; }

        /// <summary>
        /// GitHub GraphQL node ID for this issue/PR (opaque base64 string from the
        /// <c>id</c> field). Populated by <c>GithubIssueSource</c>; Jira and Linear
        /// issues leave this null. Used by <c>{{@issueId}}</c> marker in mutation
        /// templates — the GraphQL endpoint expects this for <c>updateIssue</c>,
        /// <c>addComment</c>, <c>closeIssue</c>, etc.
        /// </summary>
        public string? GithubNodeId { get; set; }

        /// <summary>
        /// GitLab GraphQL global ID for this issue (the <c>gid://gitlab/Issue/1234</c>
        /// form returned by <c>Issue.id</c>). Populated by <c>GitlabIssueSource</c>;
        /// other sources leave this null. Used by <c>{{@issueId}}</c> marker in
        /// mutation templates — GitLab's GraphQL mutations identify issues via this
        /// opaque global id.
        /// </summary>
        public string? GitlabGlobalId { get; set; }

        /// <summary>
        /// Shortcut story public ID (integer, stringified) — populated by
        /// <c>ShortcutIssueSource</c>. Jira / Linear / GitHub leave this null.
        /// Used by <c>{{@issueId}}</c> marker in Shortcut REST mutation templates
        /// (<c>PUT /api/v3/stories/{{@issueId}}</c>,
        /// <c>POST /api/v3/stories/{{@issueId}}/comments</c>).
        /// </summary>
        public string? ShortcutId { get; set; }

        /// <summary>
        /// Raw Jira custom-field payload keyed by the internal field id
        /// (<c>customfield_10001</c> etc.). Each value is the unmodified
        /// <see cref="JToken"/> Jira returned — string / number / array /
        /// object — so the formatter can decide how to render based on shape.
        /// Empty for Linear-sourced issues (Linear has no flat custom-field scheme).
        /// </summary>
        public Dictionary<string, JToken?> CustomFields { get; set; } = new Dictionary<string, JToken?>();
    }
}