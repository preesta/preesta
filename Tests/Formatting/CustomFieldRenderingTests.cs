using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Preesta.Data;
using Preesta.Data.Supplying;
using Preesta.Formatting;
using Preesta.Notification;

namespace Tests.Formatting
{
    /// <summary>
    /// IssueFormatter renders Jira custom-field columns via the optional
    /// customFields display-name → internal-id map. Linear-sourced issues
    /// have empty CustomFields and render nothing, harmlessly.
    /// </summary>
    [TestFixture]
    public class CustomFieldRenderingTests
    {
        private static Package<NotificationReaction, Issue>[] OneIssueWithColumn(string column, Issue issue) =>
            new[] {
                new Package<NotificationReaction, Issue>
                {
                    Reaction = new NotificationReaction { Subject = "T", Columns = new[] { column } },
                    Items = new[] { issue }
                }
            };

        private static Issue WithCustomField(string id, JToken? value) => new Issue
        {
            Key = "T-1",
            Summary = "S",
            CreatedDate = new DateTime(2026, 5, 1),
            CustomFields = new Dictionary<string, JToken?> { [id] = value }
        };

        private static IReadOnlyDictionary<string, string> Map(params (string name, string id)[] entries)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (n, i) in entries) d[n] = i;
            return d;
        }

        [Test]
        public void ScalarString_RenderedAsText()
        {
            var html = IssueFormatter.ToHtml(
                OneIssueWithColumn("Severity", WithCustomField("customfield_10001", "High")),
                "http://jira", customFields: Map(("Severity", "customfield_10001")));

            StringAssert.Contains("Severity: High", html);
        }

        [Test]
        public void Number_RenderedAsText()
        {
            var html = IssueFormatter.ToHtml(
                OneIssueWithColumn("Story Points", WithCustomField("customfield_10002", new JValue(5))),
                "http://jira", customFields: Map(("Story Points", "customfield_10002")));

            StringAssert.Contains("Story Points: 5", html);
        }

        [Test]
        public void JArrayOfStrings_CommaJoined()
        {
            var arr = new JArray("alpha", "beta", "gamma");
            var html = IssueFormatter.ToHtml(
                OneIssueWithColumn("Tags", WithCustomField("customfield_10003", arr)),
                "http://jira", customFields: Map(("Tags", "customfield_10003")));

            StringAssert.Contains("Tags: alpha, beta, gamma", html);
        }

        [Test]
        public void JArrayOfObjectsWithName_NamesCommaJoined()
        {
            var arr = new JArray(
                new JObject { ["name"] = "Backend" },
                new JObject { ["name"] = "Auth" });
            var html = IssueFormatter.ToHtml(
                OneIssueWithColumn("Teams", WithCustomField("customfield_10004", arr)),
                "http://jira", customFields: Map(("Teams", "customfield_10004")));

            StringAssert.Contains("Teams: Backend, Auth", html);
        }

        [Test]
        public void SingleSelectJObjectWithValue_ValueRendered()
        {
            var obj = new JObject { ["value"] = "Engineering", ["id"] = "10100" };
            var html = IssueFormatter.ToHtml(
                OneIssueWithColumn("Department", WithCustomField("customfield_10005", obj)),
                "http://jira", customFields: Map(("Department", "customfield_10005")));

            StringAssert.Contains("Department: Engineering", html);
            StringAssert.DoesNotContain("10100", html);
        }

        [Test]
        public void MissingCustomFieldOrEmptyMap_RendersEmpty_NoCrash()
        {
            // Issue has no CustomField with this id, OR map doesn't know the column —
            // both branches should yield no chip and no exception.
            var html1 = IssueFormatter.ToHtml(
                OneIssueWithColumn("Severity", new Issue { Key = "T-1", Summary = "S", CreatedDate = new DateTime(2026,5,1) }),
                "http://jira", customFields: Map(("Severity", "customfield_10001")));

            var html2 = IssueFormatter.ToHtml(
                OneIssueWithColumn("UnknownColumn", WithCustomField("customfield_10001", "value")),
                "http://jira", customFields: null);

            // Severity has no value in this issue — label shouldn't appear in HTML
            // (the chip filter drops empty strings before rendering).
            StringAssert.DoesNotContain("Severity:", html1);
            StringAssert.DoesNotContain("value", html2);
        }

        [Test]
        public void CaseInsensitive_ColumnNameResolves()
        {
            var html = IssueFormatter.ToHtml(
                OneIssueWithColumn("severity", WithCustomField("customfield_10001", "High")),
                "http://jira", customFields: Map(("Severity", "customfield_10001")));

            // Rendered label uses whatever the rule wrote ("severity" lowercase here);
            // the map only resolves to the id, not the canonical name.
            StringAssert.Contains("severity: High", html);
        }
    }
}
