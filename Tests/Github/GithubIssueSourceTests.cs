using System;
using System.Linq;
using GithubGraphQL;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using Preesta;
using Preesta.Configuration.Action;
using Serilog;

namespace Tests.Github
{
    /// <summary>
    /// Unit tests against an NSubstitute IGithubGateway — verifies GraphQL response
    /// mapping into the shared <see cref="Preesta.Data.Issue"/> model. Integration
    /// tests against a real-ish HTTP server (WireMock) can be added later if
    /// transport-level concerns arise; for now mapping is the only logic worth
    /// covering.
    /// </summary>
    [TestFixture]
    public class GithubIssueSourceTests
    {
        // Mixed Issue + PR + edge cases (assignee.email hidden, no milestone, CLOSED).
        private const string FourNodesResponse = @"{
  ""data"": {
    ""search"": {
      ""nodes"": [
        {
          ""__typename"": ""Issue"",
          ""id"": ""I_kwDOABC1"",
          ""number"": 42,
          ""title"": ""Bug: thing broken"",
          ""url"": ""https://github.com/octo/repo/issues/42"",
          ""state"": ""OPEN"",
          ""createdAt"": ""2026-05-01T10:00:00Z"",
          ""updatedAt"": ""2026-05-08T12:00:00Z"",
          ""closedAt"": null,
          ""author"": { ""login"": ""alice"", ""email"": ""alice@example.com"" },
          ""assignees"": { ""nodes"": [ { ""login"": ""bob"", ""email"": ""bob@example.com"", ""name"": ""Bob Roberts"" } ] },
          ""labels"":    { ""nodes"": [ { ""name"": ""bug"" }, { ""name"": ""urgent"" } ] },
          ""repository"": { ""nameWithOwner"": ""octo/repo"" },
          ""milestone"":  { ""title"": ""v1.0"" }
        },
        {
          ""__typename"": ""PullRequest"",
          ""id"": ""PR_kwDOABC2"",
          ""number"": 7,
          ""title"": ""Add feature X"",
          ""url"": ""https://github.com/octo/repo/pull/7"",
          ""state"": ""OPEN"",
          ""createdAt"": ""2026-05-02T11:00:00Z"",
          ""updatedAt"": ""2026-05-09T13:00:00Z"",
          ""closedAt"": null,
          ""author"": { ""login"": ""carol"", ""email"": """" },
          ""assignees"": { ""nodes"": [] },
          ""labels"":    { ""nodes"": [] },
          ""repository"": { ""nameWithOwner"": ""octo/repo"" },
          ""milestone"":  null
        },
        {
          ""__typename"": ""Issue"",
          ""id"": ""I_kwDOABC3"",
          ""number"": 100,
          ""title"": ""Old closed bug"",
          ""url"": ""https://github.com/octo/repo/issues/100"",
          ""state"": ""CLOSED"",
          ""createdAt"": ""2026-01-01T00:00:00Z"",
          ""updatedAt"": ""2026-02-01T00:00:00Z"",
          ""closedAt"": ""2026-02-01T00:00:00Z"",
          ""author"": { ""login"": ""dave"" },
          ""assignees"": { ""nodes"": [ { ""login"": ""alice"", ""email"": ""alice@example.com"", ""name"": null } ] },
          ""labels"":    { ""nodes"": [] },
          ""repository"": { ""nameWithOwner"": ""octo/repo"" },
          ""milestone"":  null
        },
        {
          ""__typename"": ""Issue"",
          ""id"": ""I_kwDOXYZ4"",
          ""number"": 1,
          ""title"": ""Cross-repo issue"",
          ""url"": ""https://github.com/another/proj/issues/1"",
          ""state"": ""OPEN"",
          ""createdAt"": ""2026-05-05T10:00:00Z"",
          ""updatedAt"": ""2026-05-05T10:00:00Z"",
          ""closedAt"": null,
          ""author"": { ""login"": ""ghost"" },
          ""assignees"": { ""nodes"": [] },
          ""labels"":    { ""nodes"": [] },
          ""repository"": { ""nameWithOwner"": ""another/proj"" },
          ""milestone"":  null
        }
      ]
    }
  }
}";

        private const string EmptyResponse = @"{ ""data"": { ""search"": { ""nodes"": [] } } }";

        private const string ErrorResponse = @"{
  ""errors"": [
    { ""message"": ""Bad credentials"" }
  ]
}";

        private static GithubIssueSource SourceWith(string responseJson)
        {
            var gateway = Substitute.For<IGithubGateway>();
            gateway.Query(Arg.Any<string>(), Arg.Any<object>()).Returns(JObject.Parse(responseJson));
            return new GithubIssueSource(gateway, Substitute.For<ILogger>());
        }

        private static GithubRule RuleFor(string filter = "is:open is:issue org:octo") =>
            new GithubRule { Filter = filter };

        [Test]
        public void HappyPath_MapsKeyAsOwnerRepoNumber()
        {
            var issues = SourceWith(FourNodesResponse).GetIssues(RuleFor());

            Assert.AreEqual(4, issues.Length);
            Assert.AreEqual("octo/repo#42", issues[0].Key);
            Assert.AreEqual("octo/repo#7", issues[1].Key);
            Assert.AreEqual("another/proj#1", issues[3].Key);
        }

        [Test]
        public void HappyPath_PopulatesGithubNodeIdForMutationTarget()
        {
            var issues = SourceWith(FourNodesResponse).GetIssues(RuleFor());

            Assert.AreEqual("I_kwDOABC1", issues[0].GithubNodeId);
            Assert.AreEqual("PR_kwDOABC2", issues[1].GithubNodeId);
        }

        [Test]
        public void TypeReflectsTypename()
        {
            var issues = SourceWith(FourNodesResponse).GetIssues(RuleFor());

            Assert.AreEqual("Issue", issues[0].Type);
            Assert.AreEqual("PR", issues[1].Type);
            Assert.AreEqual("Issue", issues[2].Type);
        }

        [Test]
        public void StateNormalizedToTitleCase()
        {
            var issues = SourceWith(FourNodesResponse).GetIssues(RuleFor());

            Assert.AreEqual("Open", issues[0].Status);
            Assert.AreEqual("Closed", issues[2].Status);
        }

        [Test]
        public void ClosedIssue_ResolutionSet()
        {
            var issues = SourceWith(FourNodesResponse).GetIssues(RuleFor());

            Assert.AreEqual("Closed", issues[2].Resolution);
            Assert.IsNull(issues[0].Resolution);
        }

        [Test]
        public void AssigneeMappedFromFirstAssigneeNode()
        {
            var issues = SourceWith(FourNodesResponse).GetIssues(RuleFor());

            Assert.IsNotNull(issues[0].Participants.Assignee);
            Assert.AreEqual("bob@example.com", issues[0].Participants.Assignee!.Email);
            Assert.AreEqual("Bob Roberts", issues[0].Participants.Assignee.DisplayName);
            Assert.AreEqual("bob", issues[0].Participants.Assignee.Key);

            // No assignees → null
            Assert.IsNull(issues[1].Participants.Assignee);
        }

        [Test]
        public void ReporterAndCreator_BothFromAuthor()
        {
            // GitHub has no separate reporter; both should be the author.
            var issues = SourceWith(FourNodesResponse).GetIssues(RuleFor());

            Assert.AreEqual("alice@example.com", issues[0].Participants.Reporter!.Email);
            Assert.AreEqual("alice@example.com", issues[0].Participants.Creator!.Email);
            Assert.AreEqual("alice", issues[0].Participants.Reporter.Key);
        }

        [Test]
        public void HiddenEmail_ProducesEmptyString_NotNull()
        {
            // GitHub returns "" when user has hidden email — we keep the User
            // object (for displayName/login) but Email is empty so the marker
            // resolver won't produce an invalid "To: " line.
            var issues = SourceWith(FourNodesResponse).GetIssues(RuleFor());

            Assert.IsNotNull(issues[1].Participants.Reporter);
            Assert.AreEqual(string.Empty, issues[1].Participants.Reporter!.Email);
            Assert.AreEqual("carol", issues[1].Participants.Reporter.Key);
        }

        [Test]
        public void LabelsJoinedByComma()
        {
            var issues = SourceWith(FourNodesResponse).GetIssues(RuleFor());

            Assert.AreEqual("bug, urgent", issues[0].Labels);
            Assert.AreEqual(string.Empty, issues[1].Labels);
        }

        [Test]
        public void Milestone_MappedToProjectKey()
        {
            var issues = SourceWith(FourNodesResponse).GetIssues(RuleFor());

            Assert.AreEqual("v1.0", issues[0].ProjectKey);
            Assert.IsNull(issues[1].ProjectKey);
        }

        [Test]
        public void Dates_ParsedAsUtc()
        {
            var issues = SourceWith(FourNodesResponse).GetIssues(RuleFor());

            Assert.AreEqual(new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc), issues[0].CreatedDate);
            Assert.AreEqual(new DateTime(2026, 5, 8, 12, 0, 0, DateTimeKind.Utc), issues[0].UpdatedDate);
        }

        [Test]
        public void GraphQLErrorEnvelope_ReturnsEmptyAndLogsError()
        {
            var gateway = Substitute.For<IGithubGateway>();
            gateway.Query(Arg.Any<string>(), Arg.Any<object>()).Returns(JObject.Parse(ErrorResponse));
            var logger = Substitute.For<ILogger>();
            var source = new GithubIssueSource(gateway, logger);

            var issues = source.GetIssues(RuleFor());

            Assert.AreEqual(0, issues.Length);
            Assert.IsTrue(logger.ReceivedCalls().Any(c => c.GetMethodInfo().Name == "Error"));
        }

        [Test]
        public void EmptyFilter_ReturnsEmptyAndDoesNotCallGateway()
        {
            var gateway = Substitute.For<IGithubGateway>();
            var source = new GithubIssueSource(gateway, Substitute.For<ILogger>());

            var issues = source.GetIssues(new GithubRule { Filter = "  " });

            Assert.AreEqual(0, issues.Length);
            gateway.DidNotReceive().Query(Arg.Any<string>(), Arg.Any<object>());
        }

        [Test]
        public void EmptySearchResult_ReturnsEmptyArray()
        {
            var issues = SourceWith(EmptyResponse).GetIssues(RuleFor());

            Assert.AreEqual(0, issues.Length);
        }
    }
}
