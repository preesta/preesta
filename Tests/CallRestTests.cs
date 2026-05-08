using System.Collections.Generic;
using Preesta;
using Preesta.Configuration;
using Preesta.Configuration.Action;
using Preesta.Data;
using Preesta.Data.Supplying;
using Preesta.Data.Supplying.Convert;
using Preesta.Notification;
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
                Updates = new[]
                {
                    new SelfUpdateSpec {UrlPattern = "http://example.com"},
                    new SelfUpdateSpec {UrlPattern = "http://example.com"}
                }
            };

            var issuesSupplier = Substitute.For<IJiraService>();
            issuesSupplier
                .GetIssuesForJql(Arg.Any<string>())
                .Returns(new[] {new Issue()});

            var jqlSupplier = new JqlSupplier(issuesSupplier, new[] { rule }, Substitute.For<ILogger>());

            var httpHandler = Substitute.For<IHttpHandler>();

            var pipe = new ReactionPipeline<Issue>
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