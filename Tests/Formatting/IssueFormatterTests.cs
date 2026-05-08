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
        public void DefaultMetaShowsStatusPriorityAssignee()
        {
            var html = Render(NotifyWith(), new Issue
            {
                Key = "T-1",
                Summary = "S",
                Status = "To Do",
                Priority = "Medium",
                Staff = new IssueStaff { Assignee = new User { DisplayName = "Alice" } }
            })[0].Body;

            Assert.IsTrue(html.Contains(">To Do<"), "Status pill missing");
            Assert.IsTrue(html.Contains(">Medium"), "Priority label missing");
            Assert.IsTrue(html.Contains("Alice"), "Assignee missing");
        }

        [Test]
        public void CustomColumnsReplaceDefaultMeta()
        {
            var html = Render(
                NotifyWith(new[] { "Type", "Affects Versions" }),
                new Issue
                {
                    Key = "T-1",
                    Summary = "S",
                    Type = "Bug",
                    AffectsVersions = new[] { "1.2.3" },
                    Status = "To Do",
                    Priority = "Medium"
                })[0].Body;

            Assert.IsTrue(html.Contains(">Bug<"), "Type pill missing");
            Assert.IsTrue(html.Contains("Affects 1.2.3"), "Affects Versions missing");
            Assert.IsFalse(html.Contains(">To Do<"), "Status should be hidden");
            Assert.IsFalse(html.Contains(">Medium"), "Priority should be hidden");
        }

        [Test]
        public void UnknownColumnsAreIgnored()
        {
            var html = Render(
                NotifyWith(new[] { "Status", "ThisColumnDoesNotExist" }),
                new Issue { Key = "T-1", Summary = "S", Status = "To Do" })[0].Body;

            Assert.IsTrue(html.Contains(">To Do<"));
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

            int p1 = html.IndexOf(">T-FIRST<", System.StringComparison.Ordinal);
            int p2 = html.IndexOf(">T-SECOND<", System.StringComparison.Ordinal);
            int p3 = html.IndexOf(">T-THIRD<", System.StringComparison.Ordinal);

            Assert.Less(p1, p2);
            Assert.Less(p2, p3);
        }

        [Test]
        public void PriorityRendersWithDot()
        {
            var html = Render(NotifyWith(new[] { "Priority" }),
                new Issue { Key = "T-1", Summary = "x", Priority = "Highest" })[0].Body;

            Assert.IsTrue(html.Contains("background:#DE350B"), "Highest priority dot color");
            Assert.IsTrue(html.Contains("Highest"));
        }

        [Test]
        public void StatusRendersWithProgressColor()
        {
            var html = Render(NotifyWith(new[] { "Status" }),
                new Issue { Key = "T-1", Summary = "x", Status = "In Progress" })[0].Body;

            Assert.IsTrue(html.Contains("background:#DEEBFF"), "Progress pill bg");
            Assert.IsTrue(html.Contains(">In Progress<"));
        }

        [Test]
        public void HeaderColumnsKeyAndSummaryAreNotMovedToMeta()
        {
            var html = Render(NotifyWith(new[] { "Key", "Summary", "Status" }),
                new Issue { Key = "T-1", Summary = "Hello", Status = "Done" })[0].Body;

            Assert.IsTrue(html.Contains("T-1"), "Key still rendered as link");
            Assert.IsTrue(html.Contains(">Done<"), "Status still in meta");
            // Key/Summary as columns should be ignored, not duplicated in meta
            int keyOccurrences = System.Text.RegularExpressions.Regex.Matches(html, ">T-1<").Count;
            Assert.AreEqual(1, keyOccurrences, "Key shown only in header, not duplicated in meta");
        }

        [Test]
        public void FooterContainsPreestaSignature()
        {
            var html = Render(NotifyWith(), new Issue { Key = "T-1", Summary = "x" })[0].Body;
            Assert.IsTrue(html.Contains("Sent by Preesta"));
        }

        [Test]
        public void AllNonEmptyExpandsToPopulatedFieldsOnly()
        {
            var html = Render(NotifyWith(new[] { "all-non-empty" }), new Issue
            {
                Key = "T-1",
                Summary = "Demo",
                Type = "Bug",
                Status = "In Progress",
                Priority = "High",
                Resolution = null,
                Components = "Frontend",
                Labels = "regression",
                ProjectKey = "SCRUM",
                Staff = new IssueStaff { Assignee = new User { DisplayName = "Alice" } }
            })[0].Body;

            // populated fields show up
            Assert.IsTrue(html.Contains(">In Progress<"));
            Assert.IsTrue(html.Contains(">High"));
            Assert.IsTrue(html.Contains(">Bug<"));
            Assert.IsTrue(html.Contains("Alice"));
            Assert.IsTrue(html.Contains("Frontend"));
            Assert.IsTrue(html.Contains("regression"));
            Assert.IsTrue(html.Contains("SCRUM"));
            // empty fields are silently skipped
            Assert.IsFalse(html.Contains("Resolution:"));
            Assert.IsFalse(html.Contains("Due "));
            Assert.IsFalse(html.Contains("Updated "));
        }

        [Test]
        public void HtmlIsWrappedInResponsiveContainer()
        {
            var html = Render(NotifyWith(), new Issue { Key = "T-1", Summary = "x" })[0].Body;

            Assert.IsTrue(html.Contains("max-width:640px"));
        }
    }
}
