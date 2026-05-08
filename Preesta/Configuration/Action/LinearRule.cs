namespace Preesta.Configuration.Action
{
    /// <summary>
    /// Marker rule that wires <see cref="LinearIssueSource"/> into the notification
    /// pipeline. The MVP filter (assignee=viewer, state.type≠completed) is hardcoded
    /// inside the source — no extra fields beyond what <see cref="Rule"/> provides.
    /// A user-friendly filter DSL is deferred to Phase 12.1.
    /// </summary>
    public class LinearRule : Rule
    {
    }
}
