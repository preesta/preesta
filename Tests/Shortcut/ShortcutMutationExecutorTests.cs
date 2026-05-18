using System;
using System.Linq;
using JiraRest;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using Preesta;
using Serilog;
using ShortcutRest;

namespace Tests.Shortcut
{
    /// <summary>
    /// ShortcutMutationExecutor receives already-marker-substituted HttpRequest
    /// objects (verb + absolute URI + body) and routes them through the gateway's
    /// REST <c>Send</c>. Per-request failures must be logged and swallowed — one
    /// bad mutation never stops the others.
    /// </summary>
    [TestFixture]
    public class ShortcutMutationExecutorTests
    {
        private static HttpRequest Req(string verb, string url, string body) => new()
        {
            Verb = verb,
            Uri = new Uri(url),
            Body = body
        };

        [Test]
        public void HandleAll_HappyPath_RoutesEachRequest()
        {
            var gateway = Substitute.For<IShortcutGateway>();
            var executor = new ShortcutMutationExecutor(gateway, Substitute.For<ILogger>());

            executor.HandleAll(new[]
            {
                Req("PUT", "https://api.app.shortcut.com/api/v3/stories/123",
                    "{\"description\":\"updated\"}"),
                Req("POST", "https://api.app.shortcut.com/api/v3/stories/123/comments",
                    "{\"text\":\"hi\"}")
            });

            Assert.AreEqual(2, gateway.ReceivedCalls()
                .Count(c => c.GetMethodInfo().Name == "Send"));
        }

        [Test]
        public void HandleAll_ExtractsPathAndQueryFromAbsoluteUri()
        {
            var gateway = Substitute.For<IShortcutGateway>();
            var executor = new ShortcutMutationExecutor(gateway, Substitute.For<ILogger>());

            executor.HandleAll(new[]
            {
                Req("DELETE", "https://api.app.shortcut.com/api/v3/stories/42?x=1", "")
            });

            gateway.Received(1).Send("DELETE", "/api/v3/stories/42?x=1", Arg.Any<string?>());
        }

        [Test]
        public void HandleAll_GatewayThrows_LogsErrorAndContinues()
        {
            var gateway = Substitute.For<IShortcutGateway>();
            // First call throws; second still runs.
            gateway.When(g => g.Send("PUT",
                    Arg.Is<string>(s => s.Contains("/stories/first")), Arg.Any<string?>()))
                .Do(_ => throw new InvalidOperationException("HTTP 500"));

            var logger = Substitute.For<ILogger>();
            var executor = new ShortcutMutationExecutor(gateway, logger);

            Assert.DoesNotThrow(() => executor.HandleAll(new[]
            {
                Req("PUT", "https://api.app.shortcut.com/api/v3/stories/first", "{}"),
                Req("PUT", "https://api.app.shortcut.com/api/v3/stories/second", "{}")
            }));

            Assert.AreEqual(2, gateway.ReceivedCalls()
                .Count(c => c.GetMethodInfo().Name == "Send"));
            Assert.IsTrue(logger.ReceivedCalls()
                .Any(c => c.GetMethodInfo().Name == "Error"));
        }

        [Test]
        public void HandleAll_EmptyInput_NoRequestsIssued()
        {
            var gateway = Substitute.For<IShortcutGateway>();
            var executor = new ShortcutMutationExecutor(gateway, Substitute.For<ILogger>());

            executor.HandleAll(Array.Empty<HttpRequest>());

            Assert.AreEqual(0, gateway.ReceivedCalls()
                .Count(c => c.GetMethodInfo().Name == "Send"));
        }
    }
}
