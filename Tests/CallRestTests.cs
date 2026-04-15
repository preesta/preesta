using System.Collections.Generic;
using Bender;
using Bender.Configuration;
using Bender.Configuration.Action;
using Bender.Data;
using Bender.Data.Supplying;
using Bender.Data.Supplying.Convert;
using Bender.Notification;
using JiraRest;
using NSubstitute;
using NUnit.Framework;
using Serilog;
using System.Linq;

namespace Tests
{
    [TestFixture]
    public class CallRestTests
    {
        [Test]
        public void CheckMoreThanOneCallRestAreCalled()
        {
            // Setup
            var rule = new JqlRule
            {
                HowToUpdate = new[]
                {
                    new Update {UrlPattern = "http://example.com"},
                    new Update {UrlPattern = "http://example.com"}
                }
            };

            var issuesSupplier = Substitute.For<IJiraService>();
            issuesSupplier
                .GetIssuesForJql(Arg.Any<string>())
                .Returns(new[] {new Issue()});

            var jqlSupplier = new JqlSupplier(issuesSupplier, new[] { rule }, Substitute.For<ILogger>());

            var httpHandler = Substitute.For<IHttpHandler>();

            var pipe = new ReactionPipe<Issue>
            {
                PackageSupplier = jqlSupplier,
                PackageConverter = new IssuePackageConverter("http://jira"),
                HttpHandler = httpHandler
            };

            // Experiment
            pipe.Run();

            // Check result
            httpHandler.Received().HandleAll(Arg.Is<IEnumerable<HttpRequest>>(r => r.Count() == 2));
            
        }
    }
}