using System;
using System.Collections.Generic;
using System.Linq;
using GitlabGraphQL;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using Preesta;
using Preesta.Configuration.Action;
using Serilog;

namespace Tests.Gitlab
{
    /// <summary>
    /// Unit tests against an NSubstitute IGitlabGateway — verifies GraphQL response
    /// mapping into the shared <see cref="Preesta.Data.Issue"/> model. WireMock-level
    /// transport tests can be added later if needed; for now mapping + variable
    /// construction is the only logic worth covering.
    /// </summary>
    [TestFixture]
    public class GitlabIssueSourceTests
    {
        // Mixed issues + edge cases (publicEmail null, no milestone, CLOSED, no labels).
        private const string FourNodesResponse = @"{
  ""data"": {
    ""issues"": {
      ""nodes"": [
        {
          ""id"": ""gid://gitlab/Issue/101"",
          ""iid"": ""42"",
          ""title"": ""Bug: thing broken"",
          ""webUrl"": ""https://gitlab.com/octo/repo/-/issues/42"",
          ""state"": ""opened"",
          ""createdAt"": ""2026-05-01T10:00:00Z"",
          ""updatedAt"": ""2026-05-08T12:00:00Z"",
          ""closedAt"": null,
          ""confidential"": false,
          ""author"":    { ""username"": ""alice"", ""name"": ""Alice A"", ""publicEmail"": ""alice@example.com"" },
          ""assignees"": { ""nodes"": [ { ""username"": ""bob"", ""name"": ""Bob Roberts"", ""publicEmail"": ""bob@example.com"" } ] },
          ""labels"":    { ""nodes"": [ { ""title"": ""bug"" }, { ""title"": ""urgent"" } ] },
          ""milestone"": { ""title"": ""v1.0"" },
          ""reference"": ""octo/repo#42""
        },
        {
          ""id"": ""gid://gitlab/Issue/102"",
          ""iid"": ""7"",
          ""title"": ""Hidden-email reporter"",
          ""webUrl"": ""https://gitlab.com/octo/repo/-/issues/7"",
          ""state"": ""opened"",
          ""createdAt"": ""2026-05-02T11:00:00Z"",
          ""updatedAt"": ""2026-05-09T13:00:00Z"",
          ""closedAt"": null,
          ""confidential"": false,
          ""author"":    { ""username"": ""carol"", ""name"": ""Carol C"", ""publicEmail"": null },
          ""assignees"": { ""nodes"": [] },
          ""labels"":    { ""nodes"": [] },
          ""milestone"": null,
          ""reference"": ""octo/repo#7""
        },
        {
          ""id"": ""gid://gitlab/Issue/103"",
          ""iid"": ""100"",
          ""title"": ""Old closed bug"",
          ""webUrl"": ""https://gitlab.com/octo/repo/-/issues/100"",
          ""state"": ""closed"",
          ""createdAt"": ""2026-01-01T00:00:00Z"",
          ""updatedAt"": ""2026-02-01T00:00:00Z"",
          ""closedAt"": ""2026-02-01T00:00:00Z"",
          ""confidential"": false,
          ""author"":    { ""username"": ""dave"", ""name"": null, ""publicEmail"": null },
          ""assignees"": { ""nodes"": [ { ""username"": ""alice"", ""name"": null, ""publicEmail"": ""alice@example.com"" } ] },
          ""labels"":    { ""nodes"": [] },
          ""milestone"": null,
          ""reference"": ""octo/repo#100""
        },
        {
          ""id"": ""gid://gitlab/Issue/104"",
          ""iid"": ""1"",
          ""title"": ""Cross-project issue"",
          ""webUrl"": ""https://gitlab.com/another/proj/-/issues/1"",
          ""state"": ""opened"",
          ""createdAt"": ""2026-05-05T10:00:00Z"",
          ""updatedAt"": ""2026-05-05T10:00:00Z"",
          ""closedAt"": null,
          ""confidential"": true,
          ""author"":    { ""username"": ""ghost"", ""name"": null, ""publicEmail"": null },
          ""assignees"": { ""nodes"": [] },
          ""labels"":    { ""nodes"": [] },
          ""milestone"": null,
          ""reference"": ""another/proj#1""
        }
      ]
    }
  }
}";

        private const string EmptyResponse = @"{ ""data"": { ""issues"": { ""nodes"": [] } } }";

        private const string ErrorResponse = @"{
  ""errors"": [
    { ""message"": ""Invalid token"" }
  ]
}";

        private static GitlabIssueSource SourceWith(string responseJson)
        {
            var gateway = Substitute.For<IGitlabGateway>();
            gateway.Query(Arg.Any<string>(), Arg.Any<object>()).Returns(JObject.Parse(responseJson));
            return new GitlabIssueSource(gateway, Substitute.For<ILogger>());
        }

        private static GitlabRule RuleFor(GitlabFilter? filter = null) =>
            new GitlabRule { Filter = filter ?? new GitlabFilter { State = "opened" } };

        [Test]
        public void HappyPath_MapsKeyAsGroupProjectNumber()
        {
            var issues = SourceWith(FourNodesResponse).GetIssues(RuleFor());

            Assert.AreEqual(4, issues.Length);
            Assert.AreEqual("octo/repo#42", issues[0].Key);
            Assert.AreEqual("octo/repo#7", issues[1].Key);
            Assert.AreEqual("another/proj#1", issues[3].Key);
        }

        [Test]
        public void HappyPath_PopulatesGitlabGlobalIdForMutationTarget()
        {
            var issues = SourceWith(FourNodesResponse).GetIssues(RuleFor());

            Assert.AreEqual("gid://gitlab/Issue/101", issues[0].GitlabGlobalId);
            Assert.AreEqual("gid://gitlab/Issue/102", issues[1].GitlabGlobalId);
        }

        [Test]
        public void TypeIsAlwaysIssue()
        {
            // Phase 13 covers Issues only — MRs are deferred. Every node should map
            // to Type="Issue" regardless of payload shape.
            var issues = SourceWith(FourNodesResponse).GetIssues(RuleFor());

            Assert.IsTrue(issues.All(i => i.Type == "Issue"));
        }

        [Test]
        public void StateNormalizedToTitleCase()
        {
            var issues = SourceWith(FourNodesResponse).GetIssues(RuleFor());

            Assert.AreEqual("Opened", issues[0].Status);
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
            // GitLab has no separate reporter; both should be the author.
            var issues = SourceWith(FourNodesResponse).GetIssues(RuleFor());

            Assert.AreEqual("alice@example.com", issues[0].Participants.Reporter!.Email);
            Assert.AreEqual("alice@example.com", issues[0].Participants.Creator!.Email);
            Assert.AreEqual("alice", issues[0].Participants.Reporter.Key);
        }

        [Test]
        public void HiddenPublicEmail_ProducesEmptyString_NotNull()
        {
            // GitLab returns null for publicEmail when the user has not exposed it
            // in profile settings. We keep the User object (login/displayName for
            // the digest header) but Email is empty so the marker resolver doesn't
            // produce an invalid "To: " line.
            var issues = SourceWith(FourNodesResponse).GetIssues(RuleFor());

            Assert.IsNotNull(issues[1].Participants.Reporter);
            Assert.AreEqual(string.Empty, issues[1].Participants.Reporter!.Email);
            Assert.AreEqual("carol", issues[1].Participants.Reporter.Key);
        }

        [Test]
        public void LabelsJoinedByComma_FromTitleField()
        {
            // GitLab's Label type uses `title`, not `name` — make sure we read the
            // right field. (We don't have a direct way to assert WHICH GraphQL field
            // was read; if mapping pulled `name` we'd just get empty strings here.)
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
            var gateway = Substitute.For<IGitlabGateway>();
            gateway.Query(Arg.Any<string>(), Arg.Any<object>()).Returns(JObject.Parse(ErrorResponse));
            var logger = Substitute.For<ILogger>();
            var source = new GitlabIssueSource(gateway, logger);

            var issues = source.GetIssues(RuleFor());

            Assert.AreEqual(0, issues.Length);
            Assert.IsTrue(logger.ReceivedCalls().Any(c => c.GetMethodInfo().Name == "Error"));
        }

        [Test]
        public void EmptyFilter_ReturnsEmptyAndDoesNotCallGateway()
        {
            // A GitlabRule whose Filter object has zero fields set must not even
            // attempt the query — GitLab's GraphQL refuses unfiltered scans, and we
            // want predictable "log + skip" behaviour at the source layer too (the
            // YAML parser already drops these before they reach the source, but the
            // contract should hold defensively).
            var gateway = Substitute.For<IGitlabGateway>();
            var source = new GitlabIssueSource(gateway, Substitute.For<ILogger>());

            var issues = source.GetIssues(new GitlabRule { Filter = new GitlabFilter() });

            Assert.AreEqual(0, issues.Length);
            gateway.DidNotReceive().Query(Arg.Any<string>(), Arg.Any<object>());
        }

        [Test]
        public void EmptySearchResult_ReturnsEmptyArray()
        {
            var issues = SourceWith(EmptyResponse).GetIssues(RuleFor());

            Assert.AreEqual(0, issues.Length);
        }

        [Test]
        public void BuildVariables_OnlyMaterialisesSetFields()
        {
            // Don't send a wall of `null` properties to GitLab — only the chips the
            // user actually configured should appear in the GraphQL `$variables`.
            var filter = new GitlabFilter
            {
                State = "opened",
                LabelName = new[] { "urgent" },
                AssigneeUsernames = new[] { "alice", "bob" },
                Search = "checkout"
            };
            var vars = (Dictionary<string, object?>)GitlabIssueSource.BuildVariables(filter);

            Assert.IsTrue(vars.ContainsKey("state"));
            Assert.IsTrue(vars.ContainsKey("labelName"));
            Assert.IsTrue(vars.ContainsKey("assigneeUsernames"));
            Assert.IsTrue(vars.ContainsKey("search"));
            Assert.IsFalse(vars.ContainsKey("authorUsername"));
            Assert.IsFalse(vars.ContainsKey("confidential"));
            Assert.IsFalse(vars.ContainsKey("createdAfter"));
            // State coerced to lower-case for GraphQL IssuableState enum compatibility.
            Assert.AreEqual("opened", vars["state"]);
        }

        [Test]
        public void BuildVariables_PassesArraysAsArrays()
        {
            // labelName and assigneeUsernames are [String!] in GraphQL — they must
            // serialise as JSON arrays, not concatenated strings.
            var filter = new GitlabFilter
            {
                LabelName = new[] { "a", "b", "c" }
            };
            var vars = (Dictionary<string, object?>)GitlabIssueSource.BuildVariables(filter);

            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, (string[])vars["labelName"]!);
        }
    }
}
