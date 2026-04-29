using System;
using System.Linq;
using Messaging;
using Preesta;
using Preesta.Configuration.Action;
using Preesta.Data;
using Preesta.Data.Supplying;
using Preesta.Data.Supplying.Convert;
using NSubstitute;
using NUnit.Framework;
using Serilog;

namespace Tests.Formatting
{
    [TestFixture]
    public class IssueFormatterTests
    {
        private static IJiraService JiraReturning(params Issue[] issues)
        {
            var jira = Substitute.For<IJiraService>();
            jira.GetIssuesForJql(Arg.Any<string>()).Returns(issues);
            return jira;
        }

        private static Message[] Render(Notify notify, params Issue[] issues)
        {
            var rule = new Preesta.Configuration.JqlRule { Jql = "any", HowToNotify = notify };
            var supplier = new JqlSupplier(JiraReturning(issues), new[] { rule }, Substitute.For<ILogger>());
            var converter = new IssuePackageConverter("http://jira", subjectPrefix: "");
            var packages = supplier.GetPackages().Cast<Package<SendsNotification, Issue>>().ToArray();
            return converter.ToMessages(packages);
        }

        private static Notify NotifyWith(string[]? columns = null) => new Notify
        {
            Subject = "T",
            MetaAddressers = new[] { "a@x" },
            MetaCarbonCopy = new string[] { },
            Columns = columns
        };

        [Test]
        public void DefaultColumnsAppearWhenNoneSpecified()
        {
            var html = Render(NotifyWith(), new Issue { Key = "T-1", Summary = "S" })[0].Body;

            Assert.IsTrue(html.Contains(">Type<"));
            Assert.IsTrue(html.Contains(">Key<"));
            Assert.IsTrue(html.Contains(">Summary<"));
            Assert.IsTrue(html.Contains(">Assignee<"));
            Assert.IsTrue(html.Contains(">Status<"));
            Assert.IsTrue(html.Contains(">Priority<"));
            Assert.IsFalse(html.Contains(">Components<"));
            Assert.IsFalse(html.Contains(">Build Found<"));
            Assert.IsFalse(html.Contains(">Time Spent (hrs)<"));
        }

        [Test]
        public void CustomColumnsReplaceDefault()
        {
            var html = Render(NotifyWith(new[] { "Key", "Summary", "Build Found" }),
                              new Issue { Key = "T-1", Summary = "S", BuildFound = new[] { "1.2.3" } })[0].Body;

            Assert.IsTrue(html.Contains(">Key<"));
            Assert.IsTrue(html.Contains(">Summary<"));
            Assert.IsTrue(html.Contains(">Build Found<"));
            Assert.IsFalse(html.Contains(">Assignee<"));
            Assert.IsFalse(html.Contains(">Priority<"));
            Assert.IsTrue(html.Contains("1.2.3"));
        }

        [Test]
        public void UnknownColumnsAreIgnored()
        {
            var html = Render(NotifyWith(new[] { "Key", "ThisColumnDoesNotExist", "Summary" }),
                              new Issue { Key = "T-1", Summary = "S" })[0].Body;

            Assert.IsTrue(html.Contains(">Key<"));
            Assert.IsTrue(html.Contains(">Summary<"));
            Assert.IsFalse(html.Contains("ThisColumnDoesNotExist"));
        }

        [Test]
        public void IssueOrderIsPreservedFromSource()
        {
            var html = Render(NotifyWith(),
                new Issue { Key = "T-FIRST", Summary = "a" },
                new Issue { Key = "T-SECOND", Summary = "b" },
                new Issue { Key = "T-THIRD", Summary = "c" }
            )[0].Body;

            int p1 = html.IndexOf(">T-FIRST<", StringComparison.Ordinal);
            int p2 = html.IndexOf(">T-SECOND<", StringComparison.Ordinal);
            int p3 = html.IndexOf(">T-THIRD<", StringComparison.Ordinal);

            Assert.Less(p1, p2);
            Assert.Less(p2, p3);
        }

        [Test]
        public void PriorityIconAppearsForKnownLevels()
        {
            var html = Render(NotifyWith(new[] { "Key", "Priority" }),
                new Issue { Key = "T-1", Summary = "x", Priority = "Highest" })[0].Body;

            Assert.IsTrue(html.Contains("🔴"));
            Assert.IsTrue(html.Contains("Highest"));
        }

        [Test]
        public void FooterContainsPreestaSignature()
        {
            var html = Render(NotifyWith(), new Issue { Key = "T-1", Summary = "x" })[0].Body;

            Assert.IsTrue(html.Contains("Sent by Preesta"));
        }

        [Test]
        public void HtmlIsWrappedInResponsiveContainer()
        {
            var html = Render(NotifyWith(), new Issue { Key = "T-1", Summary = "x" })[0].Body;

            Assert.IsTrue(html.Contains("max-width:640px"));
            Assert.IsTrue(html.Contains("overflow-x:auto"));
        }
    }
}
