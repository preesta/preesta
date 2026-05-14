using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using Preesta;
using Preesta.Configuration.Action;
using Serilog;
using ShortcutRest;

namespace Tests.Shortcut
{
    /// <summary>
    /// Unit tests against an NSubstitute IShortcutGateway — covers REST search
    /// response mapping (including the workflow-state / member resolution paths)
    /// into the shared <see cref="Preesta.Data.Issue"/> model.
    /// </summary>
    [TestFixture]
    public class ShortcutIssueSourceTests
    {
        // Three stories: bug+feature with full owner/requester, plus an outlier
        // that references an unknown member ID so we can verify the soft-fallback path.
        private const string SearchResponse = @"{
  ""total"": 3,
  ""next"": null,
  ""data"": [
    {
      ""id"": 1234,
      ""name"": ""Bug: thing broken"",
      ""app_url"": ""https://app.shortcut.com/preesta/story/1234"",
      ""story_type"": ""bug"",
      ""workflow_state_id"": 500001,
      ""owner_ids"": [""11111111-aaaa-bbbb-cccc-222222222222""],
      ""requested_by_id"": ""33333333-aaaa-bbbb-cccc-444444444444"",
      ""labels"": [
        { ""id"": 1, ""name"": ""urgent"" },
        { ""id"": 2, ""name"": ""regression"" }
      ],
      ""deadline"": ""2026-06-01T00:00:00Z"",
      ""created_at"": ""2026-05-01T10:00:00Z"",
      ""updated_at"": ""2026-05-08T12:00:00Z"",
      ""project_id"": 9,
      ""group_id"": ""99999999-aaaa-bbbb-cccc-000000000000""
    },
    {
      ""id"": 7,
      ""name"": ""Add feature X"",
      ""app_url"": ""https://app.shortcut.com/preesta/story/7"",
      ""story_type"": ""feature"",
      ""workflow_state_id"": 500002,
      ""owner_ids"": [],
      ""requested_by_id"": ""11111111-aaaa-bbbb-cccc-222222222222"",
      ""labels"": [],
      ""deadline"": null,
      ""created_at"": ""2026-05-02T11:00:00Z"",
      ""updated_at"": ""2026-05-09T13:00:00Z""
    },
    {
      ""id"": 999,
      ""name"": ""Unknown owner edge case"",
      ""app_url"": ""https://app.shortcut.com/preesta/story/999"",
      ""story_type"": ""chore"",
      ""workflow_state_id"": 500099,
      ""owner_ids"": [""ffffffff-ffff-ffff-ffff-ffffffffffff""],
      ""requested_by_id"": ""ffffffff-ffff-ffff-ffff-ffffffffffff"",
      ""labels"": [],
      ""deadline"": null,
      ""created_at"": ""2026-05-05T10:00:00Z"",
      ""updated_at"": ""2026-05-05T10:00:00Z""
    }
  ]
}";

        private const string WorkflowsResponse = @"[
  {
    ""id"": 100,
    ""name"": ""Engineering"",
    ""states"": [
      { ""id"": 500001, ""name"": ""In Progress"", ""type"": ""started"" },
      { ""id"": 500002, ""name"": ""Backlog"",     ""type"": ""unstarted"" }
    ]
  }
]";

        private const string MembersResponse = @"[
  {
    ""id"": ""11111111-aaaa-bbbb-cccc-222222222222"",
    ""profile"": {
      ""name"": ""Bob Roberts"",
      ""mention_name"": ""bob"",
      ""email_address"": ""bob@example.com""
    }
  },
  {
    ""id"": ""33333333-aaaa-bbbb-cccc-444444444444"",
    ""profile"": {
      ""name"": ""Alice"",
      ""mention_name"": ""alice"",
      ""email_address"": ""alice@example.com""
    }
  }
]";

        private const string EmptySearchResponse = @"{ ""total"": 0, ""next"": null, ""data"": [] }";

        private static ShortcutIssueSource SourceWith(
            string searchJson, string workflowsJson = WorkflowsResponse, string membersJson = MembersResponse)
        {
            var gateway = Substitute.For<IShortcutGateway>();
            gateway.SearchStories(Arg.Any<string>(), Arg.Any<int>())
                .Returns(JObject.Parse(searchJson));
            gateway.GetWorkflows().Returns(JArray.Parse(workflowsJson));
            gateway.GetMembers().Returns(JArray.Parse(membersJson));
            return new ShortcutIssueSource(gateway, Substitute.For<ILogger>());
        }

        private static ShortcutRule RuleFor(string filter = "state:\"In Progress\" type:bug") =>
            new ShortcutRule { Filter = filter };

        [Test]
        public void HappyPath_MapsKeyAsScDashId()
        {
            var issues = SourceWith(SearchResponse).GetIssues(RuleFor());

            Assert.AreEqual(3, issues.Length);
            Assert.AreEqual("sc-1234", issues[0].Key);
            Assert.AreEqual("sc-7", issues[1].Key);
            Assert.AreEqual("sc-999", issues[2].Key);
        }

        [Test]
        public void HappyPath_PopulatesShortcutIdForMutationTarget()
        {
            var issues = SourceWith(SearchResponse).GetIssues(RuleFor());

            Assert.AreEqual("1234", issues[0].ShortcutId);
            Assert.AreEqual("7", issues[1].ShortcutId);
            Assert.AreEqual("999", issues[2].ShortcutId);
        }

        [Test]
        public void TypeReflectsStoryType()
        {
            var issues = SourceWith(SearchResponse).GetIssues(RuleFor());

            Assert.AreEqual("bug", issues[0].Type);
            Assert.AreEqual("feature", issues[1].Type);
            Assert.AreEqual("chore", issues[2].Type);
        }

        [Test]
        public void Url_TakesAppUrl()
        {
            var issues = SourceWith(SearchResponse).GetIssues(RuleFor());

            Assert.AreEqual("https://app.shortcut.com/preesta/story/1234", issues[0].Url);
        }

        [Test]
        public void Status_ResolvedFromWorkflowStateId()
        {
            var issues = SourceWith(SearchResponse).GetIssues(RuleFor());

            Assert.AreEqual("In Progress", issues[0].Status);
            Assert.AreEqual("Backlog", issues[1].Status);
        }

        [Test]
        public void Status_UnknownStateId_FallsBackToNull()
        {
            // 500099 isn't in WorkflowsResponse — Status should be null, not crash.
            var issues = SourceWith(SearchResponse).GetIssues(RuleFor());
            Assert.IsNull(issues[2].Status);
        }

        [Test]
        public void Assignee_ResolvedViaMembersCache_WithEmailAndDisplayName()
        {
            var issues = SourceWith(SearchResponse).GetIssues(RuleFor());

            Assert.IsNotNull(issues[0].Participants.Assignee);
            Assert.AreEqual("bob@example.com", issues[0].Participants.Assignee!.Email);
            Assert.AreEqual("Bob Roberts", issues[0].Participants.Assignee.DisplayName);
        }

        [Test]
        public void Assignee_EmptyOwnerIds_ProducesNullUser()
        {
            var issues = SourceWith(SearchResponse).GetIssues(RuleFor());
            Assert.IsNull(issues[1].Participants.Assignee);
        }

        [Test]
        public void Assignee_UnknownMemberId_FallsBackWithEmptyEmail()
        {
            // Routing via mailTo: assignee must skip cleanly when email is empty,
            // never produce a "To: <raw-uuid>" line.
            var issues = SourceWith(SearchResponse).GetIssues(RuleFor());

            Assert.IsNotNull(issues[2].Participants.Assignee);
            Assert.AreEqual(string.Empty, issues[2].Participants.Assignee!.Email);
            Assert.AreEqual("ffffffff-ffff-ffff-ffff-ffffffffffff",
                issues[2].Participants.Assignee.Key);
        }

        [Test]
        public void ReporterAndCreator_BothFromRequestedById()
        {
            // Shortcut has no separate reporter — both should point to the requester.
            var issues = SourceWith(SearchResponse).GetIssues(RuleFor());

            Assert.AreEqual("alice@example.com", issues[0].Participants.Reporter!.Email);
            Assert.AreEqual("alice@example.com", issues[0].Participants.Creator!.Email);
        }

        [Test]
        public void LabelsJoinedByComma()
        {
            var issues = SourceWith(SearchResponse).GetIssues(RuleFor());

            Assert.AreEqual("urgent, regression", issues[0].Labels);
            Assert.AreEqual(string.Empty, issues[1].Labels);
        }

        [Test]
        public void Dates_ParsedAsUtc()
        {
            var issues = SourceWith(SearchResponse).GetIssues(RuleFor());

            Assert.AreEqual(new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc), issues[0].CreatedDate);
            Assert.AreEqual(new DateTime(2026, 5, 8, 12, 0, 0, DateTimeKind.Utc), issues[0].UpdatedDate);
            Assert.AreEqual(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), issues[0].DueDate);
        }

        [Test]
        public void Dates_NullDeadline_LeavesDueDateNull()
        {
            var issues = SourceWith(SearchResponse).GetIssues(RuleFor());
            Assert.IsNull(issues[1].DueDate);
        }

        [Test]
        public void EmptyFilter_ReturnsEmptyAndDoesNotCallGateway()
        {
            var gateway = Substitute.For<IShortcutGateway>();
            var source = new ShortcutIssueSource(gateway, Substitute.For<ILogger>());

            var issues = source.GetIssues(new ShortcutRule { Filter = "  " });

            Assert.AreEqual(0, issues.Length);
            gateway.DidNotReceive().SearchStories(Arg.Any<string>(), Arg.Any<int>());
        }

        [Test]
        public void EmptySearchResult_ReturnsEmptyArray()
        {
            var issues = SourceWith(EmptySearchResponse).GetIssues(RuleFor());
            Assert.AreEqual(0, issues.Length);
        }

        [Test]
        public void SearchFailure_ReturnsEmptyAndLogsWarning()
        {
            var gateway = Substitute.For<IShortcutGateway>();
            gateway.SearchStories(Arg.Any<string>(), Arg.Any<int>())
                .Returns<JObject>(_ => throw new InvalidOperationException("HTTP 401"));
            var logger = Substitute.For<ILogger>();
            var source = new ShortcutIssueSource(gateway, logger);

            var issues = source.GetIssues(RuleFor());

            Assert.AreEqual(0, issues.Length);
            Assert.IsTrue(logger.ReceivedCalls()
                .Any(c => c.GetMethodInfo().Name == "Warning"));
        }

        [Test]
        public void WorkflowsAndMembers_FetchedOnceAndCached()
        {
            // Two GetIssues calls → workflows and members loaded once total (Lazy<T>).
            var gateway = Substitute.For<IShortcutGateway>();
            gateway.SearchStories(Arg.Any<string>(), Arg.Any<int>())
                .Returns(JObject.Parse(SearchResponse));
            gateway.GetWorkflows().Returns(JArray.Parse(WorkflowsResponse));
            gateway.GetMembers().Returns(JArray.Parse(MembersResponse));
            var source = new ShortcutIssueSource(gateway, Substitute.For<ILogger>());

            source.GetIssues(RuleFor());
            source.GetIssues(RuleFor("other:filter"));

            Assert.AreEqual(1, gateway.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "GetWorkflows"));
            Assert.AreEqual(1, gateway.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "GetMembers"));
            Assert.AreEqual(2, gateway.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "SearchStories"));
        }
    }
}
