using System.Linq;
using Preesta.Configuration;
using Newtonsoft.Json.Linq;
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
    mutations:
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
      subject: Release alert
      mailTo: admin

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
            Assert.AreEqual("DueDate expired", rule.Notification!.Subject);
            Assert.AreEqual(new[] { "assignee" }, rule.Notification.RawRecipients);
            Assert.AreEqual(new[] { "reporter", "managers" }, rule.Notification.RawCc);
            Assert.AreEqual("Please resolve", rule.Notification.Recommendations);
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
            Assert.AreEqual(1, rules[0].Mutations.Length);
            Assert.AreEqual("PUT", rules[0].Mutations[0].Verb);
            Assert.AreEqual("{{@jiraRoot}}/rest/api/2/issue/{{@issueKey}}", rules[0].Mutations[0].UrlPattern);
            Assert.IsTrue(rules[0].Mutations[0].BodyPattern!.Contains("auto"));
        }

        [Test]
        public void GetReleaseRules_ParsedCorrectly()
        {
            var rules = _config.GetReleaseRules("daily");
            Assert.AreEqual(1, rules.Length);
            Assert.AreEqual(@"^9\.0\.0\.", rules[0].Mask);
            Assert.AreEqual("MYPROJ", rules[0].ProjectCode);
            Assert.AreEqual(2, rules[0].RemainingDays);
            Assert.IsTrue(rules[0].ExpiredOnly);
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

        // ----- Phase 12.1: Linear filter modes -----

        private const string LinearYaml = @"
rules:
  - type: linear
    group: linear-prompt
    filter: ""issues assigned to me, not completed""

  - type: linear
    group: linear-raw
    filterRaw:
      state:
        type:
          neq: completed

  - type: linear
    group: linear-view
    viewId: ""0e8a3b41-1234-4321-aaaa-bbbbbbbbbbbb""

  - type: linear
    group: linear-bad
    # zero filter sources — should be dropped

  - type: linear
    group: linear-bad
    filter: ""whatever""
    viewId: ""abc""
    # two filter sources — should be dropped

  - type: linear
    group: linear-bad
    filter: ""ok""
    filterRaw:
      state: { type: { neq: completed } }
    # two filter sources — should be dropped
";

        [Test]
        public void Linear_GraphQLMutations_ParsedFromMutationsKey()
        {
            const string yaml = @"
rules:
  - type: linear
    group: linear-mutations
    filter: ""issues with no assignee in Done""
    mutations:
      - mutation: |
          mutation { commentCreate(input: { issueId: ""{{@issueId}}"", body: ""hi"" }) { success } }
      - mutation: |
          mutation { issueUpdate(id: ""{{@issueId}}"", input: { assigneeId: null }) { success } }
";
            var config = new YamlRulesConfig(yaml, Substitute.For<ILogger>());
            var rules = config.GetLinearRules("linear-mutations");

            Assert.AreEqual(1, rules.Length);
            Assert.AreEqual(2, rules[0].GraphQLMutations.Length);
            Assert.IsTrue(rules[0].GraphQLMutations[0].MutationBody.Contains("commentCreate"));
            Assert.IsTrue(rules[0].GraphQLMutations[0].MutationBody.Contains("{{@issueId}}"));
            Assert.IsTrue(rules[0].GraphQLMutations[1].MutationBody.Contains("assigneeId: null"));
            // REST Mutations array is empty for linear rules — `mutations:` is GraphQL.
            Assert.AreEqual(0, rules[0].Mutations.Length);
        }

        [Test]
        public void Linear_FilterRaw_PreservesScalarTypes()
        {
            // YamlDotNet returns all scalars as strings when target is `object`.
            // ConvertFilterRaw must recover int/float/bool via TryParse so Linear's
            // GraphQL doesn't reject e.g. { gte: ""2"" } where it expects { gte: 2 }.
            const string yaml = @"
rules:
  - type: linear
    group: types
    filterRaw:
      priority:
        gte: 2
      ratio:
        eq: 1.5
      flag:
        eq: true
      text:
        eq: hello
      quoted:
        eq: ""9""
";
            var rules = new YamlRulesConfig(yaml, Substitute.For<ILogger>())
                .GetLinearRules("types");
            var f = rules[0].FilterRaw!;

            Assert.AreEqual(JTokenType.Integer, f.SelectToken("priority.gte")!.Type);
            Assert.AreEqual(2L, (long)f.SelectToken("priority.gte")!);

            Assert.AreEqual(JTokenType.Float, f.SelectToken("ratio.eq")!.Type);
            Assert.AreEqual(1.5, (double)f.SelectToken("ratio.eq")!);

            Assert.AreEqual(JTokenType.Boolean, f.SelectToken("flag.eq")!.Type);
            Assert.AreEqual(true, (bool)f.SelectToken("flag.eq")!);

            Assert.AreEqual(JTokenType.String, f.SelectToken("text.eq")!.Type);
            Assert.AreEqual("hello", (string?)f.SelectToken("text.eq"));

            // Quoted scalars in YAML come through as strings even if the content looks
            // numeric. Our walker can't distinguish "9" (quoted) from 9 (unquoted) since
            // YamlDotNet hands both to us as plain string — known limitation, documented.
            // Acceptable behaviour: "9" parses as Integer here too. Power users wanting
            // string-typed numerics should rename the field or rely on the AI prompt.
            Assert.AreEqual(JTokenType.Integer, f.SelectToken("quoted.eq")!.Type);
        }

        [Test]
        public void Linear_FilterPromptMode_ParsedCorrectly()
        {
            var config = new YamlRulesConfig(LinearYaml, Substitute.For<ILogger>());
            var rules = config.GetLinearRules("linear-prompt");
            Assert.AreEqual(1, rules.Length);
            Assert.AreEqual("issues assigned to me, not completed", rules[0].Filter);
            Assert.IsNull(rules[0].FilterRaw);
            Assert.IsNull(rules[0].ViewId);
        }

        [Test]
        public void Linear_FilterRawMode_ParsedAsJObject()
        {
            var config = new YamlRulesConfig(LinearYaml, Substitute.For<ILogger>());
            var rules = config.GetLinearRules("linear-raw");
            Assert.AreEqual(1, rules.Length);
            Assert.IsNull(rules[0].Filter);
            Assert.IsNotNull(rules[0].FilterRaw);
            Assert.AreEqual("completed",
                (string?)rules[0].FilterRaw!.SelectToken("state.type.neq"));
            Assert.IsNull(rules[0].ViewId);
        }

        [Test]
        public void Linear_ViewIdMode_ParsedCorrectly()
        {
            var config = new YamlRulesConfig(LinearYaml, Substitute.For<ILogger>());
            var rules = config.GetLinearRules("linear-view");
            Assert.AreEqual(1, rules.Length);
            Assert.AreEqual("0e8a3b41-1234-4321-aaaa-bbbbbbbbbbbb", rules[0].ViewId);
            Assert.IsNull(rules[0].Filter);
            Assert.IsNull(rules[0].FilterRaw);
        }

        [Test]
        public void Linear_RuleWithNoFilterSource_IsDroppedAndLogged()
        {
            var logger = Substitute.For<ILogger>();
            var config = new YamlRulesConfig(LinearYaml, logger);

            var rules = config.GetLinearRules("linear-bad");

            Assert.AreEqual(0, rules.Length);
            // Three malformed rules in linear-bad group (zero/two/two sources).
            // We don't assert exact message text here; the count of Error calls
            // (one per dropped rule) is sufficient signal.
            var errorCalls = logger.ReceivedCalls()
                .Where(c => c.GetMethodInfo().Name == "Error")
                .ToList();
            Assert.GreaterOrEqual(errorCalls.Count, 3,
                "Expected at least one Error log per dropped Linear rule (3 malformed rules)");
        }

        [Test]
        public void Slack_UserIds_ParsedFromCommaSeparated()
        {
            const string yaml = @"
rules:
  - type: jql
    group: daily
    jql: ""any""
    notify:
      subject: alert
      mailTo: assignee
      slackUserId: ""U111,U222,U333""
";
            var config = new YamlRulesConfig(yaml, Substitute.For<ILogger>());
            var rules = config.GetJqlRules("daily");

            Assert.AreEqual(1, rules.Length);
            Assert.AreEqual(new[] { "U111", "U222", "U333" }, rules[0].Notification!.SlackUserIds);
        }

        [Test]
        public void SlackUsers_MapParsed()
        {
            const string yaml = @"
rules:
  - type: jql
    group: daily
    jql: ""x""
    notify:
      subject: alert
      mailTo: assignee

slackUsers:
  ivanov@ex.com: U111
  petrov@ex.com: U222
";
            var config = new YamlRulesConfig(yaml, Substitute.For<ILogger>());
            var map = config.GetSlackUserMap();

            Assert.AreEqual(2, map.Count);
            Assert.AreEqual("U111", map["ivanov@ex.com"]);
            Assert.AreEqual("U222", map["petrov@ex.com"]);
        }

        // ----- GitHub rules -----

        [Test]
        public void Github_FilterAndMutationsParsed()
        {
            const string yaml = @"
rules:
  - type: github
    group: gh
    filter: ""is:open is:issue org:bigcorp label:urgent""
    mutations:
      - mutation: |
          mutation { addComment(input: { subjectId: ""{{@issueId}}"", body: ""ping"" }) { clientMutationId } }
      - mutation: |
          mutation { closeIssue(input: { issueId: ""{{@issueId}}"" }) { clientMutationId } }
    notify:
      subject: ""GH open""
      mailTo: assignee
";
            var rules = new YamlRulesConfig(yaml, Substitute.For<ILogger>())
                .GetGithubRules("gh");

            Assert.AreEqual(1, rules.Length);
            Assert.AreEqual("is:open is:issue org:bigcorp label:urgent", rules[0].Filter);
            Assert.AreEqual(2, rules[0].GraphQLMutations.Length);
            Assert.IsTrue(rules[0].GraphQLMutations[0].MutationBody.Contains("addComment"));
            Assert.IsTrue(rules[0].GraphQLMutations[1].MutationBody.Contains("closeIssue"));
            // REST Mutations array is empty for github rules — `mutations:` is GraphQL.
            Assert.AreEqual(0, rules[0].Mutations.Length);
            // Notification markers still flow through ToBaseRule (assignee for routing).
            Assert.Contains("assignee", rules[0].Notification!.RawRecipients);
        }

        [Test]
        public void Github_RuleWithEmptyFilter_IsDroppedAndLogged()
        {
            const string yaml = @"
rules:
  - type: github
    group: gh-bad
    filter: """"
    notify:
      subject: x
      mailTo: assignee
";
            var logger = Substitute.For<ILogger>();
            var rules = new YamlRulesConfig(yaml, logger).GetGithubRules("gh-bad");

            Assert.AreEqual(0, rules.Length);
            Assert.IsTrue(
                logger.ReceivedCalls().Any(c => c.GetMethodInfo().Name == "Error"),
                "Expected ILogger.Error when a GitHub rule has an empty filter");
        }

        [Test]
        public void Github_RuleWithoutFilter_IsDroppedAndLogged()
        {
            const string yaml = @"
rules:
  - type: github
    group: gh-bad
    notify:
      subject: x
      mailTo: assignee
";
            var logger = Substitute.For<ILogger>();
            var rules = new YamlRulesConfig(yaml, logger).GetGithubRules("gh-bad");

            Assert.AreEqual(0, rules.Length);
            Assert.IsTrue(
                logger.ReceivedCalls().Any(c => c.GetMethodInfo().Name == "Error"),
                "Expected ILogger.Error when a GitHub rule has no filter");
        }

        // ----- Plane rules -----

        [Test]
        public void Plane_ProjectIdAndFilterParsed()
        {
            const string yaml = @"
rules:
  - type: plane
    group: plane-group
    projectId: ""550e8400-e29b-41d4-a716-446655440000""
    filter:
      priority: ""urgent,high""
      search: ""memory leak""
    notify:
      subject: ""Plane open""
      mailTo: assignee
";
            var rules = new YamlRulesConfig(yaml, Substitute.For<ILogger>())
                .GetPlaneRules("plane-group");

            Assert.AreEqual(1, rules.Length);
            Assert.AreEqual("550e8400-e29b-41d4-a716-446655440000", rules[0].ProjectId);
            Assert.AreEqual(2, rules[0].Filter.Count);
            Assert.AreEqual("urgent,high", rules[0].Filter["priority"]);
            Assert.AreEqual("memory leak", rules[0].Filter["search"]);
            Assert.Contains("assignee", rules[0].Notification!.RawRecipients);
        }

        [Test]
        public void Plane_RestMutationsParsedFromMutationsKey()
        {
            const string yaml = @"
rules:
  - type: plane
    group: plane-mut
    projectId: ""proj-1""
    mutations:
      - verb: PATCH
        urlPattern: ""https://api.plane.so/api/v1/workspaces/x/projects/proj-1/work-items/{{@issueId}}""
        body: |
          {""priority"": ""medium""}
";
            var rules = new YamlRulesConfig(yaml, Substitute.For<ILogger>())
                .GetPlaneRules("plane-mut");

            Assert.AreEqual(1, rules.Length);
            Assert.AreEqual(1, rules[0].Mutations.Length);
            Assert.AreEqual("PATCH", rules[0].Mutations[0].Verb);
            Assert.IsTrue(rules[0].Mutations[0].UrlPattern.Contains("{{@issueId}}"));
            Assert.IsTrue(rules[0].Mutations[0].BodyPattern!.Contains("medium"));
        }

        [Test]
        public void Plane_RuleWithoutProjectId_IsDroppedAndLogged()
        {
            const string yaml = @"
rules:
  - type: plane
    group: plane-bad
    filter:
      priority: urgent
    notify:
      subject: x
      mailTo: assignee
";
            var logger = Substitute.For<ILogger>();
            var rules = new YamlRulesConfig(yaml, logger).GetPlaneRules("plane-bad");

            Assert.AreEqual(0, rules.Length);
            Assert.IsTrue(
                logger.ReceivedCalls().Any(c => c.GetMethodInfo().Name == "Error"),
                "Expected ILogger.Error when a Plane rule has no projectId");
        }

        [Test]
        public void Plane_EmptyFilterAllowed_DefaultsToWholeProject()
        {
            const string yaml = @"
rules:
  - type: plane
    group: plane-allitems
    projectId: ""proj-1""
    notify:
      subject: ""All Plane items""
      mailTo: assignee
";
            var rules = new YamlRulesConfig(yaml, Substitute.For<ILogger>())
                .GetPlaneRules("plane-allitems");

            Assert.AreEqual(1, rules.Length);
            Assert.AreEqual("proj-1", rules[0].ProjectId);
            Assert.AreEqual(0, rules[0].Filter.Count);
        }

        [Test]
        public void Plane_FilterAsString_IsRejected()
        {
            // filter must be a YAML mapping; if the user writes a bare string by
            // mistake we want a clear error rather than silently dropping the filter.
            const string yaml = @"
rules:
  - type: plane
    group: plane-strfilter
    projectId: ""proj-1""
    filter: ""priority=urgent""
";
            var logger = Substitute.For<ILogger>();
            var rules = new YamlRulesConfig(yaml, logger).GetPlaneRules("plane-strfilter");

            Assert.AreEqual(0, rules.Length);
            Assert.IsTrue(
                logger.ReceivedCalls().Any(c => c.GetMethodInfo().Name == "Error"),
                "Expected ILogger.Error when Plane 'filter' is a string instead of a mapping");
        }

        // ----- end Plane -----

        // ----- GitLab rules -----

        [Test]
        public void Gitlab_FilterChipsParsedIntoStructuredObject()
        {
            const string yaml = @"
rules:
  - type: gitlab
    group: gl
    filter:
      state: opened
      labelName: [urgent, blocker]
      assigneeUsernames: [alice, bob]
      authorUsername: carol
      milestoneTitle: ['v1.0']
      search: ""checkout flow""
      confidential: false
    notify:
      subject: ""GL open""
      mailTo: assignee
";
            var rules = new YamlRulesConfig(yaml, Substitute.For<ILogger>())
                .GetGitlabRules("gl");

            Assert.AreEqual(1, rules.Length);
            var f = rules[0].Filter;
            Assert.AreEqual("opened", f.State);
            CollectionAssert.AreEqual(new[] { "urgent", "blocker" }, f.LabelName);
            CollectionAssert.AreEqual(new[] { "alice", "bob" }, f.AssigneeUsernames);
            Assert.AreEqual("carol", f.AuthorUsername);
            CollectionAssert.AreEqual(new[] { "v1.0" }, f.MilestoneTitle);
            Assert.AreEqual("checkout flow", f.Search);
            Assert.AreEqual(false, f.Confidential);
            Assert.Contains("assignee", rules[0].Notification!.RawRecipients);
        }

        [Test]
        public void Gitlab_GraphQLMutations_ParsedFromMutationsKey()
        {
            const string yaml = @"
rules:
  - type: gitlab
    group: gl
    filter:
      state: opened
      labelName: [stale]
    mutations:
      - mutation: |
          mutation { createNote(input: { noteableId: ""{{@issueId}}"", body: ""ping"" }) { note { id } } }
      - mutation: |
          mutation { updateIssue(input: { id: ""{{@issueId}}"", stateEvent: CLOSE }) { issue { state } } }
";
            var rules = new YamlRulesConfig(yaml, Substitute.For<ILogger>())
                .GetGitlabRules("gl");

            Assert.AreEqual(1, rules.Length);
            Assert.AreEqual(2, rules[0].GraphQLMutations.Length);
            Assert.IsTrue(rules[0].GraphQLMutations[0].MutationBody.Contains("createNote"));
            Assert.IsTrue(rules[0].GraphQLMutations[1].MutationBody.Contains("updateIssue"));
            Assert.AreEqual(0, rules[0].Mutations.Length);
        }

        [Test]
        public void Gitlab_RuleWithNoFilterFields_IsDroppedAndLogged()
        {
            const string yaml = @"
rules:
  - type: gitlab
    group: gl-bad
    filter: {}
    notify:
      subject: x
      mailTo: assignee
";
            var logger = Substitute.For<ILogger>();
            var rules = new YamlRulesConfig(yaml, logger).GetGitlabRules("gl-bad");

            Assert.AreEqual(0, rules.Length);
            Assert.IsTrue(
                logger.ReceivedCalls().Any(c => c.GetMethodInfo().Name == "Error"),
                "Expected ILogger.Error when a GitLab rule's filter is empty");
        }

        [Test]
        public void Gitlab_RuleWithoutFilter_IsDroppedAndLogged()
        {
            const string yaml = @"
rules:
  - type: gitlab
    group: gl-bad
    notify:
      subject: x
      mailTo: assignee
";
            var logger = Substitute.For<ILogger>();
            var rules = new YamlRulesConfig(yaml, logger).GetGitlabRules("gl-bad");

            Assert.AreEqual(0, rules.Length);
            Assert.IsTrue(
                logger.ReceivedCalls().Any(c => c.GetMethodInfo().Name == "Error"),
                "Expected ILogger.Error when a GitLab rule has no filter");
        }

        [Test]
        public void Gitlab_FilterScalarString_AcceptedAsSingleEntryArray()
        {
            const string yaml = @"
rules:
  - type: gitlab
    group: gl
    filter:
      labelName: urgent
";
            var rules = new YamlRulesConfig(yaml, Substitute.For<ILogger>()).GetGitlabRules("gl");

            Assert.AreEqual(1, rules.Length);
            CollectionAssert.AreEqual(new[] { "urgent" }, rules[0].Filter.LabelName);
        }

        // ----- end GitLab -----

        // ----- Shortcut rules -----

        [Test]
        public void Shortcut_FilterAndRestMutationsParsed()
        {
            const string yaml = @"
rules:
  - type: shortcut
    group: sc
    filter: ""state:\""In Progress\"" type:bug""
    mutations:
      - verb: POST
        urlPattern: ""https://api.app.shortcut.com/api/v3/stories/{{@issueId}}/comments""
        body: |
          {""text"": ""ping""}
      - verb: PUT
        urlPattern: ""https://api.app.shortcut.com/api/v3/stories/{{@issueId}}""
        body: |
          {""archived"": true}
    notify:
      subject: ""SC bugs""
      mailTo: assignee
";
            var rules = new YamlRulesConfig(yaml, Substitute.For<ILogger>())
                .GetShortcutRules("sc");

            Assert.AreEqual(1, rules.Length);
            Assert.AreEqual("state:\"In Progress\" type:bug", rules[0].Filter);
            Assert.AreEqual(2, rules[0].Mutations.Length);
            Assert.AreEqual("POST", rules[0].Mutations[0].Verb);
            Assert.IsTrue(rules[0].Mutations[0].UrlPattern.Contains("/comments"));
            Assert.IsTrue(rules[0].Mutations[0].BodyPattern!.Contains("ping"));
            Assert.AreEqual("PUT", rules[0].Mutations[1].Verb);
            Assert.IsTrue(rules[0].Mutations[1].BodyPattern!.Contains("archived"));
            Assert.Contains("assignee", rules[0].Notification!.RawRecipients);
        }

        [Test]
        public void Shortcut_RuleWithEmptyFilter_IsDroppedAndLogged()
        {
            const string yaml = @"
rules:
  - type: shortcut
    group: sc-bad
    filter: """"
    notify:
      subject: x
      mailTo: assignee
";
            var logger = Substitute.For<ILogger>();
            var rules = new YamlRulesConfig(yaml, logger).GetShortcutRules("sc-bad");

            Assert.AreEqual(0, rules.Length);
            Assert.IsTrue(
                logger.ReceivedCalls().Any(c => c.GetMethodInfo().Name == "Error"),
                "Expected ILogger.Error when a Shortcut rule has an empty filter");
        }

        [Test]
        public void Shortcut_RuleWithoutFilter_IsDroppedAndLogged()
        {
            const string yaml = @"
rules:
  - type: shortcut
    group: sc-bad
    notify:
      subject: x
      mailTo: assignee
";
            var logger = Substitute.For<ILogger>();
            var rules = new YamlRulesConfig(yaml, logger).GetShortcutRules("sc-bad");

            Assert.AreEqual(0, rules.Length);
            Assert.IsTrue(
                logger.ReceivedCalls().Any(c => c.GetMethodInfo().Name == "Error"),
                "Expected ILogger.Error when a Shortcut rule has no filter");
        }

        // ----- end Shortcut -----


        [Test]
        public void Linear_RuleWithMultipleFilterSources_IsDroppedAndLogged()
        {
            const string yaml = @"
rules:
  - type: linear
    group: bad
    filter: ""x""
    filterRaw:
      state: { type: { neq: completed } }
";
            var logger = Substitute.For<ILogger>();
            var config = new YamlRulesConfig(yaml, logger);

            var rules = config.GetLinearRules("bad");

            Assert.AreEqual(0, rules.Length);
            Assert.IsTrue(
                logger.ReceivedCalls().Any(c => c.GetMethodInfo().Name == "Error"),
                "Expected ILogger.Error when a Linear rule has multiple filter sources set");
        }
    }
}
