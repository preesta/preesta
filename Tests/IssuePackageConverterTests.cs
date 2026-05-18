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

        [Test]
        public void IssueIdMarker_FallsBackThroughLinearGithubShortcut()
        {
            // The fallback chain LinearId ?? GithubNodeId ?? ShortcutId ?? ""
            // lets the same {{@issueId}} marker work across all three trackers.
            var converter = new IssuePackageConverter("http://example.com/");

            string Resolve(Issue issue)
            {
                var package = new Package<SelfUpdate, Issue>
                {
                    Reaction = new SelfUpdate
                    {
                        Verb = "POST",
                        UrlPattern = "http://example.com/x",
                        BodyPattern = "id={{@issueId}}"
                    },
                    Items = new[] { issue }
                };
                return converter.ToHttpRequests(new[] { package }).Single().Body;
            }

            Assert.AreEqual("id=L_abc", Resolve(new Issue { Key = "PRE-1", LinearId = "L_abc" }));
            Assert.AreEqual("id=GH_xyz", Resolve(new Issue { Key = "octo/repo#1", GithubNodeId = "GH_xyz" }));
            Assert.AreEqual("id=42", Resolve(new Issue { Key = "sc-42", ShortcutId = "42" }));
            // Linear wins over the others when several are populated.
            Assert.AreEqual("id=L_first", Resolve(new Issue
            {
                Key = "x",
                LinearId = "L_first",
                GithubNodeId = "GH_second",
                ShortcutId = "third"
            }));
            // Nothing populated → marker resolves to empty string (no exception).
            Assert.AreEqual("id=", Resolve(new Issue { Key = "x" }));
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
