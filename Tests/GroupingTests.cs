using System;
using System.Linq;
using Preesta.Configuration;
using Preesta.Configuration.Action;
using Preesta.Data;
using Preesta.Data.Supplying;
using Preesta.Data.Supplying.Convert;
using NUnit.Framework;
using NSubstitute;
using Serilog;

namespace Tests
{
    [TestFixture]
    public class GroupingTests
    {
        [Test]
        public void GroupBuilds()
        {
            var jira = Substitute.For<Preesta.IJiraService>();
            jira
                .GetReleases(Arg.Any<string>())
                .Returns(
                    new[]
                    {
                        new Release
                        {
                            Name = "9.0.0.1",
                            ReleaseDate = DateTime.Now.AddDays(1)
                        },
                        new Release
                        {
                            Name = "9.0.0.2",
                            ReleaseDate = DateTime.Now.AddDays(1)
                        }
                    }
                );

            var rules = new[]
            {
                new ReleaseRule
                {
                    Mask = @"^9\.0\.0\.1",
                    RemainingDays = 1,
                    Notification = new NotificationSpec
                    {
                        Subject = "Subject",
                        RawRecipients = new[] {"teammate1@express.ship", "teammate2@express.ship"},
                        RawCc = new[] {"teammate1@express.ship", "teammate2@express.ship"}
                    }
                },
                new ReleaseRule
                {
                    Mask = @"^9\.0\.0\.2",
                    RemainingDays = 1,
                    Notification = new NotificationSpec
                    {
                        Subject = "Subject",
                        RawRecipients = new[] {"teammate2@express.ship", "teammate1@express.ship"},
                        RawCc = new[] {"teammate2@express.ship", "teammate1@express.ship"}
                    }
                },
                new ReleaseRule
                {
                    Mask = @"^9\.0\.0\.2",
                    RemainingDays = 1,
                    Notification = new NotificationSpec
                    {
                        Subject = "DifferentSubject",

                        RawRecipients = new[] {"teammate2@express.ship", "teammate1@express.ship"},
                        RawCc = new[] {"teammate2@express.ship", "teammate1@express.ship"}
                    }
                }
            };

            var packages = new ReleaseSupplier(jira, rules).GetPackages().Cast<Package<NotificationReaction, Release>>().ToArray();
            Assert.AreEqual(2, packages.Count());
            Assert.AreEqual(2, packages.Single(p => p.Reaction.Subject == "Subject").Items.Count());
            var messages = new ReleasePackageConverter().ToMessages(packages);
            Assert.AreEqual(2, messages.Count());
            var actualBody1 = messages.First().Body;
            var actualBody2 = messages.ElementAt(1).Body;

            var tomorrow = DateTime.Now.AddDays(1).ToString("dd.MM.yyyy");

            Assert.IsTrue(actualBody1.Contains("Subject"));
            Assert.IsTrue(actualBody1.Contains("9.0.0.1"));
            Assert.IsTrue(actualBody1.Contains("9.0.0.2"));
            Assert.IsTrue(actualBody1.Contains(tomorrow));

            Assert.IsTrue(actualBody2.Contains("DifferentSubject"));
            Assert.IsTrue(actualBody2.Contains("9.0.0.2"));
            Assert.IsFalse(actualBody2.Contains("9.0.0.1"));
        }

        [Test]
        public void GroupIssues()
        {
            var jira = Substitute.For<Preesta.IJiraService>();
            jira
                .GetIssuesForJql(Arg.Any<string>())
                .Returns(
                    new[]
                    {
                        new Issue
                        {
                            FixVersions = new[] {"1"},
                            AffectsVersions = new string[] {},
                            Participants = new IssueParticipants
                                    {
                                        Assignee = null
                                    }
                        }
                    }
                );

            var rules = new[]
            {
                new JqlRule
                {
                    Notification = new NotificationSpec
                    {
                        Subject = "Subject",
                        RawRecipients = new[] {"teammate1@express.ship", "teammate2@express.ship"},
                        RawCc = new[] {"teammate1@express.ship", "teammate2@express.ship"}
                    }
                },
                new JqlRule
                {
                    Notification = new NotificationSpec
                    {
                        Subject = "Subject",
                        RawRecipients = new[] {"teammate1@express.ship", "teammate2@express.ship"},
                        RawCc = new[] {"teammate2@express.ship", "teammate1@express.ship"}
                    }
                },
                new JqlRule
                {
                    Notification = new NotificationSpec
                    {
                        Subject = "DifferentSubject",
                        RawRecipients = new[] {"teammate1@express.ship", "teammate2@express.ship"},
                        RawCc = new[] {"teammate1@express.ship", "teammate2@express.ship"}
                    }
                }
            };

            var logger = Substitute.For<ILogger>();

            var packages = new JqlSupplier(jira, rules, logger)
                .GetPackages()
                .Cast<Package<NotificationReaction, Issue>>()
                .ToArray();

            Assert.AreEqual(3, packages.Count());
            Assert.AreEqual(2, packages.Count(p => p.Reaction.Subject == "Subject"));
            Assert.AreEqual(1, packages.Count(p => p.Reaction.Subject == "DifferentSubject"));

            var messages = new IssuePackageConverter("http://jira", subjectPrefix: "")
                .ToMessages(packages);
            Assert.AreEqual(2, messages.Count());
            Assert.AreEqual(1, messages.Count(m => m.Subject == "Subject"));
            Assert.AreEqual(1, messages.Count(m => m.Subject == "DifferentSubject"));
        }
    }
}
