# Tests/Mocks

In-process HTTP mocks used by the test suite.

## Why WireMock.Net

`MockJiraServer` wraps [WireMock.Net](https://github.com/wiremock/WireMock.Net), a real HTTP listener that runs in-process on a free port. Tests construct a `JiraRest.Connection` (or `CloudConnection`) against `server.Url` and the regular `HttpClient` actually opens sockets, so URL building, query strings and request bodies are validated end-to-end. This replaces the previous `DelegatingHandler` stub, which intercepted requests before they were ever serialized.

(We use the `WireMock.Net.Minimal` package rather than the full `WireMock.Net` because the latter pulls in `Microsoft.CodeAnalysis.Common` 4.8.0, which conflicts with the 3.8.0 already used by `Preesta` via `Microsoft.CodeAnalysis.CSharp.Scripting`.)

## Adding a stub

```csharp
using var server = new MockJiraServer();
server.StubGetIssue("BENDER-123", @"{ ""key"": ""BENDER-123"" }");

var connection = new JiraRest.Connection(server.Url, "user", "token");
var issue = connection.GetIssue("BENDER-123");
```

Helpers cover the common Jira endpoints: `StubGetIssuesByJql`, `StubCloudSearchJql`, `StubGetIssue`, `StubPostIssue`, `StubPutIssue`, `StubReleases`, plus a generic `StubAnyWrite` for self-update PUTs/POSTs to arbitrary URLs.

## Inspecting recorded calls

```csharp
Assert.AreEqual(1, server.LogEntries.Count(e =>
    e.RequestMessage.Method == "POST"
    && e.RequestMessage.AbsoluteUrl == $"{server.Url}/rest/api/2/issue/BENDER-123/transitions"
    && (e.RequestMessage.Body ?? "").Contains("\"transition\"")));
```

`server.CountRequests(method, absoluteUrl)` is a convenience for the common case. Disposing the server (via `using`) stops the HTTP listener.
