using System;
using System.Linq;
using System.Threading.Tasks;
using Preesta.Data.Supplying;
using Preesta.Data.Supplying.Convert;
using Preesta.Extensions;
using Preesta.Notification.Delivery;
using Serilog;

namespace Preesta.Notification
{
    internal class ReactionPipeline<TIssueType>
    {
        public IPackageSupplier? PackageSupplier { get; init; }
        public IPackageConverter<TIssueType>? PackageConverter { get; init; }

        /// <summary>Delivery targets (email / Telegram / Slack / …). Defaults to
        /// an empty set so a pipeline configured only for mutations sends nothing.</summary>
        public DeliveryChannels Channels { get; init; } = DeliveryChannels.None;

        // Write side. Phase 2 will collapse these two into a single
        // IMutationHandler — a tracker has exactly one mutation transport.
        public IHttpHandler? HttpHandler { get; init; }
        public IGraphQLMutationHandler? GraphQLMutationHandler { get; init; }

        public ILogger? Logger { get; init; }

        public void Run()
        {
            if (PackageConverter == null || PackageSupplier == null)
            {
                return;
            }

            var allPackages = PackageSupplier.GetPackages();

            var notificationPackages = allPackages
                .OfType<Package<NotificationReaction, TIssueType>>()
                .ToArray();

            // Each channel runs inside its own try/catch — a misconfigured or
            // unreachable channel (bad SMTP creds, blocked Telegram user, expired
            // Slack token) must not abort the rest of the run. Failures are logged
            // at Error and the next channel proceeds.
            foreach (var channel in Channels.Channels)
            {
                var ch = channel;
                TryRunStage($"{ch.Name} send",
                    () => ch.Deliver(notificationPackages, PackageConverter));
            }

            TryRunStage("REST mutations", () =>
            {
                var selfUpdates = allPackages
                    .OfType<Package<SelfUpdate, TIssueType>>()
                    .ToHttpRequests(PackageConverter);
                HttpHandler?.HandleAll(selfUpdates);
            });

            TryRunStage("GraphQL mutations", () =>
            {
                var graphQLBodies = allPackages
                    .OfType<Package<GraphQLMutation, TIssueType>>()
                    .ToGraphQLMutationBodies(PackageConverter);
                GraphQLMutationHandler?.HandleAll(graphQLBodies);
            });
        }

        private void TryRunStage(string stageName, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Pipeline stage '{Stage}' failed; subsequent stages continue", stageName);
            }
        }

        public async Task RunAsync()
        {
            await Task.Factory.StartNew(Run);
        }
    }
}
