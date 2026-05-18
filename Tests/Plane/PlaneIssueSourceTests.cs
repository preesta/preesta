using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using PlaneRest;
using Preesta;
using Preesta.Configuration.Action;
using Serilog;

namespace Tests.Plane
{
    /// <summary>
    /// Unit tests against an NSubstitute IPlaneGateway — verifies Plane REST response
    /// mapping into the shared <see cref="Preesta.Data.Issue"/> model. Same shape as
    /// <see cref="Tests.Github.GithubIssueSourceTests"/>, transport-level concerns
    /// (HTTP auth, pagination) are out of scope here.
    /// </summary>
    [TestFixture]
    public class PlaneIssueSourceTests
    {
        private const string ProjectId = "550e8400-e29b-41d4-a716-446655440000";

        private const string MembersResponse = @"[
  {
    ""member"": {
      ""id"": ""u-alice"",
      ""email"": ""alice@example.com"",
      ""display_name"": ""Alice"",
      ""first_name"": ""Alice""
    }
  },
  {
    ""member"": {
      ""id"": ""u-bob"",
      ""email"": ""bob@example.com"",
      ""display_name"": ""Bob""
    }
  },
  {
    ""member"": {
      ""id"": ""u-carol"",
      ""email"": """",
      ""display_name"": ""Carol""
    }
  }
]";

        // Three work items: one with expanded state, one with state UUID only,
        // one completed (sets Resolution) and missing target_date.
        private const string ThreeWorkItemsResponse = @"{
  ""results"": [
    {
      ""id"": ""wi-1"",
      ""name"": ""Bug: thing broken"",
      ""sequence_id"": 42,
      ""priority"": ""urgent"",
      ""state"": ""state-1"",
      ""state_detail"": { ""id"": ""state-1"", ""name"": ""In Progress"", ""group"": ""started"" },
      ""assignees"": [""u-bob""],
      ""created_by"": ""u-alice"",
      ""updated_by"": ""u-alice"",
      ""labels"": [""label-bug-uuid-123""],
      ""created_at"": ""2026-05-01T10:00:00Z"",
      ""updated_at"": ""2026-05-08T12:00:00Z"",
      ""target_date"": ""2026-05-20"",
      ""completed_at"": null,
      ""project"": ""proj-1""
    },
    {
      ""id"": ""wi-2"",
      ""name"": ""Bare-state issue"",
      ""sequence_id"": 7,
      ""priority"": ""medium"",
      ""state"": ""state-22222222-3333-4444-5555-666666666666"",
      ""assignees"": [],
      ""created_by"": ""u-carol"",
      ""labels"": [{ ""id"": ""label-x"", ""name"": ""enhancement"" }, ""label-uuid-456789ab""],
      ""created_at"": ""2026-05-02T11:00:00Z"",
      ""updated_at"": null,
      ""target_date"": null,
      ""completed_at"": null,
      ""project"": ""proj-1""
    },
    {
      ""id"": ""wi-3"",
      ""name"": ""Old completed item"",
      ""sequence_id"": 3,
      ""priority"": ""low"",
      ""state"": ""state-3"",
      ""state_detail"": { ""id"": ""state-3"", ""name"": ""Done"", ""group"": ""completed"" },
      ""assignees"": [""u-unknown""],
      ""created_by"": ""u-alice"",
      ""labels"": [],
      ""created_at"": ""2026-01-01T00:00:00Z"",
      ""updated_at"": ""2026-02-01T00:00:00Z"",
      ""target_date"": null,
      ""completed_at"": ""2026-02-01T00:00:00Z"",
      ""project"": ""proj-1""
    }
  ]
}";

        private const string EmptyResponse = @"{ ""results"": [] }";

        private static PlaneIssueSource SourceWith(string itemsJson)
        {
            var gateway = Substitute.For<IPlaneGateway>();
            gateway.ListWorkItems(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>())
                .Returns(JObject.Parse(itemsJson));
            gateway.ListWorkspaceMembers().Returns(JArray.Parse(MembersResponse));
            return new PlaneIssueSource(gateway, Substitute.For<ILogger>());
        }

        private static PlaneRule RuleFor(IReadOnlyDictionary<string, string>? filter = null) =>
            new PlaneRule
            {
                ProjectId = ProjectId,
                Filter = filter == null
                    ? new Dictionary<string, string>()
                    : new Dictionary<string, string>(filter)
            };

        [Test]
        public void HappyPath_MapsKeyAsHashSequenceId()
        {
            var issues = SourceWith(ThreeWorkItemsResponse).GetIssues(RuleFor());

            Assert.AreEqual(3, issues.Length);
            Assert.AreEqual("#42", issues[0].Key);
            Assert.AreEqual("#7", issues[1].Key);
            Assert.AreEqual("#3", issues[2].Key);
        }

        [Test]
        public void PopulatesPlaneIdForMutationTarget()
        {
            var issues = SourceWith(ThreeWorkItemsResponse).GetIssues(RuleFor());

            Assert.AreEqual("wi-1", issues[0].PlaneId);
            Assert.AreEqual("wi-2", issues[1].PlaneId);
        }

        [Test]
        public void StateExpandedToHumanName()
        {
            var issues = SourceWith(ThreeWorkItemsResponse).GetIssues(RuleFor());

            Assert.AreEqual("In Progress", issues[0].Status);
            Assert.AreEqual("Done", issues[2].Status);
        }

        [Test]
        public void BareStateUuid_ShortenedForDigest()
        {
            // When state_detail is absent and only the UUID is returned, we render
            // a short "state-XXXXXXXX" prefix instead of leaking the full UUID.
            var issues = SourceWith(ThreeWorkItemsResponse).GetIssues(RuleFor());

            Assert.That(issues[1].Status, Does.StartWith("state-"));
            Assert.That(issues[1].Status, Has.Length.LessThanOrEqualTo("state-XXXXXXXX".Length));
        }

        [Test]
        public void CompletedAt_SetsResolution()
        {
            var issues = SourceWith(ThreeWorkItemsResponse).GetIssues(RuleFor());

            Assert.AreEqual("Completed", issues[2].Resolution);
            Assert.IsNull(issues[0].Resolution);
            Assert.IsNull(issues[1].Resolution);
        }

        [Test]
        public void PriorityNormalisedToTitleCase()
        {
            var issues = SourceWith(ThreeWorkItemsResponse).GetIssues(RuleFor());

            Assert.AreEqual("Urgent", issues[0].Priority);
            Assert.AreEqual("Medium", issues[1].Priority);
            Assert.AreEqual("Low", issues[2].Priority);
        }

        [Test]
        public void AssigneeResolvedFromMembersMap()
        {
            var issues = SourceWith(ThreeWorkItemsResponse).GetIssues(RuleFor());

            Assert.IsNotNull(issues[0].Participants.Assignee);
            Assert.AreEqual("bob@example.com", issues[0].Participants.Assignee!.Email);
            Assert.AreEqual("Bob", issues[0].Participants.Assignee.DisplayName);
            Assert.AreEqual("u-bob", issues[0].Participants.Assignee.Key);

            // No assignees → null
            Assert.IsNull(issues[1].Participants.Assignee);
        }

        [Test]
        public void ReporterAndCreator_BothFromCreatedBy()
        {
            // Plane has no separate reporter; both should be the creator.
            var issues = SourceWith(ThreeWorkItemsResponse).GetIssues(RuleFor());

            Assert.AreEqual("alice@example.com", issues[0].Participants.Reporter!.Email);
            Assert.AreEqual("alice@example.com", issues[0].Participants.Creator!.Email);
        }

        [Test]
        public void UnknownAssigneeUuid_ProducesEmptyEmail_NotNull()
        {
            // u-unknown isn't in the members map. We keep a minimal User (so the
            // digest renders something) but Email is empty so routing skips it.
            var issues = SourceWith(ThreeWorkItemsResponse).GetIssues(RuleFor());

            Assert.IsNotNull(issues[2].Participants.Assignee);
            Assert.AreEqual(string.Empty, issues[2].Participants.Assignee!.Email);
            Assert.AreEqual("u-unknown", issues[2].Participants.Assignee.Key);
        }

        [Test]
        public void Labels_RenderedFromExpandedObjectAndBareUuid()
        {
            // wi-2 has one expanded label object + one bare UUID. The object's
            // name is used verbatim; the bare UUID is shortened to "label-XXXXXXXX".
            var issues = SourceWith(ThreeWorkItemsResponse).GetIssues(RuleFor());

            Assert.That(issues[1].Labels, Does.Contain("enhancement"));
            Assert.That(issues[1].Labels, Does.Contain("label-"));
        }

        [Test]
        public void ProjectKey_PopulatedFromProjectField()
        {
            var issues = SourceWith(ThreeWorkItemsResponse).GetIssues(RuleFor());

            Assert.AreEqual("proj-1", issues[0].ProjectKey);
        }

        [Test]
        public void Dates_ParsedAsUtc()
        {
            var issues = SourceWith(ThreeWorkItemsResponse).GetIssues(RuleFor());

            Assert.AreEqual(new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc), issues[0].CreatedDate);
            Assert.AreEqual(new DateTime(2026, 5, 8, 12, 0, 0, DateTimeKind.Utc), issues[0].UpdatedDate);
            Assert.IsNull(issues[1].UpdatedDate);
        }

        [Test]
        public void FilterMap_PassedToGatewayVerbatim()
        {
            var gateway = Substitute.For<IPlaneGateway>();
            gateway.ListWorkItems(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>())
                .Returns(JObject.Parse(EmptyResponse));
            gateway.ListWorkspaceMembers().Returns(JArray.Parse(MembersResponse));
            var source = new PlaneIssueSource(gateway, Substitute.For<ILogger>());

            var rule = RuleFor(new Dictionary<string, string>
            {
                { "priority", "urgent,high" },
                { "search", "memory leak" }
            });
            source.GetIssues(rule);

            // Source augments the user filter with `expand=state` so the response
            // carries readable state names — verify the user's chips survive verbatim
            // alongside the implicit expand directive.
            gateway.Received(1).ListWorkItems(
                ProjectId,
                Arg.Is<IReadOnlyDictionary<string, string>>(d =>
                    d["priority"] == "urgent,high"
                    && d["search"] == "memory leak"
                    && d["expand"] == "state"));
        }

        [Test]
        public void NoProjectId_ReturnsEmptyAndDoesNotCallGateway()
        {
            var gateway = Substitute.For<IPlaneGateway>();
            var source = new PlaneIssueSource(gateway, Substitute.For<ILogger>());

            var issues = source.GetIssues(new PlaneRule { ProjectId = "" });

            Assert.AreEqual(0, issues.Length);
            gateway.DidNotReceive().ListWorkItems(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>());
        }

        [Test]
        public void GatewayThrows_LogsAndReturnsEmpty()
        {
            var gateway = Substitute.For<IPlaneGateway>();
            gateway.ListWorkItems(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>())
                .Returns<JObject>(_ => throw new InvalidOperationException("HTTP 500"));
            gateway.ListWorkspaceMembers().Returns(JArray.Parse(MembersResponse));
            var logger = Substitute.For<ILogger>();
            var source = new PlaneIssueSource(gateway, logger);

            var issues = source.GetIssues(RuleFor());

            Assert.AreEqual(0, issues.Length);
            Assert.IsTrue(logger.ReceivedCalls().Any(c => c.GetMethodInfo().Name == "Warning"));
        }

        [Test]
        public void EmptyResults_ReturnsEmptyArray()
        {
            var issues = SourceWith(EmptyResponse).GetIssues(RuleFor());

            Assert.AreEqual(0, issues.Length);
        }

        [Test]
        public void MembersLookupFailure_KeepsIssueWithBlankAssigneeEmail()
        {
            // If /members fails, the UUID→email map is empty; assignees still get a
            // minimal User so the digest doesn't crash, but Email is blank so the
            // marker resolver skips them.
            var gateway = Substitute.For<IPlaneGateway>();
            gateway.ListWorkItems(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>())
                .Returns(JObject.Parse(ThreeWorkItemsResponse));
            gateway.ListWorkspaceMembers()
                .Returns<JArray>(_ => throw new InvalidOperationException("members 403"));
            var source = new PlaneIssueSource(gateway, Substitute.For<ILogger>());

            var issues = source.GetIssues(RuleFor());

            Assert.AreEqual(3, issues.Length);
            Assert.IsNotNull(issues[0].Participants.Assignee);
            Assert.AreEqual(string.Empty, issues[0].Participants.Assignee!.Email);
        }
    }
}
