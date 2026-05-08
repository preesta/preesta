using System;
using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json.Linq;
using WireMock.Logging;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Tests.Mocks
{
    /// <summary>
    /// In-process HTTP mock server for Linear GraphQL tests, built on WireMock.Net.
    /// Mirrors <see cref="MockJiraServer"/> in style: tests construct a
    /// <see cref="LinearGraphQL.LinearConnection"/> against <see cref="GraphQlUrl"/>
    /// and stub the single GraphQL endpoint via the four <c>Stub*</c> helpers.
    /// </summary>
    /// <remarks>
    /// Stubs use body-substring matching so multiple stubs can co-exist on the same
    /// <c>POST /graphql</c> endpoint. WireMock evaluates registered mappings in the
    /// reverse order they were added, so register the most-specific stub last when
    /// stubs share request body substrings.
    /// </remarks>
    internal sealed class MockLinearServer : IDisposable
    {
        private readonly WireMockServer _server;

        public MockLinearServer()
        {
            _server = WireMockServer.Start();
        }

        /// <summary>Base URL of the running mock server.</summary>
        public string Url => _server.Url ?? throw new InvalidOperationException("WireMock server has no URL");

        /// <summary>Full GraphQL endpoint URL — pass to <see cref="LinearGraphQL.LinearConnection"/>.</summary>
        public string GraphQlUrl => $"{Url}/graphql";

        public IEnumerable<ILogEntry> LogEntries => _server.LogEntries;

        /// <summary>
        /// Stubs <c>POST /graphql</c> with a JSON response, no body matching. Useful for
        /// tests that issue a single query and don't need to disambiguate between stubs.
        /// Kept for backwards compatibility with the Phase 12 MVP test suite.
        /// </summary>
        public void StubAssignedIssuesQuery(string jsonResponse)
        {
            _server
                .Given(Request.Create()
                    .WithPath("/graphql")
                    .UsingPost())
                .RespondWith(Response.Create()
                    .WithStatusCode(HttpStatusCode.OK)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(jsonResponse));
        }

        /// <summary>
        /// Hop 1 of the AI-prompt path: matches a request body containing
        /// <c>issueFilterSuggestion</c> and the supplied prompt substring; returns
        /// <c>{ "data": { "issueFilterSuggestion": { "filter": &lt;returnedFilter&gt; } } }</c>.
        /// </summary>
        public void StubFilterSuggestionQuery(string promptSubstring, JObject returnedFilter)
        {
            var body = new JObject
            {
                ["data"] = new JObject
                {
                    ["issueFilterSuggestion"] = new JObject
                    {
                        ["filter"] = returnedFilter
                    }
                }
            };

            _server
                .Given(Request.Create()
                    .WithPath("/graphql")
                    .UsingPost()
                    .WithBody(new RegexMatcher("issueFilterSuggestion"))
                    .WithBody(new RegexMatcher(System.Text.RegularExpressions.Regex.Escape(promptSubstring))))
                .RespondWith(Response.Create()
                    .WithStatusCode(HttpStatusCode.OK)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(body.ToString()));
        }

        /// <summary>
        /// Matches a top-level <c>issues(filter:</c> request body (NOT a customView one,
        /// which embeds <c>issues</c> inside a different selection). The regex requires
        /// the bare token <c>issues(</c> so it doesn't accidentally match
        /// <c>customView { issues</c>.
        /// </summary>
        public void StubIssuesQuery(string jsonResponse)
        {
            _server
                .Given(Request.Create()
                    .WithPath("/graphql")
                    .UsingPost()
                    .WithBody(new RegexMatcher(@"issues\("))
                    .WithBody(new RegexMatcher(@"\$filter")))
                .RespondWith(Response.Create()
                    .WithStatusCode(HttpStatusCode.OK)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(jsonResponse));
        }

        /// <summary>
        /// Matches a <c>customView</c> query whose variables include the supplied
        /// view ID; returns the supplied response.
        /// </summary>
        public void StubCustomViewQuery(string viewId, string jsonResponse)
        {
            _server
                .Given(Request.Create()
                    .WithPath("/graphql")
                    .UsingPost()
                    .WithBody(new RegexMatcher("customView"))
                    .WithBody(new RegexMatcher(System.Text.RegularExpressions.Regex.Escape(viewId))))
                .RespondWith(Response.Create()
                    .WithStatusCode(HttpStatusCode.OK)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(jsonResponse));
        }

        /// <summary>
        /// Matches any request body containing a substring (e.g. a mutation name or a
        /// marker like <c>"PRE-7"</c>) and returns the supplied JSON response. Used by
        /// LinearMutationExecutor tests to assert that the right mutation was POSTed.
        /// </summary>
        public void StubMutation(string bodySubstring, string jsonResponse)
        {
            _server
                .Given(Request.Create()
                    .WithPath("/graphql")
                    .UsingPost()
                    .WithBody(new RegexMatcher(System.Text.RegularExpressions.Regex.Escape(bodySubstring))))
                .RespondWith(Response.Create()
                    .WithStatusCode(HttpStatusCode.OK)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(jsonResponse));
        }

        public void Dispose()
        {
            _server.Stop();
            _server.Dispose();
        }
    }
}
