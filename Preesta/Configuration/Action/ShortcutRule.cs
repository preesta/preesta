namespace Preesta.Configuration.Action
{
    /// <summary>
    /// Wires <see cref="ShortcutIssueSource"/> into the notification pipeline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Selection mode is a single raw Shortcut search string (<see cref="Filter"/>) —
    /// the same syntax users see in Shortcut's web search bar (e.g.
    /// <c>state:"In Progress" type:bug !is:archived</c>). Shortcut's search operators
    /// are already human-readable, so no per-tracker DSL is layered on top.
    /// </para>
    /// <para>
    /// Like the GitHub rule, Shortcut rules are deliberately obezlichennye (impersonal):
    /// no <c>owner:me</c> / <c>requester:me</c> inside the filter. Per-recipient routing
    /// happens later in the notification step via the standard <c>assignee</c> /
    /// <c>reporter</c> markers in <c>mailTo</c>.
    /// </para>
    /// <para>
    /// Mutations are raw REST (Shortcut has no GraphQL), shape identical to Jira's:
    /// <c>verb</c> + <c>urlPattern</c> (path relative to api.app.shortcut.com) + <c>body</c>.
    /// Power-user hook — markers <c>{{@issueId}}</c> (resolves to the Shortcut story
    /// public id), <c>{{@title}}</c>, <c>{{@assignee.email}}</c>, etc.
    /// </para>
    /// </remarks>
    public class ShortcutRule : Rule
    {
        /// <summary>
        /// Raw Shortcut search query (e.g. <c>"state:\"In Progress\" type:bug"</c>).
        /// Required.
        /// </summary>
        public string? Filter { get; set; }
    }
}
