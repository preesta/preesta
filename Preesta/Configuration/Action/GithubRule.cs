namespace Preesta.Configuration.Action
{
    /// <summary>
    /// Wires <see cref="GithubIssueSource"/> into the notification pipeline.
    /// </summary>
    /// <remarks>
    /// Selection mode is a single raw GitHub search string (<see cref="Filter"/>) — the
    /// same syntax users see in the GitHub web UI. Multi-repo, org-wide and
    /// issue-vs-PR distinctions all live inside that string (e.g.
    /// <c>"is:open is:issue org:bigcorp label:urgent"</c>).
    /// <para>
    /// GitHub's search syntax is already human-readable, so unlike Linear there is no
    /// AI-prompt mode, no separate "raw" mode and no saved-view mode — one field covers
    /// every case.
    /// </para>
    /// <para>
    /// Rules are deliberately obezlichennye (impersonal): no <c>assignee:@me</c> /
    /// <c>author:@me</c> inside the filter. Per-recipient routing happens later in the
    /// notification step via the standard <c>assignee</c> / <c>reporter</c> markers in
    /// <c>mailTo</c>.
    /// </para>
    /// </remarks>
    public class GithubRule : Rule
    {
        /// <summary>
        /// Raw GitHub search query (e.g. <c>"is:open is:issue org:bigcorp"</c>).
        /// Required.
        /// </summary>
        public string? Filter { get; set; }

        /// <summary>
        /// Raw GraphQL mutations to execute for each matched issue. Power-user hook —
        /// the rule author writes the full <c>mutation { ... }</c> body and places markers
        /// (<c>{{@issueId}}</c>, <c>{{@assignee.email}}</c>, etc.) where Preesta should
        /// substitute issue context. No DSL, no ID resolution — bring your own state/user/label IDs.
        /// </summary>
        public GraphQLMutationSpec[] GraphQLMutations { get; set; } = new GraphQLMutationSpec[] { };
    }
}
