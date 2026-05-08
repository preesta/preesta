using System.Collections.Generic;
using System.Linq;
using Preesta.Configuration.Action;
using Preesta.Data;
using Preesta.Data.Supplying;
using Preesta.Data.Supplying.Convert;
using Preesta.Notification;
using NUnit.Framework;

namespace Tests
{
    [TestFixture]
    public class IssuePackageConverterTests
    {
        [TestCase("http://jira.example.com")]
        [TestCase("http://jira.example.com/")]
        public void RootUri_NormalizedToTrailingSlash(string configuredRootUri)
        {
            var package = MakePackageWithUpdate(
                "{{@jiraRoot}}rest/api/2/issue/{{@issueKey}}/transitions");
            var converter = new IssuePackageConverter(configuredRootUri);

            var url = converter.ToHttpRequests(new[] { package }).Single().Uri.ToString();

            Assert.AreEqual("http://jira.example.com/rest/api/2/issue/T-1/transitions", url);
        }

        [Test]
        public void RootUri_BothFormsProduceIdenticalRequests()
        {
            var package = MakePackageWithUpdate(
                "{{@jiraRoot}}rest/api/2/issue/{{@issueKey}}");

            var withSlash    = new IssuePackageConverter("http://jira.example.com/");
            var withoutSlash = new IssuePackageConverter("http://jira.example.com");

            var a = withSlash   .ToHttpRequests(new[] { package }).Single().Uri.ToString();
            var b = withoutSlash.ToHttpRequests(new[] { package }).Single().Uri.ToString();

            Assert.AreEqual(a, b);
        }

        private static Package<SelfUpdate, Issue> MakePackageWithUpdate(string urlPattern) =>
            new Package<SelfUpdate, Issue>
            {
                Reaction = new SelfUpdate
                {
                    Verb = "PUT",
                    UrlPattern = urlPattern,
                    BodyPattern = "{}"
                },
                Items = new[] { new Issue { Key = "T-1" } }
            };
    }
}
