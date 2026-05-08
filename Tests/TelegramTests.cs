using System.Collections.Generic;
using System.Linq;
using Messaging;
using Preesta;
using Preesta.Configuration.Action;
using Preesta.Data;
using Preesta.Data.Supplying;
using Preesta.Data.Supplying.Convert;
using Preesta.Notification;
using NSubstitute;
using NUnit.Framework;
using Serilog;
using Tests.Mocks;

namespace Tests
{
    [TestFixture]
    public class TelegramTests
    {
        private static Redirector EmptyRedirector => Redirector.Empty;
        private static IReadOnlyDictionary<string, string> EmptyMap => new Dictionary<string, string>();

        [Test]
        public void TelegramMessagesCreatedForRulesWithChatId()
        {
            const string issuesJson = @"{
                    ""issues"": [{
                        ""key"": ""TEST-1"",
                        ""fields"": {
                            ""status"": {},
                            ""issuetype"": { ""name"": ""Bug"" },
                            ""created"": ""2024-01-01T00:00:00.000+0000"",
                            ""summary"": ""Test issue""
                        }
                    }]
                }";

            using var server = new MockJiraServer();
            server.StubGetIssuesByJql("any", issuesJson);

            var connection = new JiraRest.Connection(server.Url, "any", "any");
            var svc = new HttpJiraService(server.Url, string.Empty, string.Empty)
            {
                Connection = connection
            };

            var rule = new Preesta.Configuration.JqlRule
            {
                Jql = "any",
                Notification = new NotificationSpec
                {
                    Subject = "Alert",
                    RawRecipients = new[] { "admin@test.com" },
                    RawCc = new string[] { },
                    TelegramChatIds = new[] { "-1001234567890" }
                }
            };

            var supplier = new JqlSupplier(svc, new[] { rule }, Substitute.For<ILogger>());
            var converter = new IssuePackageConverter("http://jira");

            var packages = supplier.GetPackages()
                .Cast<Package<NotificationReaction, Issue>>()
                .ToArray();

            var telegramMessages = converter.ToTelegramMessages(packages, EmptyRedirector, EmptyMap);

            Assert.AreEqual(1, telegramMessages.Length);
            Assert.AreEqual("-1001234567890", telegramMessages[0].To);
            Assert.IsTrue(telegramMessages[0].TextBody.Contains("TEST-1"));
            Assert.IsTrue(telegramMessages[0].TextBody.Contains("Test issue"));
        }

        [Test]
        public void NoTelegramMessagesWhenNoChatId()
        {
            var rule = new Preesta.Configuration.JqlRule
            {
                Jql = "any",
                Notification = new NotificationSpec
                {
                    Subject = "Alert",
                    RawRecipients = new[] { "admin@test.com" },
                    RawCc = new string[] { }
                }
            };

            var jira = Substitute.For<IJiraService>();
            jira.GetIssuesForJql(Arg.Any<string>()).Returns(new[] { new Issue { Key = "T-1" } });

            var supplier = new JqlSupplier(jira, new[] { rule }, Substitute.For<ILogger>());
            var converter = new IssuePackageConverter("http://jira");

            var packages = supplier.GetPackages()
                .Cast<Package<NotificationReaction, Issue>>()
                .ToArray();

            var telegramMessages = converter.ToTelegramMessages(packages, EmptyRedirector, EmptyMap);
            Assert.AreEqual(0, telegramMessages.Length);
        }

        [Test]
        public void EmailMessagesHaveTextBody()
        {
            var jira = Substitute.For<IJiraService>();
            jira.GetIssuesForJql(Arg.Any<string>()).Returns(new[]
            {
                new Issue { Key = "T-1", Summary = "Test" }
            });

            var rule = new Preesta.Configuration.JqlRule
            {
                Jql = "any",
                Notification = new NotificationSpec
                {
                    Subject = "Alert",
                    RawRecipients = new[] { "admin@test.com" },
                    RawCc = new string[] { }
                }
            };

            var supplier = new JqlSupplier(jira, new[] { rule }, Substitute.For<ILogger>());
            var converter = new IssuePackageConverter("http://jira");

            var packages = supplier.GetPackages()
                .Cast<Package<NotificationReaction, Issue>>()
                .ToArray();

            var messages = converter.ToMessages(packages);
            Assert.AreEqual(1, messages.Length);
            Assert.IsTrue(messages[0].Body.Contains("max-width:640px"));
            Assert.IsTrue(messages[0].TextBody.Contains("T-1"));
            Assert.IsFalse(messages[0].TextBody.Contains("max-width:640px"));
        }

        [Test]
        public void ReactionPipelineSendsTelegramMessages()
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
                    TelegramChatIds = new[] { "-100" }
                }
            };

            var supplier = new JqlSupplier(jira, new[] { rule }, Substitute.For<ILogger>());
            var emailMessenger = Substitute.For<IMessenger>();
            var telegramMessenger = Substitute.For<IMessenger>();

            var pipe = new ReactionPipeline<Issue>
            {
                PackageSupplier = supplier,
                PackageConverter = new IssuePackageConverter("http://jira"),
                Messenger = emailMessenger,
                TelegramMessenger = telegramMessenger,
                HttpHandler = Substitute.For<IHttpHandler>()
            };

            pipe.Run();

            emailMessenger.Received(1).SendAll(Arg.Any<IEnumerable<Message>>());
            telegramMessenger.Received(1).SendAll(Arg.Is<IEnumerable<Message>>(
                msgs => msgs.Count() == 1 && msgs.First().To == "-100"));
        }

        [Test]
        public void RedirectionRulesExpandToMultipleTelegramChatIds()
        {
            var jira = Substitute.For<IJiraService>();
            jira.GetIssuesForJql(Arg.Any<string>()).Returns(new[] { new Issue { Key = "T-1" } });

            var rule = new Preesta.Configuration.JqlRule
            {
                Jql = "any",
                Notification = new NotificationSpec
                {
                    Subject = "Alert",
                    RawRecipients = new[] { "managers" },
                    RawCc = new string[] { }
                }
            };

            var redirector = new Redirector(
                new Dictionary<string, string>
                {
                    { "managers", "ivanov@ex.com,petrov@ex.com,sidorov@ex.com" }
                },
                Enumerable.Empty<string>(),
                Enumerable.Empty<string>());

            var telegramUsers = new Dictionary<string, string>
            {
                { "ivanov@ex.com", "111" },
                { "petrov@ex.com", "222" },
                { "sidorov@ex.com", "333" }
            };

            var supplier = new JqlSupplier(jira, new[] { rule }, Substitute.For<ILogger>());
            var converter = new IssuePackageConverter("http://jira");

            var packages = supplier.GetPackages()
                .Cast<Package<NotificationReaction, Issue>>()
                .ToArray();

            var messages = converter.ToTelegramMessages(packages, redirector, telegramUsers);

            CollectionAssert.AreEquivalent(new[] { "111", "222", "333" },
                messages.Select(m => m.To).ToArray());
        }

        [Test]
        public void AssigneeMarkerResolvesToTelegramChatId()
        {
            var jira = Substitute.For<IJiraService>();
            jira.GetIssuesForJql(Arg.Any<string>()).Returns(new[]
            {
                new Issue
                {
                    Key = "T-1",
                    Participants = new IssueParticipants
                    {
                        Assignee = new User { Email = "assignee@ex.com" }
                    }
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

            var telegramUsers = new Dictionary<string, string>
            {
                { "assignee@ex.com", "999" }
            };

            var supplier = new JqlSupplier(jira, new[] { rule }, Substitute.For<ILogger>());
            var converter = new IssuePackageConverter("http://jira");

            var packages = supplier.GetPackages()
                .Cast<Package<NotificationReaction, Issue>>()
                .ToArray();

            var messages = converter.ToTelegramMessages(packages, Redirector.Empty, telegramUsers);

            Assert.AreEqual(1, messages.Length);
            Assert.AreEqual("999", messages[0].To);
        }

        [Test]
        public void EmailWithoutMappingProducesNoTelegramMessage()
        {
            var jira = Substitute.For<IJiraService>();
            jira.GetIssuesForJql(Arg.Any<string>()).Returns(new[] { new Issue { Key = "T-1" } });

            var rule = new Preesta.Configuration.JqlRule
            {
                Jql = "any",
                Notification = new NotificationSpec
                {
                    Subject = "Alert",
                    RawRecipients = new[] { "stranger@ex.com" },
                    RawCc = new string[] { }
                }
            };

            var supplier = new JqlSupplier(jira, new[] { rule }, Substitute.For<ILogger>());
            var converter = new IssuePackageConverter("http://jira");

            var packages = supplier.GetPackages()
                .Cast<Package<NotificationReaction, Issue>>()
                .ToArray();

            var messages = converter.ToTelegramMessages(packages, Redirector.Empty, EmptyMap);

            Assert.AreEqual(0, messages.Length);
        }

        [Test]
        public void StaticChatIdAndMappedRecipientCoexist()
        {
            var jira = Substitute.For<IJiraService>();
            jira.GetIssuesForJql(Arg.Any<string>()).Returns(new[]
            {
                new Issue
                {
                    Key = "T-1",
                    Participants = new IssueParticipants { Assignee = new User { Email = "a@ex.com" } }
                }
            });

            var rule = new Preesta.Configuration.JqlRule
            {
                Jql = "any",
                Notification = new NotificationSpec
                {
                    Subject = "Alert",
                    RawRecipients = new[] { "assignee" },
                    RawCc = new string[] { },
                    TelegramChatIds = new[] { "-1001" }
                }
            };

            var telegramUsers = new Dictionary<string, string>
            {
                { "a@ex.com", "777" }
            };

            var supplier = new JqlSupplier(jira, new[] { rule }, Substitute.For<ILogger>());
            var converter = new IssuePackageConverter("http://jira");

            var packages = supplier.GetPackages()
                .Cast<Package<NotificationReaction, Issue>>()
                .ToArray();

            var messages = converter.ToTelegramMessages(packages, Redirector.Empty, telegramUsers);

            CollectionAssert.AreEquivalent(new[] { "777", "-1001" },
                messages.Select(m => m.To).ToArray());
        }

        [Test]
        public void TelegramUserMapIsCaseInsensitive()
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

            var telegramUsers = new Dictionary<string, string>
            {
                { "mixed@ex.com", "555" }
            };

            var supplier = new JqlSupplier(jira, new[] { rule }, Substitute.For<ILogger>());
            var converter = new IssuePackageConverter("http://jira");

            var packages = supplier.GetPackages()
                .Cast<Package<NotificationReaction, Issue>>()
                .ToArray();

            var messages = converter.ToTelegramMessages(packages, Redirector.Empty, telegramUsers);

            Assert.AreEqual(1, messages.Length);
            Assert.AreEqual("555", messages[0].To);
        }

        [Test]
        public void SameChatIdAcrossPackagesIsDeduplicatedAndContentMerged()
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

            var telegramUsers = new Dictionary<string, string>
            {
                { "a@ex.com", "777" },
                { "b@ex.com", "777" }
            };

            var supplier = new JqlSupplier(jira, new[] { rule }, Substitute.For<ILogger>());
            var converter = new IssuePackageConverter("http://jira");

            var packages = supplier.GetPackages()
                .Cast<Package<NotificationReaction, Issue>>()
                .ToArray();

            var messages = converter.ToTelegramMessages(packages, Redirector.Empty, telegramUsers);

            Assert.AreEqual(1, messages.Length);
            Assert.AreEqual("777", messages[0].To);
            Assert.IsTrue(messages[0].TextBody.Contains("T-1"));
            Assert.IsTrue(messages[0].TextBody.Contains("T-2"));
        }
    }
}
