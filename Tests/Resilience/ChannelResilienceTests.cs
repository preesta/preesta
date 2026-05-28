using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Security;
using Messaging;
using NSubstitute;
using NUnit.Framework;
using Preesta;
using Preesta.Data;
using Preesta.Data.Supplying;
using Preesta.Data.Supplying.Convert;
using Preesta.Notification;
using Preesta.Notification.Delivery;
using Preesta.Notification.Mutation;
using Serilog;

namespace Tests.Resilience
{
    /// <summary>
    /// Per-message and per-channel isolation: one failing recipient or one
    /// crashed delivery target must not abort the rest of the run. The same
    /// log-and-continue contract holds across SMTP, Telegram, Slack, and the
    /// pipeline-level channel loop.
    /// </summary>
    [TestFixture]
    public class ChannelResilienceTests
    {
        [Test]
        public void SmtpClient_OneRecipientThrows_RestStillSent()
        {
            // SmtpClient.Send (the static loop driver) used to abort on the first
            // non-transient exception or retry-exhausted batch — one bounced
            // address poisoned every later recipient in the digest.
            var messenger = Substitute.For<IMessenger>();
            messenger
                .When(m => m.Send(Arg.Is<Message>(x => x.To == "b@example.com")))
                .Do(_ => throw new AuthenticationException("simulated auth failure"));

            var logger = Substitute.For<ILogger>();

            var batch = new[]
            {
                new Message { Subject = "m1", To = "a@example.com" },
                new Message { Subject = "m2", To = "b@example.com" },
                new Message { Subject = "m3", To = "c@example.com" },
            };

            SmtpClient.Send(messenger, batch, TimeSpan.Zero, retryCount: 0, logger);

            messenger.Received(1).Send(Arg.Is<Message>(s => s.To == "a@example.com"));
            messenger.Received(1).Send(Arg.Is<Message>(s => s.To == "b@example.com"));
            messenger.Received(1).Send(Arg.Is<Message>(s => s.To == "c@example.com"));
            // Serilog's many ILogger.Error overloads (params object[] vs T0/T1/T2)
            // make Arg-matched verification brittle; the count of Error calls is
            // a stable signal — one per failed recipient.
            Assert.IsTrue(
                logger.ReceivedCalls().Any(c => c.GetMethodInfo().Name == "Error"),
                "Expected ILogger.Error when SMTP send fails on a recipient");
        }

        [Test]
        public void TelegramMessenger_TransportErrorOnOneRecipient_RestStillSent()
        {
            // TelegramMessenger.Send catches transport errors internally —
            // verifying that contract: a thrown HttpRequestException for one chat
            // does not stop the SendAll loop from posting the rest.
            var seen = new List<string>();
            var handler = new StubHandler(req =>
            {
                var body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                seen.Add(body);
                if (body.Contains("\"chat_id\":\"fail\""))
                    throw new HttpRequestException("simulated transport fail");
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"ok\":true}")
                };
            });
            using var http = new HttpClient(handler);
            var messenger = new TelegramMessenger("token", http, Substitute.For<ILogger>());

            messenger.SendAll(new[]
            {
                new Message { To = "ok1", Body = "<p>1</p>", IsBodyHtml = true },
                new Message { To = "fail", Body = "<p>2</p>", IsBodyHtml = true },
                new Message { To = "ok2", Body = "<p>3</p>", IsBodyHtml = true },
            });

            Assert.AreEqual(3, seen.Count, "every recipient should have been attempted");
            Assert.IsTrue(seen[0].Contains("\"chat_id\":\"ok1\""));
            Assert.IsTrue(seen[1].Contains("\"chat_id\":\"fail\""));
            Assert.IsTrue(seen[2].Contains("\"chat_id\":\"ok2\""));
        }

        [Test]
        public void SlackMessenger_TransportErrorOnOneRecipient_RestStillSent()
        {
            // Same contract for Slack: one user with a broken DM channel must not
            // suppress the digest for everyone after them in the batch.
            var seen = new List<string>();
            var handler = new StubHandler(req =>
            {
                var body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                seen.Add(body);
                if (body.Contains("\"channel\":\"UFAIL\""))
                    throw new HttpRequestException("simulated transport fail");
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"ok\":true}")
                };
            });
            using var http = new HttpClient(handler);
            var messenger = new SlackMessenger("token", "https://slack.test/api/chat.postMessage",
                http, Substitute.For<ILogger>());

            messenger.SendAll(new[]
            {
                new Message { To = "UOK1", Body = "1" },
                new Message { To = "UFAIL", Body = "2" },
                new Message { To = "UOK2", Body = "3" },
            });

            Assert.AreEqual(3, seen.Count, "every recipient should have been attempted");
            Assert.IsTrue(seen[0].Contains("\"channel\":\"UOK1\""));
            Assert.IsTrue(seen[1].Contains("\"channel\":\"UFAIL\""));
            Assert.IsTrue(seen[2].Contains("\"channel\":\"UOK2\""));
        }

        [Test]
        public void ReactionPipeline_OneChannelThrows_OtherChannelsAndMutationStillRun()
        {
            // ReactionPipeline.TryRunStage isolates each delivery channel and the
            // mutation step. A bad-credentials EmailChannel must not block the
            // Slack channel that comes after it, and the mutation step must still
            // fire — silently swallowing a write-side update because the email
            // stage exploded would be a worse bug than the email failure itself.
            var badChannel = Substitute.For<IDeliveryChannel>();
            badChannel.Name.Returns("bad");
            badChannel
                .When(c => c.Deliver(
                    Arg.Any<IEnumerable<Package<NotificationReaction, Issue>>>(),
                    Arg.Any<IPackageConverter<Issue>>()))
                .Do(_ => throw new InvalidOperationException("simulated channel crash"));

            var goodChannel = Substitute.For<IDeliveryChannel>();
            goodChannel.Name.Returns("good");

            var mutation = Substitute.For<IMutationHandler>();

            var supplier = Substitute.For<IPackageSupplier>();
            var package = new Package<NotificationReaction, Issue>
            {
                Items = new[] { new Issue { Key = "X-1" } },
                Reaction = new NotificationReaction
                {
                    Subject = "s",
                    Followup = "f",
                    Addressees = new Addressees()
                }
            };
            supplier.GetPackages().Returns(new PackageBase[] { package });

            var converter = Substitute.For<IPackageConverter<Issue>>();

            var pipe = new ReactionPipeline<Issue>
            {
                PackageSupplier = supplier,
                PackageConverter = converter,
                Channels = new DeliveryChannels(new IDeliveryChannel[] { badChannel, goodChannel }),
                Mutations = mutation,
                Logger = Substitute.For<ILogger>()
            };

            pipe.Run();

            badChannel.Received(1).Deliver(
                Arg.Any<IEnumerable<Package<NotificationReaction, Issue>>>(),
                Arg.Any<IPackageConverter<Issue>>());
            goodChannel.Received(1).Deliver(
                Arg.Any<IEnumerable<Package<NotificationReaction, Issue>>>(),
                Arg.Any<IPackageConverter<Issue>>());
            mutation.Received(1).Execute(
                Arg.Any<IEnumerable<PackageBase>>(),
                Arg.Any<IPackageConverter<Issue>>());
        }

        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _fn;
            public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> fn) { _fn = fn; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
                => Task.FromResult(_fn(req));
        }
    }
}
