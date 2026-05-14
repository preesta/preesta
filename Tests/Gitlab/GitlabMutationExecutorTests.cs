using System;
using System.Linq;
using GitlabGraphQL;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using Preesta;
using Serilog;

namespace Tests.Gitlab
{
    /// <summary>
    /// GitlabMutationExecutor receives already-marker-substituted mutation bodies
    /// and POSTs them one by one. Per-mutation failures (HTTP error, GraphQL
    /// <c>errors</c> envelope) must be logged and swallowed — one bad mutation
    /// never stops the others. Mirror of GithubMutationExecutorTests.
    /// </summary>
    [TestFixture]
    public class GitlabMutationExecutorTests
    {
        private const string SuccessResponse = @"{
  ""data"": { ""createNote"": { ""note"": { ""id"": ""gid://gitlab/Note/1"" } } }
}";

        private const string GraphQlErrorResponse = @"{
  ""errors"": [{ ""message"": ""Could not resolve to a node"" }],
  ""data"": null
}";

        [Test]
        public void HandleAll_HappyPath_PostsEachMutationBody()
        {
            var gateway = Substitute.For<IGitlabGateway>();
            gateway.Query(Arg.Any<string>(), Arg.Any<object?>())
                .Returns(JObject.Parse(SuccessResponse));

            var executor = new GitlabMutationExecutor(gateway, Substitute.For<ILogger>());

            executor.HandleAll(new[]
            {
                "mutation { createNote(input: { noteableId: \"gid://gitlab/Issue/1\", body: \"hi\" }) { note { id } } }",
                "mutation { updateIssue(input: { iid: \"1\", projectPath: \"foo/bar\", stateEvent: CLOSE }) { issue { state } } }"
            });

            Assert.AreEqual(2, gateway.ReceivedCalls()
                .Count(c => c.GetMethodInfo().Name == "Query"));
        }

        [Test]
        public void HandleAll_GraphQlErrorEnvelope_LogsErrorAndContinues()
        {
            var gateway = Substitute.For<IGitlabGateway>();
            gateway.Query(Arg.Is<string>(s => s.Contains("badMutation")), Arg.Any<object?>())
                .Returns(JObject.Parse(GraphQlErrorResponse));
            gateway.Query(Arg.Is<string>(s => s.Contains("goodMutation")), Arg.Any<object?>())
                .Returns(JObject.Parse(SuccessResponse));

            var logger = Substitute.For<ILogger>();
            var executor = new GitlabMutationExecutor(gateway, logger);

            executor.HandleAll(new[]
            {
                "mutation { badMutation(input: {}) { ok } }",
                "mutation { goodMutation(input: {}) { ok } }"
            });

            // Both calls were issued — bad mutation didn't abort the loop.
            Assert.AreEqual(2, gateway.ReceivedCalls()
                .Count(c => c.GetMethodInfo().Name == "Query"));

            var errorCalls = logger.ReceivedCalls()
                .Count(c => c.GetMethodInfo().Name == "Error");
            Assert.GreaterOrEqual(errorCalls, 1,
                "Expected at least one Error log for the GraphQL `errors` envelope");
        }

        [Test]
        public void HandleAll_HttpError_LogsErrorAndContinues()
        {
            var gateway = Substitute.For<IGitlabGateway>();
            gateway.Query(Arg.Is<string>(s => s.Contains("first")), Arg.Any<object?>())
                .Throws(new InvalidOperationException("HTTP 500"));
            gateway.Query(Arg.Is<string>(s => s.Contains("second")), Arg.Any<object?>())
                .Returns(JObject.Parse(SuccessResponse));

            var logger = Substitute.For<ILogger>();
            var executor = new GitlabMutationExecutor(gateway, logger);

            Assert.DoesNotThrow(() => executor.HandleAll(new[]
            {
                "mutation { first(input: {}) { ok } }",
                "mutation { second(input: {}) { ok } }"
            }));

            Assert.AreEqual(2, gateway.ReceivedCalls()
                .Count(c => c.GetMethodInfo().Name == "Query"));
            Assert.IsTrue(logger.ReceivedCalls()
                .Any(c => c.GetMethodInfo().Name == "Error"));
        }

        [Test]
        public void HandleAll_EmptyInput_NoRequestsIssued()
        {
            var gateway = Substitute.For<IGitlabGateway>();
            var executor = new GitlabMutationExecutor(gateway, Substitute.For<ILogger>());

            executor.HandleAll(Array.Empty<string>());

            Assert.AreEqual(0, gateway.ReceivedCalls()
                .Count(c => c.GetMethodInfo().Name == "Query"));
        }
    }
}
