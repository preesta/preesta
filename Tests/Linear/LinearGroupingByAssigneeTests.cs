using System.Linq;
using NSubstitute;
using NUnit.Framework;
using Preesta;
using Preesta.Configuration.Action;
using Preesta.Data;
using Preesta.Data.Supplying;
using Serilog;

namespace Tests.Linear
{
    /// <summary>
    /// Regression guard for the obezlichennye-rules contract: a single Linear rule
    /// with <c>mailTo: ["assignee"]</c> must produce one notification package per
    /// distinct assignee email, with issues partitioned correctly between them.
    /// Equivalent for "reporter" (Linear's reporter = GraphQL creator).
    /// </summary>
    [TestFixture]
    public class LinearGroupingByAssigneeTests
    {
        private static Issue Issue(string key, string assigneeEmail, string? creatorEmail = null) =>
            new Issue
            {
                Key = key,
                Summary = key,
                Participants = new IssueParticipants
                {
                    Assignee = new User { Email = assigneeEmail, DisplayName = assigneeEmail },
                    Reporter = creatorEmail == null ? null
                        : new User { Email = creatorEmail, DisplayName = creatorEmail },
                    Creator = creatorEmail == null ? null
                        : new User { Email = creatorEmail, DisplayName = creatorEmail }
                }
            };

        private static LinearIssueSupplier SupplierWith(LinearRule rule, params Issue[] issues)
        {
            var source = Substitute.For<LinearIssueSource>("dummy-key", null, null);
            source.GetIssues(Arg.Any<LinearRule>()).Returns(issues);
            return new LinearIssueSupplier(
                source, Substitute.For<IJiraService>(),
                new[] { rule }, Substitute.For<ILogger>());
        }

        [Test]
        public void GroupsByAssigneeEmail_OnePackagePerDistinctAssignee()
        {
            var rule = new LinearRule
            {
                FilterRaw = new Newtonsoft.Json.Linq.JObject(),
                Notification = new NotificationSpec
                {
                    Subject = "Open",
                    RawRecipients = new[] { "assignee" },
                    RawCc = new string[] { }
                }
            };

            var supplier = SupplierWith(rule,
                Issue("PRE-1", "alice@x.com"),
                Issue("PRE-2", "alice@x.com"),
                Issue("PRE-3", "bob@x.com"));

            var packages = supplier.GetPackages()
                .OfType<Package<NotificationReaction, Issue>>()
                .ToArray();

            Assert.AreEqual(2, packages.Length, "Expected one package per distinct assignee");

            var alicePkg = packages.Single(p => p.Reaction.Addressees.To.Contains("alice@x.com"));
            var bobPkg = packages.Single(p => p.Reaction.Addressees.To.Contains("bob@x.com"));

            CollectionAssert.AreEquivalent(
                new[] { "PRE-1", "PRE-2" }, alicePkg.Items.Select(i => i.Key));
            CollectionAssert.AreEquivalent(
                new[] { "PRE-3" }, bobPkg.Items.Select(i => i.Key));
        }

        [Test]
        public void GroupsByReporterEmail_OnePackagePerDistinctReporter()
        {
            // Linear has no separate "reporter" — LinearIssueSource maps GraphQL
            // `creator` into both Participants.Reporter and Participants.Creator,
            // so the marker behaves the same way as in Jira rules.
            var rule = new LinearRule
            {
                FilterRaw = new Newtonsoft.Json.Linq.JObject(),
                Notification = new NotificationSpec
                {
                    Subject = "Opened",
                    RawRecipients = new[] { "reporter" },
                    RawCc = new string[] { }
                }
            };

            var supplier = SupplierWith(rule,
                Issue("PRE-1", assigneeEmail: "alice@x.com", creatorEmail: "carol@x.com"),
                Issue("PRE-2", assigneeEmail: "bob@x.com", creatorEmail: "carol@x.com"),
                Issue("PRE-3", assigneeEmail: "alice@x.com", creatorEmail: "dave@x.com"));

            var packages = supplier.GetPackages()
                .OfType<Package<NotificationReaction, Issue>>()
                .ToArray();

            Assert.AreEqual(2, packages.Length);

            var carolPkg = packages.Single(p => p.Reaction.Addressees.To.Contains("carol@x.com"));
            var davePkg = packages.Single(p => p.Reaction.Addressees.To.Contains("dave@x.com"));

            CollectionAssert.AreEquivalent(
                new[] { "PRE-1", "PRE-2" }, carolPkg.Items.Select(i => i.Key));
            CollectionAssert.AreEquivalent(
                new[] { "PRE-3" }, davePkg.Items.Select(i => i.Key));
        }

        [Test]
        public void LiteralEmailsAndAssigneeMarker_CoexistInRawRecipients()
        {
            // Mixed: a literal team-lead address always gets the digest, plus the
            // assignee gets their personal one — should produce two packages with
            // the lead present in both (Cc-style fanout via To).
            var rule = new LinearRule
            {
                FilterRaw = new Newtonsoft.Json.Linq.JObject(),
                Notification = new NotificationSpec
                {
                    Subject = "Open",
                    RawRecipients = new[] { "assignee", "lead@x.com" },
                    RawCc = new string[] { }
                }
            };

            var supplier = SupplierWith(rule,
                Issue("PRE-1", "alice@x.com"),
                Issue("PRE-2", "bob@x.com"));

            var packages = supplier.GetPackages()
                .OfType<Package<NotificationReaction, Issue>>()
                .ToArray();

            Assert.AreEqual(2, packages.Length);
            Assert.IsTrue(packages.All(p => p.Reaction.Addressees.To.Contains("lead@x.com")),
                "Literal recipient must appear in every per-assignee package");
        }
    }
}
