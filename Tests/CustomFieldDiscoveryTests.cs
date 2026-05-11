using System.Linq;
using NUnit.Framework;
using NSubstitute;
using Preesta;
using Serilog;
using Tests.Mocks;

namespace Tests
{
    /// <summary>
    /// Phase 12 (Custom Fields): HttpJiraService.GetCustomFieldMap calls
    /// /rest/api/?/field at most once on demand, builds a case-insensitive
    /// name→id map keyed by display name, logs duplicates and HTTP failures
    /// without throwing.
    /// </summary>
    [TestFixture]
    public class CustomFieldDiscoveryTests
    {
        private const string TwoCustomFieldsResponse = @"[
  { ""id"": ""customfield_10001"", ""name"": ""Severity"",      ""custom"": true },
  { ""id"": ""customfield_10002"", ""name"": ""Story Points"",  ""custom"": true },
  { ""id"": ""summary"",            ""name"": ""Summary"",       ""custom"": false },
  { ""id"": ""status"",             ""name"": ""Status"",        ""custom"": false }
]";

        // Two custom fields share the display name "Sprint" — one is from a
        // miscreated Jira import. First one wins, second logs a warning.
        private const string AmbiguousResponse = @"[
  { ""id"": ""customfield_10100"", ""name"": ""Sprint"", ""custom"": true },
  { ""id"": ""customfield_10101"", ""name"": ""Sprint"", ""custom"": true }
]";

        [Test]
        public void GetCustomFieldMap_HappyPath_OnlyCustomFieldsByName()
        {
            using var server = new MockJiraServer();
            server.StubGetFields(TwoCustomFieldsResponse);

            var jira = new HttpJiraService(server.Url, "user", "pass", logger: Substitute.For<ILogger>());
            var map = jira.GetCustomFieldMap();

            Assert.AreEqual(2, map.Count);
            Assert.AreEqual("customfield_10001", map["Severity"]);
            Assert.AreEqual("customfield_10002", map["Story Points"]);
            // Case-insensitive lookup.
            Assert.AreEqual("customfield_10001", map["severity"]);
            Assert.AreEqual("customfield_10001", map["SEVERITY"]);
            // System fields are excluded.
            Assert.IsFalse(map.ContainsKey("Summary"));
            Assert.IsFalse(map.ContainsKey("Status"));
        }

        [Test]
        public void GetCustomFieldMap_DuplicateDisplayName_FirstWinsAndLogsWarning()
        {
            using var server = new MockJiraServer();
            server.StubGetFields(AmbiguousResponse);
            var logger = Substitute.For<ILogger>();

            var jira = new HttpJiraService(server.Url, "user", "pass", logger: logger);
            var map = jira.GetCustomFieldMap();

            Assert.AreEqual(1, map.Count);
            Assert.AreEqual("customfield_10100", map["Sprint"]); // first wins

            var warnings = logger.ReceivedCalls()
                .Count(c => c.GetMethodInfo().Name == "Warning");
            Assert.GreaterOrEqual(warnings, 1, "Expected at least one warning log for the ambiguous Sprint name");
        }

        [Test]
        public void GetCustomFieldMap_EndpointFailure_ReturnsEmptyMapAndLogsWarning()
        {
            using var server = new MockJiraServer();
            server.StubGetFieldsError(403);
            var logger = Substitute.For<ILogger>();

            var jira = new HttpJiraService(server.Url, "user", "pass", logger: logger);
            var map = jira.GetCustomFieldMap();

            Assert.AreEqual(0, map.Count);
            var warnings = logger.ReceivedCalls()
                .Count(c => c.GetMethodInfo().Name == "Warning");
            Assert.GreaterOrEqual(warnings, 1, "Expected a warning log on /field failure");
        }
    }
}
