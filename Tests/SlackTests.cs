using System.Collections.Generic;
using System.Linq;
using Messaging;
using Preesta;
using Preesta.Configuration.Action;
using Preesta.Data;
using Preesta.Data.Supplying;
using Preesta.Data.Supplying.Convert;
using Preesta.Formatting;
using Preesta.Notification;
using NSubstitute;
using NUnit.Framework;
using Serilog;
using Tests.Mocks;

namespace Tests
{
    [TestFixture]
    public class SlackTests
    {
        private static Redirector EmptyRedirector => Redirector.Empty;
        private static IReadOnlyDictionary<string, string> EmptyMap => new Dictionary<string, string>();

        // ----- SlackMessenger direct tests -----

        [Test]
        public void Send_PostsAuthBearerAndChannelAndText()
        {
            using var server = new MockSlackServer();
            server.StubPostMessageOk();

            var messenger = new SlackMessenger("xoxb-FAKE-TEST-TOKEN", server.PostMessageUrl);
            messenger.Send(new Message
            {
                To = "U123ABC",
                TextBody = "*hello* from Preesta"
            });

            var entry = server.LogEntries.Single();
            var headers = entry.RequestMessage!.Headers!;
            Assert.IsTrue(headers.ContainsKey("Authorization"),
                "Authorization header missing");
            Assert.AreEqual("Bearer xoxb-FAKE-TEST-TOKEN", headers["Authorization"].ToString());

            var body = entry.RequestMessage.Body ?? "";
            Assert.IsTrue(body.Contains("\"channel\":\"U123ABC\""), $"channel missing in body: {body}");
            Assert.IsTrue(body.Contains("hello"), $"text missing in body: {body}");
            Assert.IsTrue(body.Contains("\"mrkdwn\":true"), $"mrkdwn flag missing in body: {body}");
        }

        [Test]
        public void Send_OkFalseResponse_LogsErrorDoesNotThrow()
        {
            using var server = new MockSlackServer();
            server.StubPostMessageError("users_not_found");

            var logger = Substitute.For<ILogger>();
            var messenger = new SlackMessenger("xoxb-FAKE-TEST-TOKEN", server.PostMessageUrl, logger: logger);

            Assert.DoesNotThrow(() => messenger.Send(new Message
            {
                To = "Uinvalid",
                TextBody = "anything"
            }));

            // We don't assert exact message text — count of Error calls is enough signal.
            var errorCalls = logger.ReceivedCalls()
                .Count(c => c.GetMethodInfo().Name == "Error");
            Assert.GreaterOrEqual(errorCalls, 1,
                "Expected ILogger.Error when chat.postMessage returns ok:false");
        }

        [Test]
        public void Send_HttpError_LogsErrorDoesNotThrow()
        {
            using var server = new MockSlackServer();
            server.StubPostMessageHttpError(500);

            var logger = Substitute.For<ILogger>();
            var messenger = new SlackMessenger("xoxb-FAKE-TEST-TOKEN", server.PostMessageUrl, logger: logger);

            Assert.DoesNotThrow(() => messenger.Send(new Message
            {
                To = "U123",
                TextBody = "anything"
            }));

            var errorCalls = logger.ReceivedCalls()
                .Count(c => c.GetMethodInfo().Name == "Error");
            Assert.GreaterOrEqual(errorCalls, 1,
                "Expected ILogger.Error on HTTP 5xx");
        }

        // ----- MessageBuilder.ToSlackMessages routing tests -----

        [Test]
        public void MessagesCreatedForRulesWithSlackUserId()
        {
            var jira = Substitute.For<IJiraService>();
            jira.GetIssuesForJql(Arg.Any<string>()).Returns(new[]
            {
                new Issue { Key = "T-1", Summary = "Test issue" }
            });

            var rule = new Preesta.Configuration.JqlRule
            {
                Jql = "any",
                Notification = new NotificationSpec
                {
                    Subject = "Alert",
                    RawRecipients = new[] { "admin@test.com" },
                    RawCc = new string[] { },
                    SlackUserIds = new[] { "U999ABC" }
                }
            };

            var supplier = new JqlSupplier(jira, new[] { rule }, Substitute.For<ILogger>());
            var converter = new IssuePackageConverter("http://jira");

            var packages = supplier.GetPackages()
                .Cast<Package<NotificationReaction, Issue>>()
                .ToArray();

            var slackMessages = converter.ToSlackMessages(packages, EmptyRedirector, EmptyMap);

            Assert.AreEqual(1, slackMessages.Length);
            Assert.AreEqual("U999ABC", slackMessages[0].To);
        }

        [Test]
        public void EmailMapsToSlackUserViaSlackUsers()
        {
            var jira = Substitute.For<IJiraService>();
            jira.GetIssuesForJql(Arg.Any<string>()).Returns(new[]
            {
                new Issue
                {
                    Key = "T-1",
                    Participants = new IssueParticipants { Assignee = new User { Email = "assignee@ex.com" } }
                }
            });

            var rule = new Preesta.Configuration.JqlRule
            {
                Jql = "any",
                Notification = new NotificationSpec
                {
                    Subject = "Alert",
                    RawRecipients = new[] { "assignee" },
                    RawCc = new string[] { }
                }
            };

            var slackUsers = new Dictionary<string, string>
            {
                { "assignee@ex.com", "U777" }
            };

            var supplier = new JqlSupplier(jira, new[] { rule }, Substitute.For<ILogger>());
            var converter = new IssuePackageConverter("http://jira");

            var packages = supplier.GetPackages()
                .Cast<Package<NotificationReaction, Issue>>()
                .ToArray();

            var messages = converter.ToSlackMessages(packages, Redirector.Empty, slackUsers);

            Assert.AreEqual(1, messages.Length);
            Assert.AreEqual("U777", messages[0].To);
        }

        [Test]
        public void SlackUserMapIsCaseInsensitive()
        {
            var jira = Substitute.For<IJiraService>();
            jira.GetIssuesForJql(Arg.Any<string>()).Returns(new[]
            {
                new Issue
                {
                    Key = "T-1",
                    Participants = new IssueParticipants { Assignee = new User { Email = "MiXeD@Ex.com" } }
                }
            });

            var rule = new Preesta.Configuration.JqlRule
            {
                Jql = "any",
                Notification = new NotificationSpec
                {
                    Subject = "Alert",
                    RawRecipients = new[] { "assignee" },
                    RawCc = new string[] { }
                }
            };

            var slackUsers = new Dictionary<string, string>
            {
                { "mixed@ex.com", "U555" }
            };

            var supplier = new JqlSupplier(jira, new[] { rule }, Substitute.For<ILogger>());
            var converter = new IssuePackageConverter("http://jira");

            var packages = supplier.GetPackages()
                .Cast<Package<NotificationReaction, Issue>>()
                .ToArray();

            var messages = converter.ToSlackMessages(packages, Redirector.Empty, slackUsers);

            Assert.AreEqual(1, messages.Length);
            Assert.AreEqual("U555", messages[0].To);
        }

        [Test]
        public void SameUserIdAcrossPackagesIsDeduplicated()
        {
            var jira = Substitute.For<IJiraService>();
            jira.GetIssuesForJql(Arg.Any<string>()).Returns(new[]
            {
                new Issue
                {
                    Key = "T-1",
                    Participants = new IssueParticipants { Assignee = new User { Email = "a@ex.com" } }
                },
                new Issue
                {
                    Key = "T-2",
                    Participants = new IssueParticipants { Assignee = new User { Email = "b@ex.com" } }
                }
            });

            var rule = new Preesta.Configuration.JqlRule
            {
                Jql = "any",
                Notification = new NotificationSpec
                {
                    Subject = "Alert",
                    RawRecipients = new[] { "assignee" },
                    RawCc = new string[] { }
                }
            };

            var slackUsers = new Dictionary<string, string>
            {
                { "a@ex.com", "U777" },
                { "b@ex.com", "U777" }
            };

            var supplier = new JqlSupplier(jira, new[] { rule }, Substitute.For<ILogger>());
            var converter = new IssuePackageConverter("http://jira");

            var packages = supplier.GetPackages()
                .Cast<Package<NotificationReaction, Issue>>()
                .ToArray();

            var messages = converter.ToSlackMessages(packages, Redirector.Empty, slackUsers);

            // Both packages collapse onto the single Slack user — exactly one message,
            // delivered once, even though two distinct Issues / two distinct Addressees.
            Assert.AreEqual(1, messages.Length);
            Assert.AreEqual("U777", messages[0].To);
        }

        // ----- Mrkdwn formatting tests -----

        [Test]
        public void MrkdwnFormatting_BoldKey_LinkInBrackets_StatusEmoji()
        {
            var package = new Package<NotificationReaction, Issue>
            {
                Reaction = new NotificationReaction
                {
                    Subject = "Alert",
                    Recommendations = "Please act"
                },
                Items = new[]
                {
                    new Issue
                    {
                        Key = "PRE-7",
                        Summary = "Refactor X",
                        Url = "https://linear.app/preesta-dev/issue/PRE-7",
                        Status = "In Progress",
                        Priority = "High",
                        Participants = new IssueParticipants
                        {
                            Assignee = new User { DisplayName = "Alice" }
                        }
                    }
                }
            };

            var mrkdwn = IssueFormatter.ToSlackMrkdwn(new[] { package }, "https://linear.app/preesta-dev/", "preesta-dev");

            // Bold + clickable: *<url|PRE-7>* (Slack mrkdwn link inside *...*)
            Assert.IsTrue(mrkdwn.Contains("*<https://linear.app/preesta-dev/issue/PRE-7|PRE-7>*"),
                $"Expected bold-clickable issue key. Got:\n{mrkdwn}");
            Assert.IsTrue(mrkdwn.Contains("Refactor X"), "Summary missing");
            Assert.IsTrue(mrkdwn.Contains(":hourglass_flowing_sand:"), "In Progress emoji missing");
            Assert.IsTrue(mrkdwn.Contains(":large_orange_circle:"), "High priority emoji missing");
            Assert.IsTrue(mrkdwn.Contains("Alice"), "Assignee missing");
            Assert.IsTrue(mrkdwn.Contains("Please act"), "Recommendations missing");
        }

        // ----- Pipeline integration -----

        [Test]
        public void NoSlackBotTokenSilentlySkipsSlackDispatch()
        {
            // Mirror of the DI path "no Slack:botToken → SlackMessenger stays null
            // → ReactionPipeline.Run() walks the email path normally". We verify
            // the pipeline-level invariant directly (without bringing up the full
            // DependencyContainer, which is internal and would try to read real
            // appsettings.yaml off disk).
            var jira = Substitute.For<IJiraService>();
            jira.GetIssuesForJql(Arg.Any<string>()).Returns(new[] { new Issue { Key = "T-1" } });

            var rule = new Preesta.Configuration.JqlRule
            {
                Jql = "any",
                Notification = new NotificationSpec
                {
                    Subject = "Alert",
                    RawRecipients = new[] { "admin" },
                    RawCc = new string[] { },
                    SlackUserIds = new[] { "U999" } // ← user-configured, but no SlackMessenger
                }
            };

            var supplier = new JqlSupplier(jira, new[] { rule }, Substitute.For<ILogger>());
            var emailMessenger = Substitute.For<IMessenger>();

            var pipe = new ReactionPipeline<Issue>(
                packageSupplier: supplier,
                packageConverter: new IssuePackageConverter("http://jira"),
                messenger: emailMessenger,
                slackMessenger: null,                                  // ← simulates empty botToken
                slackUserMap: new Dictionary<string, string>());

            Assert.DoesNotThrow(() => pipe.Run());
            // Email side still fires — Slack side simply never enters the dispatch block.
            emailMessenger.Received(1).SendAll(Arg.Any<IEnumerable<Message>>());
        }

        [Test]
        public void ReactionPipelineSendsSlackMessages()
        {
            var jira = Substitute.For<IJiraService>();
            jira.GetIssuesForJql(Arg.Any<string>()).Returns(new[]
            {
                new Issue { Key = "T-1" }
            });

            var rule = new Preesta.Configuration.JqlRule
            {
                Jql = "any",
                Notification = new NotificationSpec
                {
                    Subject = "Alert",
                    RawRecipients = new[] { "admin" },
                    RawCc = new string[] { },
                    SlackUserIds = new[] { "U999" }
                }
            };

            var supplier = new JqlSupplier(jira, new[] { rule }, Substitute.For<ILogger>());
            var emailMessenger = Substitute.For<IMessenger>();
            var slackMessenger = Substitute.For<IMessenger>();

            var pipe = new ReactionPipeline<Issue>
            {
                PackageSupplier = supplier,
                PackageConverter = new IssuePackageConverter("http://jira"),
                Messenger = emailMessenger,
                SlackMessenger = slackMessenger,
                HttpHandler = Substitute.For<IHttpHandler>()
            };

            pipe.Run();

            emailMessenger.Received(1).SendAll(Arg.Any<IEnumerable<Message>>());
            slackMessenger.Received(1).SendAll(Arg.Is<IEnumerable<Message>>(
                msgs => msgs.Count() == 1 && msgs.First().To == "U999"));
        }

        [Test]
        public void MrkdwnFormatting_FilterDescription_AsItalic()
        {
            var package = new Package<NotificationReaction, Issue>
            {
                Reaction = new NotificationReaction
                {
                    Subject = "Alert",
                    Recommendations = "Resolve please"
                },
                Items = new[]
                {
                    new Issue { Key = "PRE-1", Summary = "X", Status = "Todo" }
                }
            };
            // Simulate LinearIssueSupplier.Enrich populating LinearFilter on the package.
            package.Properties["LinearFilter"] = "issues assigned to me, not done";

            var mrkdwn = IssueFormatter.ToSlackMrkdwn(new[] { package }, "https://linear.app/preesta-dev/", "preesta-dev");

            // Italic via _..._ wrapping
            Assert.IsTrue(mrkdwn.Contains("_AI filter:"),
                $"Expected italicized filter description. Got:\n{mrkdwn}");
            Assert.IsTrue(mrkdwn.Contains("issues assigned to me, not done"),
                "Filter prompt content missing");
        }
    }
}
