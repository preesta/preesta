using System;
using System.Linq;
using LinearGraphQL;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using Preesta;
using Preesta.Configuration.Action;
using Serilog;
using Tests.Mocks;

namespace Tests.Linear
{
    [TestFixture]
    public class LinearIssueSourceTests
    {
        // Issues-shape response — used by both raw-filter and (post-suggestion) AI-prompt
        // tests. data.issues.nodes matches what `issues(filter: $filter)` returns.
        private const string FiveIssuesResponse = @"{
  ""data"": {
    ""issues"": {
      ""nodes"": [
        {
          ""identifier"": ""PRE-1"",
          ""title"": ""Set up CI"",
          ""url"": ""https://linear.app/preesta-dev/issue/PRE-1"",
          ""state"": { ""name"": ""In Progress"", ""type"": ""started"" },
          ""priority"": 1,
          ""priorityLabel"": ""Urgent"",
          ""assignee"": { ""id"": ""u1"", ""name"": ""Valentin"", ""email"": ""valentin@example.com"" },
          ""creator"":  { ""id"": ""u1"", ""name"": ""Valentin"", ""email"": ""valentin@example.com"" },
          ""project"":  { ""id"": ""p1"", ""name"": ""Platform"" },
          ""labels"":   { ""nodes"": [ { ""name"": ""infra"" }, { ""name"": ""ci"" } ] },
          ""dueDate"":   ""2026-06-01"",
          ""createdAt"": ""2026-05-01T10:00:00.000Z"",
          ""updatedAt"": ""2026-05-08T12:00:00.000Z""
        },
        {
          ""identifier"": ""PRE-2"",
          ""title"": ""Triage backlog"",
          ""url"": ""https://linear.app/preesta-dev/issue/PRE-2"",
          ""state"": { ""name"": ""Todo"", ""type"": ""unstarted"" },
          ""priority"": 2,
          ""priorityLabel"": ""High"",
          ""assignee"": { ""id"": ""u1"", ""name"": ""Valentin"", ""email"": ""valentin@example.com"" },
          ""creator"":  { ""id"": ""u2"", ""name"": ""Other"",     ""email"": ""other@example.com"" },
          ""project"":  null,
          ""labels"":   { ""nodes"": [] },
          ""dueDate"":   null,
          ""createdAt"": ""2026-04-20T09:00:00.000Z"",
          ""updatedAt"": ""2026-05-07T08:00:00.000Z""
        },
        {
          ""identifier"": ""PRE-3"",
          ""title"": ""Write docs"",
          ""url"": ""https://linear.app/preesta-dev/issue/PRE-3"",
          ""state"": { ""name"": ""Todo"", ""type"": ""unstarted"" },
          ""priority"": 3,
          ""priorityLabel"": ""Medium"",
          ""assignee"": { ""id"": ""u1"", ""name"": ""Valentin"", ""email"": ""valentin@example.com"" },
          ""creator"":  { ""id"": ""u1"", ""name"": ""Valentin"", ""email"": ""valentin@example.com"" },
          ""project"":  { ""id"": ""p1"", ""name"": ""Platform"" },
          ""labels"":   { ""nodes"": [ { ""name"": ""docs"" } ] },
          ""dueDate"":   null,
          ""createdAt"": ""2026-04-25T09:00:00.000Z"",
          ""updatedAt"": ""2026-05-06T11:00:00.000Z""
        },
        {
          ""identifier"": ""PRE-4"",
          ""title"": ""Reproduce bug"",
          ""url"": ""https://linear.app/preesta-dev/issue/PRE-4"",
          ""state"": { ""name"": ""In Review"", ""type"": ""started"" },
          ""priority"": 2,
          ""priorityLabel"": ""High"",
          ""assignee"": { ""id"": ""u1"", ""name"": ""Valentin"", ""email"": ""valentin@example.com"" },
          ""creator"":  { ""id"": ""u2"", ""name"": ""Other"",     ""email"": ""other@example.com"" },
          ""project"":  { ""id"": ""p2"", ""name"": ""Bugs"" },
          ""labels"":   { ""nodes"": [] },
          ""dueDate"":   null,
          ""createdAt"": ""2026-05-03T09:00:00.000Z"",
          ""updatedAt"": ""2026-05-08T10:00:00.000Z""
        },
        {
          ""identifier"": ""PRE-5"",
          ""title"": ""Plan Q3"",
          ""url"": ""https://linear.app/preesta-dev/issue/PRE-5"",
          ""state"": { ""name"": ""Todo"", ""type"": ""unstarted"" },
          ""priority"": 4,
          ""priorityLabel"": ""Low"",
          ""assignee"": { ""id"": ""u1"", ""name"": ""Valentin"", ""email"": ""valentin@example.com"" },
          ""creator"":  { ""id"": ""u1"", ""name"": ""Valentin"", ""email"": ""valentin@example.com"" },
          ""project"":  null,
          ""labels"":   { ""nodes"": [] },
          ""dueDate"":   null,
          ""createdAt"": ""2026-05-04T09:00:00.000Z"",
          ""updatedAt"": ""2026-05-05T11:00:00.000Z""
        }
      ]
    }
  }
}";

        private const string EmptyIssuesResponse = @"{
  ""data"": { ""issues"": { ""nodes"": [] } }
}";

        private const string ErrorResponse = @"{
  ""errors"": [
    { ""message"": ""Authentication required"", ""extensions"": { ""code"": ""AUTHENTICATION_ERROR"" } }
  ]
}";

        private const string CustomViewTwoIssuesResponse = @"{
  ""data"": {
    ""customView"": {
      ""issues"": {
        ""nodes"": [
          {
            ""identifier"": ""PRE-10"",
            ""title"": ""View issue A"",
            ""url"": ""https://linear.app/preesta-dev/issue/PRE-10"",
            ""state"": { ""name"": ""Todo"", ""type"": ""unstarted"" },
            ""priority"": 2,
            ""priorityLabel"": ""High"",
            ""assignee"": null,
            ""creator"":  { ""id"": ""u1"", ""name"": ""Valentin"", ""email"": ""valentin@example.com"" },
            ""project"":  null,
            ""labels"":   { ""nodes"": [] },
            ""dueDate"":   null,
            ""createdAt"": ""2026-05-01T10:00:00.000Z"",
            ""updatedAt"": ""2026-05-08T12:00:00.000Z""
          },
          {
            ""identifier"": ""PRE-11"",
            ""title"": ""View issue B"",
            ""url"": ""https://linear.app/preesta-dev/issue/PRE-11"",
            ""state"": { ""name"": ""Done"", ""type"": ""completed"" },
            ""priority"": 3,
            ""priorityLabel"": ""Medium"",
            ""assignee"": { ""id"": ""u1"", ""name"": ""Valentin"", ""email"": ""valentin@example.com"" },
            ""creator"":  { ""id"": ""u1"", ""name"": ""Valentin"", ""email"": ""valentin@example.com"" },
            ""project"":  null,
            ""labels"":   { ""nodes"": [] },
            ""dueDate"":   null,
            ""createdAt"": ""2026-04-20T09:00:00.000Z"",
            ""updatedAt"": ""2026-05-07T08:00:00.000Z""
          }
        ]
      }
    }
  }
}";

        private const string FakeApiKey = "lin_api_FAKE_TEST_KEY";

        private static JObject SuggestedFilter() =>
            JObject.Parse(@"{ ""state"": { ""type"": { ""neq"": ""completed"" } } }");

        private static LinearRule RawFilterRule() => new LinearRule
        {
            FilterRaw = JObject.Parse(@"{ ""state"": { ""type"": { ""neq"": ""completed"" } } }")
        };

        private static LinearRule PromptRule(string prompt = "issues assigned to me, not completed") =>
            new LinearRule { Filter = prompt };

        private static LinearRule ViewIdRule(string id = "0e8a3b41-1234-4321-aaaa-bbbbbbbbbbbb") =>
            new LinearRule { ViewId = id };

        // ----- Raw filter mode -----

        [Test]
        public void RawFilter_HappyPath_MapsAllFields()
        {
            using var server = new MockLinearServer();
            server.StubIssuesQuery(FiveIssuesResponse);

            var connection = new LinearConnection(FakeApiKey, server.GraphQlUrl);
            var source = new LinearIssueSource(connection);

            var issues = source.GetIssues(RawFilterRule());

            Assert.AreEqual(5, issues.Length);

            var first = issues[0];
            Assert.AreEqual("PRE-1", first.Key);
            Assert.AreEqual("Set up CI", first.Summary);
            Assert.AreEqual("https://linear.app/preesta-dev/issue/PRE-1", first.Url);
            Assert.AreEqual("In Progress", first.Status);
            Assert.AreEqual("Urgent", first.Priority);
            Assert.IsNotNull(first.Participants.Assignee);
            Assert.AreEqual("valentin@example.com", first.Participants.Assignee!.Email);
            Assert.AreEqual("Valentin", first.Participants.Assignee.Name);
            Assert.IsNotNull(first.Participants.Reporter);
            Assert.AreEqual("valentin@example.com", first.Participants.Reporter!.Email);
            Assert.AreEqual("Platform", first.ProjectKey);
            Assert.AreEqual("infra, ci", first.Labels);
            Assert.IsNotNull(first.UpdatedDate);
            Assert.AreEqual(new DateTime(2026, 5, 8, 12, 0, 0, DateTimeKind.Utc),
                first.UpdatedDate!.Value.ToUniversalTime());
            Assert.IsNull(first.Resolution); // state.type != completed
        }

        [Test]
        public void RawFilter_EmptyResult_ReturnsEmptyArray()
        {
            using var server = new MockLinearServer();
            server.StubIssuesQuery(EmptyIssuesResponse);

            var connection = new LinearConnection(FakeApiKey, server.GraphQlUrl);
            var source = new LinearIssueSource(connection);

            var issues = source.GetIssues(RawFilterRule());

            Assert.IsNotNull(issues);
            Assert.AreEqual(0, issues.Length);
        }

        [Test]
        public void RawFilter_GraphQlErrorResponse_ReturnsEmptyArrayAndLogs()
        {
            using var server = new MockLinearServer();
            server.StubIssuesQuery(ErrorResponse);

            var connection = new LinearConnection(FakeApiKey, server.GraphQlUrl);
            var logger = Substitute.For<ILogger>();
            var source = new LinearIssueSource(connection, logger);

            var issues = source.GetIssues(RawFilterRule());

            Assert.AreEqual(0, issues.Length);
            Assert.IsTrue(
                logger.ReceivedCalls().Any(c => c.GetMethodInfo().Name == "Error"),
                "Expected ILogger.Error to be called when GraphQL response contains an errors array");
        }

        // ----- AI prompt mode (2-hop fetch) -----

        [Test]
        public void Prompt_HappyPath_TwoHopFetch()
        {
            using var server = new MockLinearServer();
            // Order matters when using WireMock.Net regex matchers — register the
            // (more specific) suggestion stub last so it's evaluated first.
            server.StubIssuesQuery(FiveIssuesResponse);
            server.StubFilterSuggestionQuery("not completed", SuggestedFilter());

            var connection = new LinearConnection(FakeApiKey, server.GraphQlUrl);
            var source = new LinearIssueSource(connection);

            var issues = source.GetIssues(PromptRule("issues assigned to me, not completed"));

            Assert.AreEqual(5, issues.Length);
            Assert.AreEqual("PRE-1", issues[0].Key);
        }

        [Test]
        public void Prompt_SuggestionReturnsNullFilter_ReturnsEmptyArrayAndLogsWarning()
        {
            using var server = new MockLinearServer();
            // Linear can return a null filter for ambiguous prompts; we should
            // skip the second hop and warn.
            const string nullFilterResponse =
                @"{ ""data"": { ""issueFilterSuggestion"": { ""filter"": null } } }";
            server.StubAssignedIssuesQuery(nullFilterResponse);

            var connection = new LinearConnection(FakeApiKey, server.GraphQlUrl);
            var logger = Substitute.For<ILogger>();
            var source = new LinearIssueSource(connection, logger);

            var issues = source.GetIssues(PromptRule("ambiguous prompt"));

            Assert.AreEqual(0, issues.Length);
            Assert.IsTrue(
                logger.ReceivedCalls().Any(c => c.GetMethodInfo().Name == "Warning"),
                "Expected ILogger.Warning when issueFilterSuggestion returns no filter");
        }

        [Test]
        public void Prompt_SuggestionReturnsErrors_ReturnsEmptyArrayAndLogs()
        {
            using var server = new MockLinearServer();
            // The generic body-less stub matches the first request (suggestion),
            // returning the GraphQL error envelope.
            server.StubAssignedIssuesQuery(ErrorResponse);

            var connection = new LinearConnection(FakeApiKey, server.GraphQlUrl);
            var logger = Substitute.For<ILogger>();
            var source = new LinearIssueSource(connection, logger);

            var issues = source.GetIssues(PromptRule());

            Assert.AreEqual(0, issues.Length);
            Assert.IsTrue(
                logger.ReceivedCalls().Any(c => c.GetMethodInfo().Name == "Error"));
        }

        // ----- ViewId mode -----

        [Test]
        public void ViewId_HappyPath_MapsAllFields()
        {
            using var server = new MockLinearServer();
            const string viewId = "0e8a3b41-1234-4321-aaaa-bbbbbbbbbbbb";
            server.StubCustomViewQuery(viewId, CustomViewTwoIssuesResponse);

            var connection = new LinearConnection(FakeApiKey, server.GraphQlUrl);
            var source = new LinearIssueSource(connection);

            var issues = source.GetIssues(ViewIdRule(viewId));

            Assert.AreEqual(2, issues.Length);
            Assert.AreEqual("PRE-10", issues[0].Key);
            Assert.AreEqual("View issue A", issues[0].Summary);
            // PRE-11 has state.type == completed, so Resolution should be set.
            Assert.AreEqual("Done", issues[1].Resolution);
        }

        [Test]
        public void ViewId_ErrorResponse_ReturnsEmptyArray()
        {
            using var server = new MockLinearServer();
            const string viewId = "deadbeef-0000-0000-0000-000000000000";
            server.StubCustomViewQuery(viewId, ErrorResponse);

            var connection = new LinearConnection(FakeApiKey, server.GraphQlUrl);
            var logger = Substitute.For<ILogger>();
            var source = new LinearIssueSource(connection, logger);

            var issues = source.GetIssues(ViewIdRule(viewId));

            Assert.AreEqual(0, issues.Length);
            Assert.IsTrue(logger.ReceivedCalls().Any(c => c.GetMethodInfo().Name == "Error"));
        }

        // ----- Defensive: malformed rule with no source set -----

        [Test]
        public void NoFilterSet_ReturnsEmptyArrayAndLogsWarning()
        {
            // GetLinearRules drops these before they hit the source, but we still
            // defend against a hand-constructed rule.
            var gateway = Substitute.For<ILinearGateway>();
            var logger = Substitute.For<ILogger>();
            var source = new LinearIssueSource(gateway, logger);

            var issues = source.GetIssues(new LinearRule());

            Assert.AreEqual(0, issues.Length);
            gateway.DidNotReceive().Query(Arg.Any<string>(), Arg.Any<object?>());
            Assert.IsTrue(logger.ReceivedCalls().Any(c => c.GetMethodInfo().Name == "Warning"));
        }
    }
}
