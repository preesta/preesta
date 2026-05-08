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
    /// In-process HTTP mock server for Slack <c>chat.postMessage</c> tests, built on
    /// WireMock.Net. Mirrors <see cref="MockLinearServer"/>: tests construct a
    /// <see cref="Messaging.SlackMessenger"/> with the test endpoint
    /// (<see cref="PostMessageUrl"/>) and stub the single endpoint via the
    /// <c>Stub*</c> helpers.
    /// </summary>
    internal sealed class MockSlackServer : IDisposable
    {
        private readonly WireMockServer _server;

        public MockSlackServer()
        {
            _server = WireMockServer.Start();
        }

        /// <summary>Base URL of the running mock server.</summary>
        public string Url => _server.Url ?? throw new InvalidOperationException("WireMock server has no URL");

        /// <summary>Full <c>chat.postMessage</c> URL — pass to <see cref="Messaging.SlackMessenger"/>.</summary>
        public string PostMessageUrl => $"{Url}/api/chat.postMessage";

        public IEnumerable<ILogEntry> LogEntries => _server.LogEntries;

        /// <summary>
        /// Stubs <c>POST /api/chat.postMessage</c> with a 200 OK and a Slack-style
        /// <c>{"ok": true}</c> body — the happy path.
        /// </summary>
        public void StubPostMessageOk()
        {
            _server
                .Given(Request.Create()
                    .WithPath("/api/chat.postMessage")
                    .UsingPost())
                .RespondWith(Response.Create()
                    .WithStatusCode(HttpStatusCode.OK)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(@"{""ok"":true}"));
        }

        /// <summary>
        /// Stubs <c>POST /api/chat.postMessage</c> with a 200 OK and a Slack-style
        /// <c>{"ok": false, "error": "..."}</c> body — the application-level failure
        /// path that <see cref="Messaging.SlackMessenger"/> must log without throwing.
        /// </summary>
        public void StubPostMessageError(string errorCode)
        {
            _server
                .Given(Request.Create()
                    .WithPath("/api/chat.postMessage")
                    .UsingPost())
                .RespondWith(Response.Create()
                    .WithStatusCode(HttpStatusCode.OK)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody($@"{{""ok"":false,""error"":""{errorCode}""}}"));
        }

        /// <summary>
        /// Stubs <c>POST /api/chat.postMessage</c> with an HTTP-level error (5xx).
        /// </summary>
        public void StubPostMessageHttpError(int statusCode = 500)
        {
            _server
                .Given(Request.Create()
                    .WithPath("/api/chat.postMessage")
                    .UsingPost())
                .RespondWith(Response.Create()
                    .WithStatusCode(statusCode)
                    .WithBody("internal error"));
        }

        public void Dispose()
        {
            _server.Stop();
            _server.Dispose();
        }
    }
}
