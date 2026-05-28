using System;
using System.Linq;
using System.Threading.Tasks;
using Preesta.Data.Supplying;
using Preesta.Data.Supplying.Convert;
using Preesta.Notification.Delivery;
using Preesta.Notification.Mutation;
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

        /// <summary>Write side — one transport per tracker (REST or GraphQL).
        /// Null when the tracker's pipeline does no mutations.</summary>
        public IMutationHandler? Mutations { get; init; }

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

            // Trace line so a quiet pipeline can be distinguished from "0 matches".
            // Without this a misrouted rule looks identical to an empty result set.
            Logger?.Information(
                "Pipeline {Item}: {AllCount} package(s), {NotifyCount} for notification",
                typeof(TIssueType).Name, allPackages.Count(), notificationPackages.Length);

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

            TryRunStage("mutations",
                () => Mutations?.Execute(allPackages, PackageConverter));
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
