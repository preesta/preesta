using System.Linq;
using Preesta.Configuration;
using NSubstitute;
using NUnit.Framework;
using Serilog;

namespace Tests
{
    [TestFixture]
    public class YamlConfigTests
    {
        private const string YamlConfig = @"
rules:
  - type: jql
    group: daily
    jql: ""DueDate < startOfDay() AND Resolution is EMPTY""
    notify:
      subject: DueDate expired
      mailTo: assignee
      cc: reporter,managers
      recommendations: Please resolve

  - type: jql
    group: daily
    active: false
    jql: ""should be skipped""
    notify:
      subject: inactive
      mailTo: nobody

  - type: jql
    group: hourly
    jql: ""Type = Support""
    callRest:
      - verb: PUT
        urlPattern: ""{{@jiraRoot}}/rest/api/2/issue/{{@issueKey}}""
        body: |
          {""update"": {""comment"": [{""add"": {""body"": ""auto""}}]}}

  - type: build
    group: daily
    mask: ""^9\\.0\\.0\\.""
    projectCode: MYPROJ
    remainingDays: 2
    expiredOnly: true
    notify:
      subject: Build alert
      mailTo: admin

  - type: structure
    group: daily
    structures: ""417,462,525""
    notify:
      subject: Duplicate issues
      mailTo: reporter

redirectionRules:
  managers: ""super_boss@example.com,super_boss2@example.com""
  admin: ""administrator@example.com""
";

        private YamlRulesConfig _config = null!;

        [SetUp]
        public void Setup()
        {
            _config = new YamlRulesConfig(YamlConfig, Substitute.For<ILogger>());
        }

        [Test]
        public void GetJqlRules_ReturnsActiveRulesForGroup()
        {
            var rules = _config.GetJqlRules("daily");
            Assert.AreEqual(1, rules.Length);
            Assert.AreEqual("DueDate < startOfDay() AND Resolution is EMPTY", rules[0].Jql);
        }

        [Test]
        public void GetJqlRules_NotifyParsedCorrectly()
        {
            var rule = _config.GetJqlRules("daily").Single();
            Assert.AreEqual("DueDate expired", rule.HowToNotify!.Subject);
            Assert.AreEqual(new[] { "assignee" }, rule.HowToNotify.MetaAddressers);
            Assert.AreEqual(new[] { "reporter", "managers" }, rule.HowToNotify.MetaCarbonCopy);
            Assert.AreEqual("Please resolve", rule.HowToNotify.Recommendations);
        }

        [Test]
        public void GetJqlRules_InactiveRulesSkipped()
        {
            var rules = _config.GetJqlRules("daily");
            Assert.IsFalse(rules.Any(r => r.Jql == "should be skipped"));
        }

        [Test]
        public void GetJqlRules_CallRestParsedCorrectly()
        {
            var rules = _config.GetJqlRules("hourly");
            Assert.AreEqual(1, rules.Length);
            Assert.AreEqual(1, rules[0].HowToUpdate.Length);
            Assert.AreEqual("PUT", rules[0].HowToUpdate[0].Verb);
            Assert.AreEqual("{{@jiraRoot}}/rest/api/2/issue/{{@issueKey}}", rules[0].HowToUpdate[0].UrlPattern);
            Assert.IsTrue(rules[0].HowToUpdate[0].BodyPattern!.Contains("auto"));
        }

        [Test]
        public void GetBuildRules_ParsedCorrectly()
        {
            var rules = _config.GetBuildRules("daily");
            Assert.AreEqual(1, rules.Length);
            Assert.AreEqual(@"^9\.0\.0\.", rules[0].Mask);
            Assert.AreEqual("MYPROJ", rules[0].ProjectCode);
            Assert.AreEqual(2, rules[0].RemainingDays);
            Assert.IsTrue(rules[0].ExpiredOnly);
        }

        [Test]
        public void GetInStructRules_ParsedCorrectly()
        {
            var rules = _config.GetInStructRules("daily");
            Assert.AreEqual(1, rules.Length);
            Assert.AreEqual(new[] { "417", "462", "525" }, rules[0].Structures);
        }

        [Test]
        public void GetRedirectionMap_ParsedCorrectly()
        {
            var map = _config.GetRedirectionMap();
            Assert.AreEqual(2, map.Count);
            Assert.AreEqual("super_boss@example.com,super_boss2@example.com", map["managers"]);
            Assert.AreEqual("administrator@example.com", map["admin"]);
        }

        [Test]
        public void GetRules_EmptyGroupReturnsAll()
        {
            var rules = _config.GetJqlRules("");
            Assert.AreEqual(2, rules.Length);
        }

        [Test]
        public void GetRules_NonexistentGroupReturnsEmpty()
        {
            var rules = _config.GetJqlRules("nonexistent");
            Assert.AreEqual(0, rules.Length);
        }
    }
}
