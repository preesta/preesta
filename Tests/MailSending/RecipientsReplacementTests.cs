using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Preesta.Configuration;
using Preesta.Data;
using Preesta.Data.Supplying;
using Preesta.Data.Supplying.Convert;
using Preesta.Extensions;
using Preesta.Notification;
using Messaging;
using NSubstitute;
using NUnit.Framework;
using Serilog;

namespace Tests.MailSending
{
    [TestFixture]
    public class RecipientsReplacementTests
    {
        private Preesta.IJiraService? _jiraService;
        private IRulesConfig? _rulesConfig;
        private ILogger? _logger;

        [SetUp]
        public void Initialize()
        {
            var xmlConfig = XDocument.Parse(
@"<configuration>
    <jqlRule group=""test"">
        <notify 
            subject=""test""
            mailTo=""Admin,assignee,creator""
            cc=""reporter""
            />
        <jql/>
    </jqlRule>

    <jqlRule group=""test-2"">
        <notify 
            subject=""test""
            mailTo=""Team_Claim,reporter""
            />            
        <jql/>
    </jqlRule>

    <jqlRule group=""supervisor-in-To"">
        <notify 
            subject=""""
            mailTo=""supervisor""
            />            
        <jql/>
    </jqlRule>

    <jqlRule group=""supervisor-in-Cc"">
        <notify 
            subject=""""
            mailTo=""anybody""
            cc=""supervisor""
            />            
        <jql/>
    </jqlRule>

    <jqlRule group=""test-for-faired-supervisor"">
        <notify 
            subject=""""
            mailTo=""anybody""
            />            
        <jql/>
    </jqlRule>

    <jqlRule group=""test-for-empty-addressers"">
        <notify 
            subject=""""
            mailTo=""""
            comment=""When neither To nor Cc attributes are set, the message should be send to supervisors or (if supervisor is not set) to maintainers""
            />            
        <jql/>
    </jqlRule>

    <jqlRule group=""expired-supervisor-in-addressees"">
        <notify 
            subject=""expired_supervisor""
            mailTo=""expired_supervisor""
            comment=""When neither To nor Cc attributes are set, the message should be send to supervisor""
            />            
        <jql/>
    </jqlRule>

    <redirection_rules>
        <rule from=""admin"" to=""administrator""/>
        <rule from=""TEAM_CLAIM"" to=""Zoldberg@express.ship,Amy_Wong@express.ship""/>
        <rule from=""expired_supervisor"" to=""Hubert_Farnsworth@express.ship""/>
    </redirection_rules>
</configuration>
"
            );

            _rulesConfig = new XmlRulesConfig(xmlConfig, Substitute.For<ILogger>());

            var jiraMock = Substitute.For<Preesta.IJiraService>();
            jiraMock
                .GetIssuesForJql(Arg.Any<string>())
                .Returns(
                    new[]
                    {
                        new Issue
                        {
                            FixVersions = new[] {"1"},
                            AffectsVersions = new string[] {},
                            Staff = new IssueStaff
                                    {
                                        Assignee = new User {Email = "assignee@express.ship"},
                                        Reporter = new User {Email = "reporter@express.ship"},
                                        Creator = new User {Email = "creator@express.ship"}
                                    }
                        }
                    }
                );

            jiraMock.GetBuilds(Arg.Any<string>()).Returns(new Build[] { });
            jiraMock.GetIssueById(Arg.Any<string>()).Returns(new Issue());

            _jiraService = jiraMock;

            _logger = Substitute.For<ILogger>();
        }



        [Test]
        public void JqlSupplierReplaceMarkersByRealAddresses()
        {
            IPackageSupplier packageSupplier = new JqlSupplier(_jiraService!, _rulesConfig!.GetJqlRules("test"), _logger!);
            var message = new IssuePackageConverter("https://jira.express.ship/jira/")
                .ToMessages(packageSupplier.GetPackages().Cast<Package<SendsNotification, Issue>>())
                .Redirect(new Redirector(_rulesConfig.GetRedirectionMap(), Enumerable.Empty<string>(), Enumerable.Empty<string>()))
                .Single();

            Assert.AreEqual("administrator,assignee@express.ship,creator@express.ship", message.To);
            Assert.AreEqual("reporter@express.ship", message.Cc);
        }

        [Test]
        public void RedirectionRulesAreCaseInsensitive()
        {

            IPackageSupplier packageSupplier = new JqlSupplier(_jiraService!, _rulesConfig!.GetJqlRules("test-2"), _logger!);
            var message = new IssuePackageConverter("https://jira.express.ship/jira/")
                .ToMessages(packageSupplier.GetPackages().Cast<Package<SendsNotification, Issue>>())
                .Redirect(new Redirector(_rulesConfig.GetRedirectionMap(), Enumerable.Empty<string>(), Enumerable.Empty<string>()))
                .Single();

            Assert.AreEqual("reporter@express.ship,Zoldberg@express.ship,Amy_Wong@express.ship", message.To);
        }

        [Test]
        public void CheckThatSupervisorIsNotReplacedEvenIfSheIsInRedirectionMap()
        {
            Message? message = null;
            var messenger = Substitute.For<IMessenger>();
            messenger.SendAll(Arg.Do<IEnumerable<Message>>(m => message = m.Single()));
            
            var pipe = new ReactionPipe<Issue>()
            {
                PackageSupplier = new JqlSupplier(_jiraService!, _rulesConfig!.GetJqlRules("test-for-faired-supervisor"), _logger!),
                PackageConverter = new IssuePackageConverter("https://jira.example.com"),
                Redirector = new Redirector(_rulesConfig.GetRedirectionMap(), new[] { "expired_supervisor" }, Enumerable.Empty<string>()),
                Messenger = messenger
                    
            };

            pipe.Run();

            Assert.AreEqual("anybody", message?.To);
            Assert.AreEqual("expired_supervisor", message?.Cc);
        }

        [Test]
        public void CheckThatSupervisorIsNotDuplicatedInCcIfSheIsInTo()
        {
            Message? message = null;
            var messenger = Substitute.For<IMessenger>();
            messenger.SendAll(Arg.Do<IEnumerable<Message>>(m => message = m.Single()));

            var pipe = new ReactionPipe<Issue>()
            {
                PackageSupplier = new JqlSupplier(_jiraService!, _rulesConfig!.GetJqlRules("supervisor-in-To"), _logger!),
                PackageConverter = new IssuePackageConverter("https://jira.example.com"),
                Redirector = new Redirector(_rulesConfig.GetRedirectionMap(), new[] { "supervisor" }, Enumerable.Empty<string>()),
                Messenger = messenger
            };

            pipe.Run();

            Assert.AreEqual("supervisor", message!.To);
            Assert.AreEqual(string.Empty, message!.Cc);
        }

        [Test]
        public void CheckThatSupervisorInCcIsNotDuplicated()
        {
            Message? message = null;
            var messenger = Substitute.For<IMessenger>();
            messenger.SendAll(Arg.Do<IEnumerable<Message>>(m => message = m.Single()));

            var pipe = new ReactionPipe<Issue>()
            {
                PackageSupplier = new JqlSupplier(_jiraService!, _rulesConfig!.GetJqlRules("supervisor-in-Cc"), _logger!),
                PackageConverter = new IssuePackageConverter("https://jira.example.com"),
                Redirector = new Redirector(_rulesConfig.GetRedirectionMap(), new[] { "supervisor" }, Enumerable.Empty<string>()),
                Messenger = messenger
            };

            pipe.Run();

            Assert.AreEqual("anybody", message?.To);
            Assert.AreEqual("supervisor", message?.Cc);
        }

        [Test, Description("When there are no any main addressers, notifications are sent to Supervisors")]
        public void CheckThatSupervisorIsInToFieldWhenThereAreNoOtherAddressees()
        {
            Message? message = null;
            var messenger = Substitute.For<IMessenger>();
            messenger.SendAll(Arg.Do<IEnumerable<Message>>(m => message = m.Single()));

            var pipe = new ReactionPipe<Issue>()
            {
                PackageSupplier = new JqlSupplier(_jiraService!, _rulesConfig!.GetJqlRules("test-for-empty-addressers"), _logger!),
                PackageConverter = new IssuePackageConverter("https://jira.example.com"),
                Redirector = new Redirector(_rulesConfig.GetRedirectionMap(), new[] { "supervisor" }, Enumerable.Empty<string>()),
                Messenger = messenger
            };

            pipe.Run();

            Assert.AreEqual("supervisor", message!.To);
            Assert.AreEqual(string.Empty, message!.Cc);
        }


        [Test, Description("When Superviser is not configured, messages are sent to Maintainers")]
        public void CheckThatMessagesSentToMaintainerWhenSupervisorIsAbsent()
        {
            Message? message = null;
            var messenger = Substitute.For<IMessenger>();
            messenger.SendAll(Arg.Do<IEnumerable<Message>>(m => message = m.Single()));

            var pipe = new ReactionPipe<Issue>()
            {
                PackageSupplier = new JqlSupplier(_jiraService!, _rulesConfig!.GetJqlRules("test-for-empty-addressers"), _logger!),
                PackageConverter = new IssuePackageConverter("https://jira.example.com"),
                Redirector = new Redirector(_rulesConfig.GetRedirectionMap(), Enumerable.Empty<string>(), new[] { "maintainer" }),
                Messenger = messenger
            };

            pipe.Run();

            Assert.AreEqual("maintainer", message?.To);
            Assert.AreEqual(string.Empty, message?.Cc);
        }

        [Test]
        public void ExpiredSupervisorInAddressees()
        {
            Message? message = null;
            var messenger = Substitute.For<IMessenger>();
            messenger.SendAll(Arg.Do<IEnumerable<Message>>(m => message = m.Single()));

            var pipe = new ReactionPipe<Issue>()
            {
                PackageSupplier = new JqlSupplier(_jiraService!, _rulesConfig!.GetJqlRules("expired-supervisor-in-addressees"), _logger!),
                PackageConverter = new IssuePackageConverter("https://jira.example.com"),
                Redirector = new Redirector(_rulesConfig.GetRedirectionMap(), new[] { "expired_supervisor" }, Enumerable.Empty<string>()),
                Messenger = messenger
            };

            pipe.Run();

            Assert.AreEqual("Hubert_Farnsworth@express.ship", message!.To);
            Assert.AreEqual("expired_supervisor", message!.Cc);
        }
    }
}
