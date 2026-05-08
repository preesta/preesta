using System;
using System.Collections.Generic;
using System.Net;
using WireMock.Logging;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Tests.Mocks
{
    /// <summary>
    /// In-process HTTP mock server for Linear GraphQL tests, built on WireMock.Net.
    /// Mirrors <see cref="MockJiraServer"/> in style: tests construct a
    /// <see cref="LinearGraphQL.LinearConnection"/> against <see cref="Url"/> and
    /// stub the single GraphQL endpoint via <see cref="StubAssignedIssuesQuery"/>.
    /// </summary>
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
        /// Stub <c>POST /graphql</c> with a JSON response. Linear has a single endpoint;
        /// in the MVP we only emit the assigned-issues query so request-body matching
        /// isn't necessary.
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

        public void Dispose()
        {
            _server.Stop();
            _server.Dispose();
        }
    }
}
