using System.Linq;
using Preesta;
using Preesta.Configuration;
using Preesta.Configuration.Action;
using Preesta.Data;
using Preesta.Data.Supplying;
using NSubstitute;
using NUnit.Framework;
using Serilog;

namespace Tests
{
    [TestFixture]
    public class PerIssueSplittingTests
    {
        private static IJiraService JiraReturning(params Issue[] issues)
        {
            var jira = Substitute.For<IJiraService>();
            jira.GetIssuesForJql(Arg.Any<string>()).Returns(issues);
            return jira;
        }

        [Test]
        public void AssigneeMarkerSplitsIssuesByAssigneeIntoSeparatePackages()
        {
            var rule = new JqlRule
            {
                Filter = "DueDate < startOfDay() AND Resolution is EMPTY",
                Notification = new NotificationSpec
                {
                    Subject = "DueDate expired",
                    RawRecipients = new[] { "assignee" },
                    RawCc = new string[] { }
                }
            };

            var supplier = new JqlSupplier(JiraReturning(
                new Issue { Key = "T-1", Participants = new IssueParticipants { Assignee = new User { Email = "ivanov@x" } } },
                new Issue { Key = "T-2", Participants = new IssueParticipants { Assignee = new User { Email = "sidorov@x" } } },
                new Issue { Key = "T-3", Participants = new IssueParticipants { Assignee = new User { Email = "ivanov@x" } } }
            ), new[] { rule }, Substitute.For<ILogger>());

            var packages = supplier.GetPackages()
                .Cast<Package<NotificationReaction, Issue>>()
                .ToArray();

            Assert.AreEqual(2, packages.Length);

            var ivanov = packages.Single(p => p.Reaction.Addressees.To.Contains("ivanov@x"));
            var sidorov = packages.Single(p => p.Reaction.Addressees.To.Contains("sidorov@x"));

            CollectionAssert.AreEquivalent(new[] { "T-1", "T-3" }, ivanov.Items.Select(i => i.Key));
            CollectionAssert.AreEquivalent(new[] { "T-2" }, sidorov.Items.Select(i => i.Key));
        }

        [Test]
        public void ReporterMarkerInCcSplitsByReporter()
        {
            var rule = new JqlRule
            {
                Filter = "any",
                Notification = new NotificationSpec
                {
                    Subject = "S",
                    RawRecipients = new[] { "assignee" },
                    RawCc = new[] { "reporter" }
                }
            };

            var supplier = new JqlSupplier(JiraReturning(
                new Issue
                {
                    Key = "T-1",
                    Participants = new IssueParticipants
                    {
                        Assignee = new User { Email = "a@x" },
                        Reporter = new User { Email = "p1@x" }
                    }
                },
                new Issue
                {
                    Key = "T-2",
                    Participants = new IssueParticipants
                    {
                        Assignee = new User { Email = "a@x" },
                        Reporter = new User { Email = "p2@x" }
                    }
                }
            ), new[] { rule }, Substitute.For<ILogger>());

            var packages = supplier.GetPackages()
                .Cast<Package<NotificationReaction, Issue>>()
                .ToArray();

            Assert.AreEqual(2, packages.Length);

            var p1 = packages.Single(p => p.Reaction.Addressees.Cc.Contains("p1@x"));
            var p2 = packages.Single(p => p.Reaction.Addressees.Cc.Contains("p2@x"));

            CollectionAssert.AreEquivalent(new[] { "T-1" }, p1.Items.Select(i => i.Key));
            CollectionAssert.AreEquivalent(new[] { "T-2" }, p2.Items.Select(i => i.Key));
        }

        [Test]
        public void StaticAddresseesNextToMarkerArePreserved()
        {
            var rule = new JqlRule
            {
                Filter = "any",
                Notification = new NotificationSpec
                {
                    Subject = "S",
                    RawRecipients = new[] { "assignee" },
                    RawCc = new[] { "reporter", "managers" }
                }
            };

            var supplier = new JqlSupplier(JiraReturning(
                new Issue
                {
                    Key = "T-1",
                    Participants = new IssueParticipants
                    {
                        Assignee = new User { Email = "a@x" },
                        Reporter = new User { Email = "p@x" }
                    }
                }
            ), new[] { rule }, Substitute.For<ILogger>());

            var package = supplier.GetPackages()
                .Cast<Package<NotificationReaction, Issue>>()
                .Single();

            CollectionAssert.AreEquivalent(new[] { "a@x" }, package.Reaction.Addressees.To);
            CollectionAssert.AreEquivalent(new[] { "managers", "p@x" }, package.Reaction.Addressees.Cc);
        }

        [Test]
        public void IssuesWithSameAssigneeButDifferentReporterEndUpInDifferentPackages()
        {
            var rule = new JqlRule
            {
                Filter = "any",
                Notification = new NotificationSpec
                {
                    Subject = "S",
                    RawRecipients = new[] { "assignee", "reporter" },
                    RawCc = new string[] { }
                }
            };

            var supplier = new JqlSupplier(JiraReturning(
                new Issue
                {
                    Key = "T-1",
                    Participants = new IssueParticipants
                    {
                        Assignee = new User { Email = "a@x" },
                        Reporter = new User { Email = "p1@x" }
                    }
                },
                new Issue
                {
                    Key = "T-2",
                    Participants = new IssueParticipants
                    {
                        Assignee = new User { Email = "a@x" },
                        Reporter = new User { Email = "p2@x" }
                    }
                }
            ), new[] { rule }, Substitute.For<ILogger>());

            var packages = supplier.GetPackages()
                .Cast<Package<NotificationReaction, Issue>>()
                .ToArray();

            Assert.AreEqual(2, packages.Length);
        }
    }
}
