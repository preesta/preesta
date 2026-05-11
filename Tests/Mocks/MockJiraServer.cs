using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using WireMock.Logging;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Tests.Mocks
{
    /// <summary>
    /// In-process HTTP mock server for Jira REST tests, built on WireMock.Net.
    /// <para>
    /// Each instance starts a real HTTP listener on a free port. Tests construct
    /// a <see cref="JiraRest.Connection"/> (or <see cref="JiraRest.CloudConnection"/>)
    /// against <see cref="Url"/>, and stub responses through the <c>StubXxx</c>
    /// helpers below. After the test runs, recorded calls can be inspected
    /// through <see cref="LogEntries"/>. The server is stopped on
    /// <see cref="Dispose"/>.
    /// </para>
    /// <para>
    /// This class replaces the previous <c>StubDelegatingHandler</c> approach:
    /// requests now go through real sockets, so URL building, query strings
    /// and request bodies are validated end-to-end.
    /// </para>
    /// </summary>
    internal sealed class MockJiraServer : IDisposable
    {
        private readonly WireMockServer _server;

        public MockJiraServer()
        {
            _server = WireMockServer.Start();
        }

        /// <summary>Base URL of the running mock server, e.g. <c>http://localhost:54321</c>.</summary>
        public string Url => _server.Url ?? throw new InvalidOperationException("WireMock server has no URL");

        /// <summary>All HTTP calls received by the server, in order.</summary>
        public IEnumerable<ILogEntry> LogEntries => _server.LogEntries;

        /// <summary>
        /// Stub <c>GET /rest/api/2/search?jql=...</c> (Jira Server-style).
        /// Matches when the <c>jql</c> query parameter contains <paramref name="jqlSubstring"/>.
        /// </summary>
        public void StubGetIssuesByJql(string jqlSubstring, string jsonResponse)
        {
            _server
                .Given(Request.Create()
                    .WithPath("/rest/api/2/search")
                    .UsingGet()
                    .WithParam("jql", new WildcardMatcher($"*{jqlSubstring}*")))
                .RespondWith(JsonOk(jsonResponse));
        }

        /// <summary>
        /// Stub <c>POST /rest/api/3/search/jql</c> (Jira Cloud-style) with a JSON body
        /// whose <c>jql</c> field contains <paramref name="jqlSubstring"/>.
        /// </summary>
        public void StubCloudSearchJql(string jqlSubstring, string jsonResponse)
        {
            _server
                .Given(Request.Create()
                    .WithPath("/rest/api/3/search/jql")
                    .UsingPost()
                    .WithBody(new JsonPartialMatcher(new { jql = $"*{jqlSubstring}*" })))
                .RespondWith(JsonOk(jsonResponse));
        }

        /// <summary>Stub <c>GET /rest/api/2/issue/{key}</c>.</summary>
        public void StubGetIssue(string issueKey, string jsonResponse)
        {
            _server
                .Given(Request.Create()
                    .WithPath($"/rest/api/2/issue/{issueKey}")
                    .UsingGet())
                .RespondWith(JsonOk(jsonResponse));
        }

        /// <summary>
        /// Stub <c>POST /rest/api/2/issue/{key}*</c> returning HTTP 200 with empty body.
        /// Path is treated as a prefix so transition sub-paths (e.g. <c>/transitions</c>) match.
        /// </summary>
        public void StubPostIssue(string issueKey)
        {
            _server
                .Given(Request.Create()
                    .WithPath(new WildcardMatcher($"/rest/api/2/issue/{issueKey}*"))
                    .UsingPost())
                .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK).WithBody(""));
        }

        /// <summary>
        /// Stub <c>PUT /rest/api/2/issue/{key}*</c> returning HTTP 200 with empty body.
        /// Path is treated as a prefix so sub-paths match too.
        /// </summary>
        public void StubPutIssue(string issueKey)
        {
            _server
                .Given(Request.Create()
                    .WithPath(new WildcardMatcher($"/rest/api/2/issue/{issueKey}*"))
                    .UsingPut())
                .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK).WithBody(""));
        }

        /// <summary>
        /// Stub a generic write (POST/PUT) to any URL path. Useful when the path
        /// is dynamic (e.g. an external system named via <c>UrlPattern</c>).
        /// </summary>
        public void StubAnyWrite()
        {
            _server
                .Given(Request.Create().UsingPost())
                .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK).WithBody(""));
            _server
                .Given(Request.Create().UsingPut())
                .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK).WithBody(""));
        }

        /// <summary>Stub <c>GET /rest/api/2/project/{code}/versions</c>.</summary>
        public void StubReleases(string projectCode, string jsonResponse)
        {
            _server
                .Given(Request.Create()
                    .WithPath($"/rest/api/2/project/{projectCode}/versions")
                    .UsingGet())
                .RespondWith(JsonOk(jsonResponse));
        }

        /// <summary>
        /// Stub <c>GET /rest/api/2/field</c> (Server) / <c>/rest/api/3/field</c> (Cloud)
        /// with the supplied JSON array. Both endpoints have the same response shape.
        /// </summary>
        public void StubGetFields(string jsonResponseArray)
        {
            _server
                .Given(Request.Create()
                    .WithPath(new WildcardMatcher("/rest/api/?/field"))
                    .UsingGet())
                .RespondWith(JsonOk(jsonResponseArray));
        }

        /// <summary>
        /// Stub <c>GET /rest/api/?/field</c> with the given HTTP status (e.g. 403).
        /// Used to verify HttpJiraService.GetCustomFieldMap swallows failures.
        /// </summary>
        public void StubGetFieldsError(int statusCode)
        {
            _server
                .Given(Request.Create()
                    .WithPath(new WildcardMatcher("/rest/api/?/field"))
                    .UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(statusCode)
                    .WithBody("{\"errorMessage\":\"forbidden\"}"));
        }

        /// <summary>
        /// Convenience: count recorded requests matching a method + absolute URL.
        /// </summary>
        public int CountRequests(string method, string absoluteUrl) =>
            LogEntries.Count(e =>
                e.RequestMessage != null
                && string.Equals(e.RequestMessage.Method, method, StringComparison.OrdinalIgnoreCase)
                && string.Equals(e.RequestMessage.AbsoluteUrl, absoluteUrl, StringComparison.Ordinal));

        public void Dispose()
        {
            _server.Stop();
            _server.Dispose();
        }

        private static IResponseBuilder JsonOk(string body) =>
            Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body);
    }
}
