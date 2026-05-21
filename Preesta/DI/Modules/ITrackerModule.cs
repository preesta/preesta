using Preesta.AppConfig;
using Preesta.Data;
using Preesta.Notification;

namespace Preesta.DI.Modules
{
    /// <summary>
    /// A self-contained issue-tracker integration. Each module owns its own
    /// "am I configured?" check and the construction of its read path
    /// (source + supplier), write path (mutation handler) and converter.
    /// The orchestrator (<see cref="DependencyContainer"/>) discovers modules,
    /// registers the configured ones, and never names a specific tracker —
    /// so adding a tracker is one new module class plus one list entry, with
    /// no edits to the orchestrator or to <c>Application</c>.
    /// </summary>
    internal interface ITrackerModule
    {
        /// <summary>Service key the pipeline is registered/resolved under.</summary>
        string Key { get; }

        /// <summary>True when this tracker's configuration section is present.</summary>
        bool IsConfigured(AppSettings settings);

        /// <summary>Builds the fully-wired pipeline for this tracker.</summary>
        ReactionPipeline<Issue> BuildPipeline(TrackerBuildContext context);
    }
}
