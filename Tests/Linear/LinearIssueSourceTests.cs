using System;
using System.Linq;
using System.Net.Http;
using LinearGraphQL;
using NSubstitute;
using NUnit.Framework;
using Preesta;
using Serilog;
using Tests.Mocks;

namespace Tests.Linear
{
    [TestFixture]
    public class LinearIssueSourceTests
    {
        private const string FiveIssuesResponse = @"{
  ""data"": {
    ""viewer"": {
      ""assignedIssues"": {
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
  }
}";

        private const string EmptyResponse = @"{
  ""data"": { ""viewer"": { ""assignedIssues"": { ""nodes"": [] } } }
}";

        private const string ErrorResponse = @"{
  ""errors"": [
    { ""message"": ""Authentication required"", ""extensions"": { ""code"": ""AUTHENTICATION_ERROR"" } }
  ]
}";

        private const string FakeApiKey = "lin_api_FAKE_TEST_KEY";

        [Test]
        public void HappyPath_MapsAllFields()
        {
            using var server = new MockLinearServer();
            server.StubAssignedIssuesQuery(FiveIssuesResponse);

            var connection = new LinearConnection(FakeApiKey, server.GraphQlUrl);
            var source = new LinearIssueSource(connection);

            var issues = source.GetAssignedIssues();

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
        public void EmptyResult_ReturnsEmptyArray()
        {
            using var server = new MockLinearServer();
            server.StubAssignedIssuesQuery(EmptyResponse);

            var connection = new LinearConnection(FakeApiKey, server.GraphQlUrl);
            var source = new LinearIssueSource(connection);

            var issues = source.GetAssignedIssues();

            Assert.IsNotNull(issues);
            Assert.AreEqual(0, issues.Length);
        }

        [Test]
        public void GraphQlErrorResponse_ReturnsEmptyArrayAndLogs()
        {
            using var server = new MockLinearServer();
            server.StubAssignedIssuesQuery(ErrorResponse);

            var connection = new LinearConnection(FakeApiKey, server.GraphQlUrl);
            var logger = Substitute.For<ILogger>();
            var source = new LinearIssueSource(connection, logger);

            var issues = source.GetAssignedIssues();

            Assert.AreEqual(0, issues.Length);
            // The exact Serilog overload signature varies by argument count; just
            // assert that *some* Error call was made.
            logger.ReceivedCalls()
                .Where(c => c.GetMethodInfo().Name == "Error")
                .ToList(); // materialise

            Assert.IsTrue(
                logger.ReceivedCalls().Any(c => c.GetMethodInfo().Name == "Error"),
                "Expected ILogger.Error to be called when GraphQL response contains an errors array");
        }
    }
}
