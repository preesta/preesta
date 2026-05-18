using System;
using System.Linq;
using JiraRest;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using PlaneRest;
using Preesta;
using Serilog;

namespace Tests.Plane
{
    /// <summary>
    /// PlaneMutationExecutor receives already-marker-substituted REST requests
    /// and forwards them to the Plane gateway one by one. Per-mutation failures
    /// (HTTP error, network) must be logged and swallowed — one bad mutation
    /// never stops the others (same contract as the Linear / GitHub / Jira
    /// executors).
    /// </summary>
    [TestFixture]
    public class PlaneMutationExecutorTests
    {
        [Test]
        public void HandleAll_HappyPath_ForwardsEachRequest()
        {
            var gateway = Substitute.For<IPlaneGateway>();
            gateway.Send(Arg.Any<string>(), Arg.Any<Uri>(), Arg.Any<string?>())
                .Returns("{ \"id\": \"wi-1\" }");

            var executor = new PlaneMutationExecutor(gateway, Substitute.For<ILogger>());

            executor.HandleAll(new[]
            {
                new HttpRequest { Verb = "PATCH", Uri = new Uri("https://api.plane.so/x/1"), Body = "{}" },
                new HttpRequest { Verb = "POST",  Uri = new Uri("https://api.plane.so/x/2/comments"), Body = "{\"comment_html\":\"hi\"}" }
            });

            Assert.AreEqual(2, gateway.ReceivedCalls()
                .Count(c => c.GetMethodInfo().Name == "Send"));
        }

        [Test]
        public void HandleAll_HttpError_LogsErrorAndContinues()
        {
            var gateway = Substitute.For<IPlaneGateway>();
            gateway.Send(Arg.Is<string>(_ => true),
                         Arg.Is<Uri>(u => u.AbsolutePath.EndsWith("/first")),
                         Arg.Any<string?>())
                .Throws(new InvalidOperationException("HTTP 500"));
            gateway.Send(Arg.Is<string>(_ => true),
                         Arg.Is<Uri>(u => u.AbsolutePath.EndsWith("/second")),
                         Arg.Any<string?>())
                .Returns("{ \"ok\": true }");

            var logger = Substitute.For<ILogger>();
            var executor = new PlaneMutationExecutor(gateway, logger);

            Assert.DoesNotThrow(() => executor.HandleAll(new[]
            {
                new HttpRequest { Verb = "PATCH", Uri = new Uri("https://api.plane.so/first"), Body = "{}" },
                new HttpRequest { Verb = "PATCH", Uri = new Uri("https://api.plane.so/second"), Body = "{}" }
            }));

            // Second request was still attempted.
            Assert.AreEqual(2, gateway.ReceivedCalls()
                .Count(c => c.GetMethodInfo().Name == "Send"));
            Assert.IsTrue(logger.ReceivedCalls()
                .Any(c => c.GetMethodInfo().Name == "Error"));
        }

        [Test]
        public void HandleAll_EmptyInput_NoCalls()
        {
            var gateway = Substitute.For<IPlaneGateway>();
            var executor = new PlaneMutationExecutor(gateway, Substitute.For<ILogger>());

            executor.HandleAll(Array.Empty<HttpRequest>());

            Assert.AreEqual(0, gateway.ReceivedCalls()
                .Count(c => c.GetMethodInfo().Name == "Send"));
        }

        [Test]
        public void HandleAll_VerbAndBodyForwardedVerbatim()
        {
            var gateway = Substitute.For<IPlaneGateway>();
            gateway.Send(Arg.Any<string>(), Arg.Any<Uri>(), Arg.Any<string?>()).Returns("{}");
            var executor = new PlaneMutationExecutor(gateway, Substitute.For<ILogger>());

            executor.HandleAll(new[]
            {
                new HttpRequest { Verb = "PATCH",
                                  Uri = new Uri("https://api.plane.so/x"),
                                  Body = "{\"state\":\"state-uuid\"}" }
            });

            gateway.Received(1).Send("PATCH",
                Arg.Is<Uri>(u => u.ToString() == "https://api.plane.so/x"),
                "{\"state\":\"state-uuid\"}");
        }
    }
}
