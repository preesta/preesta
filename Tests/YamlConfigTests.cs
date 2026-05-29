using System;
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
  - tracker: jira
    tags: daily
    filter: ""DueDate < startOfDay() AND Resolution is EMPTY""
    notify:
      subject: DueDate expired
      mailTo: assignee
      cc: reporter,managers
      followup: Please resolve

  - tracker: jira
    tags: daily
    active: false
    filter: ""should be skipped""
    notify:
      subject: inactive
      mailTo: nobody

  - tracker: jira
    tags: hourly
    filter: ""Type = Support""
    mutations:
      - verb: PUT
        urlPattern: ""{{@jiraRoot}}/rest/api/2/issue/{{@issueKey}}""
        body: |
          {""update"": {""comment"": [{""add"": {""body"": ""auto""}}]}}

  - type: build
    tags: daily
    mask: ""^9\\.0\\.0\\.""
    projectCode: MYPROJ
    remainingDays: 2
    expiredOnly: true
    notify:
      subject: Release alert
      mailTo: admin

mailAliases:
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
            var rules = _config.GetJqlRules(new[] { "daily" });
            Assert.AreEqual(1, rules.Length);
            Assert.AreEqual("DueDate < startOfDay() AND Resolution is EMPTY", rules[0].Filter);
        }

        [Test]
        public void GetJqlRules_NotifyParsedCorrectly()
        {
            var rule = _config.GetJqlRules(new[] { "daily" }).Single();
            Assert.AreEqual("DueDate expired", rule.Notification!.Subject);
            Assert.AreEqual(new[] { "assignee" }, rule.Notification.RawRecipients);
            Assert.AreEqual(new[] { "reporter", "managers" }, rule.Notification.RawCc);
            Assert.AreEqual("Please resolve", rule.Notification.Followup);
        }

        [Test]
        public void GetJqlRules_InactiveRulesSkipped()
        {
            var rules = _config.GetJqlRules(new[] { "daily" });
            Assert.IsFalse(rules.Any(r => r.Filter == "should be skipped"));
        }

        [Test]
        public void GetJqlRules_CallRestParsedCorrectly()
        {
            var rules = _config.GetJqlRules(new[] { "hourly" });
            Assert.AreEqual(1, rules.Length);
            Assert.AreEqual(1, rules[0].Mutations.Length);
            Assert.AreEqual("PUT", rules[0].Mutations[0].Verb);
            Assert.AreEqual("{{@jiraRoot}}/rest/api/2/issue/{{@issueKey}}", rules[0].Mutations[0].UrlPattern);
            Assert.IsTrue(rules[0].Mutations[0].BodyPattern!.Contains("auto"));
        }

        [Test]
        public void GetReleaseRules_ParsedCorrectly()
        {
            var rules = _config.GetReleaseRules(new[] { "daily" });
            Assert.AreEqual(1, rules.Length);
            Assert.AreEqual(@"^9\.0\.0\.", rules[0].Mask);
            Assert.AreEqual("MYPROJ", rules[0].ProjectCode);
            Assert.AreEqual(2, rules[0].RemainingDays);
            Assert.IsTrue(rules[0].ExpiredOnly);
        }

        [Test]
        public void GetMailAliasMap_ParsedCorrectly()
        {
            var map = _config.GetMailAliasMap();
            Assert.AreEqual(2, map.Count);
            Assert.AreEqual("super_boss@example.com,super_boss2@example.com", map["managers"]);
            Assert.AreEqual("administrator@example.com", map["admin"]);
        }

        [Test]
        public void GetRules_EmptyTagFilterReturnsAll()
        {
            var rules = _config.GetJqlRules(Array.Empty<string>());
            Assert.AreEqual(2, rules.Length);
        }

        [Test]
        public void GetRules_NonexistentTagReturnsEmpty()
        {
            var rules = _config.GetJqlRules(new[] { "nonexistent" });
            Assert.AreEqual(0, rules.Length);
        }

        // ----- lefthook-style tag semantics -----

        private const string TagYaml = @"
rules:
  - tracker: jira
    filter: ""untagged-rule""

  - tracker: jira
    tags: morning
    filter: ""single-tag""

  - tracker: jira
    tags: [morning, standup]
    filter: ""list-tags""

  - tracker: jira
    tags: ""release, hotfix""
    filter: ""comma-separated""
";

        [Test]
        public void Tags_EmptyFilter_RunsEveryRuleIncludingUntagged()
        {
            var cfg = new YamlRulesConfig(TagYaml, Substitute.For<ILogger>());
            var rules = cfg.GetJqlRules(Array.Empty<string>());
            Assert.AreEqual(4, rules.Length, "no tag filter = run everything");
        }

        [Test]
        public void Tags_NonEmptyFilter_SkipsUntaggedRules()
        {
            var cfg = new YamlRulesConfig(TagYaml, Substitute.For<ILogger>());
            var rules = cfg.GetJqlRules(new[] { "morning" });
            // morning matches: single-tag (morning), list-tags (morning ∈ {morning, standup})
            // untagged-rule and comma-separated (tags release/hotfix) drop out
            Assert.AreEqual(2, rules.Length);
            CollectionAssert.AreEquivalent(
                new[] { "single-tag", "list-tags" },
                rules.Select(r => r.Filter).ToArray());
        }

        [Test]
        public void Tags_OrMatch_MultipleCliTags()
        {
            var cfg = new YamlRulesConfig(TagYaml, Substitute.For<ILogger>());
            var rules = cfg.GetJqlRules(new[] { "standup", "release" });
            // standup matches list-tags; release matches comma-separated
            Assert.AreEqual(2, rules.Length);
            CollectionAssert.AreEquivalent(
                new[] { "list-tags", "comma-separated" },
                rules.Select(r => r.Filter).ToArray());
        }

        [Test]
        public void Tags_CommaSeparatedScalar_ParsedAsMultipleTags()
        {
            var cfg = new YamlRulesConfig(TagYaml, Substitute.For<ILogger>());
            // Both "release" and "hotfix" target the same comma-separated rule
            Assert.AreEqual(1, cfg.GetJqlRules(new[] { "release" }).Length);
            Assert.AreEqual(1, cfg.GetJqlRules(new[] { "hotfix" }).Length);
        }

        // ----- Phase 12.1: Linear filter modes -----

        private const string LinearYaml = @"
rules:
  - tracker: linear
    tags: linear-prompt
    filter: ""issues assigned to me, not completed""

  - tracker: linear
    tags: linear-raw
    filterRaw:
      state:
        type:
          neq: completed

  - tracker: linear
    tags: linear-view
    viewId: ""0e8a3b41-1234-4321-aaaa-bbbbbbbbbbbb""

  - tracker: linear
    tags: linear-bad
    # zero filter sources — should be dropped

  - tracker: linear
    tags: linear-bad
    filter: ""whatever""
    viewId: ""abc""
    # two filter sources — should be dropped

  - tracker: linear
    tags: linear-bad
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
  - tracker: linear
    tags: linear-mutations
    filter: ""issues with no assignee in Done""
    mutations:
      - mutation: |
          mutation { commentCreate(input: { issueId: ""{{@issueId}}"", body: ""hi"" }) { success } }
      - mutation: |
          mutation { issueUpdate(id: ""{{@issueId}}"", input: { assigneeId: null }) { success } }
";
            var config = new YamlRulesConfig(yaml, Substitute.For<ILogger>());
            var rules = config.GetLinearRules(new[] { "linear-mutations" });

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
  - tracker: linear
    tags: types
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
                .GetLinearRules(new[] { "types" });
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
            var rules = config.GetLinearRules(new[] { "linear-prompt" });
            Assert.AreEqual(1, rules.Length);
            Assert.AreEqual("issues assigned to me, not completed", rules[0].Filter);
            Assert.IsNull(rules[0].FilterRaw);
            Assert.IsNull(rules[0].ViewId);
        }

        [Test]
        public void Linear_FilterRawMode_ParsedAsJObject()
        {
            var config = new YamlRulesConfig(LinearYaml, Substitute.For<ILogger>());
            var rules = config.GetLinearRules(new[] { "linear-raw" });
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
            var rules = config.GetLinearRules(new[] { "linear-view" });
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

            var rules = config.GetLinearRules(new[] { "linear-bad" });

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
  - tracker: jira
    tags: daily
    filter: ""any""
    notify:
      subject: alert
      mailTo: assignee
      slackUserId: ""U111,U222,U333""
";
            var config = new YamlRulesConfig(yaml, Substitute.For<ILogger>());
            var rules = config.GetJqlRules(new[] { "daily" });

            Assert.AreEqual(1, rules.Length);
            Assert.AreEqual(new[] { "U111", "U222", "U333" }, rules[0].Notification!.SlackUserIds);
        }

        [Test]
        public void SlackUsers_MapParsed()
        {
            const string yaml = @"
rules:
  - tracker: jira
    tags: daily
    filter: ""x""
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
  - tracker: github
    tags: gh
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
                .GetGithubRules(new[] { "gh" });

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
  - tracker: github
    tags: gh-bad
    filter: """"
    notify:
      subject: x
      mailTo: assignee
";
            var logger = Substitute.For<ILogger>();
            var rules = new YamlRulesConfig(yaml, logger).GetGithubRules(new[] { "gh-bad" });

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
  - tracker: github
    tags: gh-bad
    notify:
      subject: x
      mailTo: assignee
";
            var logger = Substitute.For<ILogger>();
            var rules = new YamlRulesConfig(yaml, logger).GetGithubRules(new[] { "gh-bad" });

            Assert.AreEqual(0, rules.Length);
            Assert.IsTrue(
                logger.ReceivedCalls().Any(c => c.GetMethodInfo().Name == "Error"),
                "Expected ILogger.Error when a GitHub rule has no filter");
        }

        // ----- GitLab rules -----

        [Test]
        public void Gitlab_FilterChipsParsedIntoStructuredObject()
        {
            const string yaml = @"
rules:
  - tracker: gitlab
    tags: gl
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
                .GetGitlabRules(new[] { "gl" });

            Assert.AreEqual(1, rules.Length);
            var f = rules[0].Filter;
            Assert.AreEqual("opened", f.State);
            CollectionAssert.AreEqual(new[] { "urgent", "blocker" }, f.LabelName!);
            CollectionAssert.AreEqual(new[] { "alice", "bob" }, f.AssigneeUsernames!);
            Assert.AreEqual("carol", f.AuthorUsername);
            CollectionAssert.AreEqual(new[] { "v1.0" }, f.MilestoneTitle!);
            Assert.AreEqual("checkout flow", f.Search);
            Assert.AreEqual(false, f.Confidential);
            Assert.Contains("assignee", rules[0].Notification!.RawRecipients);
        }

        [Test]
        public void Gitlab_GraphQLMutations_ParsedFromMutationsKey()
        {
            const string yaml = @"
rules:
  - tracker: gitlab
    tags: gl
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
                .GetGitlabRules(new[] { "gl" });

            Assert.AreEqual(1, rules.Length);
            Assert.AreEqual(2, rules[0].GraphQLMutations.Length);
            Assert.IsTrue(rules[0].GraphQLMutations[0].MutationBody.Contains("createNote"));
            Assert.IsTrue(rules[0].GraphQLMutations[1].MutationBody.Contains("updateIssue"));
            Assert.AreEqual(0, rules[0].Mutations.Length);
        }

        [Test]
        public void Gitlab_RuleWithEmptyOrNoFilter_IsAccepted()
        {
            // Whether a GitLab query is "too broad" depends on the target instance
            // (gitlab.com vs a small self-hosted one), which the parser can't know.
            // So we don't statically reject thin/empty filters — an over-broad query
            // is handled at runtime (the server times out, GitlabIssueSource catches
            // it, logs a warning, and the run continues).
            const string emptyFilter = @"
rules:
  - tracker: gitlab
    tags: gl
    filter: {}
    notify: { subject: x, mailTo: assignee }
";
            const string noFilter = @"
rules:
  - tracker: gitlab
    tags: gl
    notify: { subject: x, mailTo: assignee }
";
            foreach (var yaml in new[] { emptyFilter, noFilter })
            {
                var logger = Substitute.For<ILogger>();
                var rules = new YamlRulesConfig(yaml, logger).GetGitlabRules(new[] { "gl" });

                Assert.AreEqual(1, rules.Length, "Rule should be accepted, not dropped");
                Assert.IsFalse(
                    logger.ReceivedCalls().Any(c => c.GetMethodInfo().Name == "Error"),
                    "No Error should be logged — thin filters are no longer rejected");
            }
        }

        [Test]
        public void Gitlab_FilterScalarString_AcceptedAsSingleEntryArray()
        {
            const string yaml = @"
rules:
  - tracker: gitlab
    tags: gl
    filter:
      labelName: urgent
";
            var rules = new YamlRulesConfig(yaml, Substitute.For<ILogger>()).GetGitlabRules(new[] { "gl" });

            Assert.AreEqual(1, rules.Length);
            CollectionAssert.AreEqual(new[] { "urgent" }, rules[0].Filter.LabelName!);
        }

        // ----- end GitLab -----

        // ----- Shortcut rules -----

        [Test]
        public void Shortcut_FilterAndRestMutationsParsed()
        {
            const string yaml = @"
rules:
  - tracker: shortcut
    tags: sc
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
                .GetShortcutRules(new[] { "sc" });

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
  - tracker: shortcut
    tags: sc-bad
    filter: """"
    notify:
      subject: x
      mailTo: assignee
";
            var logger = Substitute.For<ILogger>();
            var rules = new YamlRulesConfig(yaml, logger).GetShortcutRules(new[] { "sc-bad" });

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
  - tracker: shortcut
    tags: sc-bad
    notify:
      subject: x
      mailTo: assignee
";
            var logger = Substitute.For<ILogger>();
            var rules = new YamlRulesConfig(yaml, logger).GetShortcutRules(new[] { "sc-bad" });

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
  - tracker: linear
    tags: bad
    filter: ""x""
    filterRaw:
      state: { type: { neq: completed } }
";
            var logger = Substitute.For<ILogger>();
            var config = new YamlRulesConfig(yaml, logger);

            var rules = config.GetLinearRules(new[] { "bad" });

            Assert.AreEqual(0, rules.Length);
            Assert.IsTrue(
                logger.ReceivedCalls().Any(c => c.GetMethodInfo().Name == "Error"),
                "Expected ILogger.Error when a Linear rule has multiple filter sources set");
        }
    }
}
