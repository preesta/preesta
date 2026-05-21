using System.Collections.Generic;
using Preesta.AppConfig;
using Preesta.Configuration;
using Preesta.Notification.Delivery;
using Serilog;

namespace Preesta.DI.Modules
{
    /// <summary>
    /// The shared inputs every tracker module needs to build its pipeline:
    /// configuration, the parsed rules, the active schedule group, the common
    /// delivery channels, the Jira custom-field map (for column rendering),
    /// the Jira service (for predicate-based filtering in the supplier base),
    /// and the logger. Assembled once in <see cref="DependencyContainer"/>.
    /// </summary>
    internal sealed record TrackerBuildContext(
        AppSettings Settings,
        IRulesConfig Rules,
        string Group,
        DeliveryChannels Channels,
        IReadOnlyDictionary<string, string> CustomFields,
        IJiraService JiraService,
        ILogger Logger);
}
