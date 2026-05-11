using System;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using PreestaConvert = Preesta.Data.Convert;

namespace Tests
{
    [TestFixture]
    public class JTokenConvertTests
    {
        private const string SampleIssueWithCustomFields = @"{
  ""key"": ""SCRUM-7"",
  ""fields"": {
    ""summary"": ""Sample"",
    ""fixVersions"": [],
    ""versions"": [],
    ""components"": [],
    ""labels"": [],
    ""priority"": { ""name"": ""High"" },
    ""assignee"": null,
    ""reporter"": null,
    ""creator"": null,
    ""status"": { ""name"": ""In Progress"" },
    ""issuetype"": { ""name"": ""Task"" },
    ""timespent"": null,
    ""duedate"": null,
    ""created"": ""2026-05-01T10:00:00.000+0000"",
    ""updated"": ""2026-05-08T12:00:00.000+0000"",
    ""resolution"": null,
    ""project"": { ""key"": ""SCRUM"" },
    ""customfield_10001"": ""High"",
    ""customfield_10002"": [{""name"": ""Backend""}, {""name"": ""Auth""}],
    ""customfield_10003"": null
  }
}";

        [Test]
        public void ToIssue_ExtractsCustomFieldsByPrefix_PreservesShape()
        {
            dynamic issue = JObject.Parse(SampleIssueWithCustomFields);
            var result = PreestaConvert.JToken.ToIssue(issue);

            Assert.AreEqual("SCRUM-7", result.Key);

            // String scalar
            Assert.IsTrue(result.CustomFields.ContainsKey("customfield_10001"));
            Assert.AreEqual("High", (string?)result.CustomFields["customfield_10001"]);

            // JArray of objects (multiselect-like)
            Assert.IsTrue(result.CustomFields.ContainsKey("customfield_10002"));
            var arr = result.CustomFields["customfield_10002"] as JArray;
            Assert.IsNotNull(arr);
            Assert.AreEqual(2, arr!.Count);

            // Null is preserved as null entry (NOT skipped) so downstream can
            // distinguish "field absent in Jira config" vs "field present but unset".
            Assert.IsTrue(result.CustomFields.ContainsKey("customfield_10003"));
            Assert.IsNull(result.CustomFields["customfield_10003"]);

            // Standard fields are NOT in CustomFields — only customfield_* prefix.
            Assert.IsFalse(result.CustomFields.ContainsKey("priority"));
            Assert.IsFalse(result.CustomFields.ContainsKey("status"));
        }
    }
}
