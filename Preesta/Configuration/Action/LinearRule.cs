using Newtonsoft.Json.Linq;

namespace Preesta.Configuration.Action
{
    /// <summary>
    /// Wires <see cref="LinearIssueSource"/> into the notification pipeline.
    /// </summary>
    /// <remarks>
    /// A Linear rule selects which issues to fetch via exactly one of three mutually
    /// exclusive modes:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// <b>AI prompt</b> (<see cref="Filter"/>) — natural-language description of the
    /// desired filter. This is the primary, user-facing mode (the only one mentioned
    /// in user-facing docs). Internally a 2-hop fetch: Linear's
    /// <c>issueFilterSuggestion</c> API translates the prompt into a filter object,
    /// which is then passed to <c>issues(filter:)</c>.
    /// Example: <c>filter: "issues assigned to me, not completed"</c>.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <b>Raw GraphQL filter</b> (<see cref="FilterRaw"/>, advanced/undocumented escape
    /// hatch) — the user supplies a literal Linear filter object that is passed
    /// straight into <c>issues(filter:)</c>. The user is responsible for valid
    /// shape; no DSL translation is performed.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <b>Saved view ID</b> (<see cref="ViewId"/>, advanced/undocumented escape
    /// hatch) — fetches the issues of a Linear saved view created in the UI.
    /// Implemented via <c>customView(id:){ issues { ... } }</c>; Preesta does not
    /// touch the filter at all.
    /// </description>
    /// </item>
    /// </list>
    /// Validation (in the YAML converter): exactly one of the three must be set; rules
    /// that set zero or more than one are dropped with an error log.
    /// </remarks>
    public class LinearRule : Rule
    {
        /// <summary>AI-prompt filter (primary, user-facing). Mutually exclusive with <see cref="FilterRaw"/> and <see cref="ViewId"/>.</summary>
        public string? Filter { get; set; }

        /// <summary>Raw Linear GraphQL filter object (escape hatch). Mutually exclusive with <see cref="Filter"/> and <see cref="ViewId"/>.</summary>
        public JObject? FilterRaw { get; set; }

        /// <summary>Linear saved-view ID (escape hatch). Mutually exclusive with <see cref="Filter"/> and <see cref="FilterRaw"/>.</summary>
        public string? ViewId { get; set; }
    }
}
