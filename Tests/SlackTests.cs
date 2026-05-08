using System.Linq;
using Messaging;
using NSubstitute;
using NUnit.Framework;
using Serilog;
using Tests.Mocks;

namespace Tests
{
    [TestFixture]
    public class SlackTests
    {
        // ----- SlackMessenger direct tests -----

        [Test]
        public void Send_PostsAuthBearerAndChannelAndText()
        {
            using var server = new MockSlackServer();
            server.StubPostMessageOk();

            var messenger = new SlackMessenger("xoxb-FAKE-TEST-TOKEN", server.PostMessageUrl);
            messenger.Send(new Message
            {
                To = "U123ABC",
                TextBody = "*hello* from Preesta"
            });

            var entry = server.LogEntries.Single();
            var headers = entry.RequestMessage.Headers!;
            Assert.IsTrue(headers.ContainsKey("Authorization"),
                "Authorization header missing");
            Assert.AreEqual("Bearer xoxb-FAKE-TEST-TOKEN", headers["Authorization"].ToString());

            var body = entry.RequestMessage.Body ?? "";
            Assert.IsTrue(body.Contains("\"channel\":\"U123ABC\""), $"channel missing in body: {body}");
            Assert.IsTrue(body.Contains("hello"), $"text missing in body: {body}");
            Assert.IsTrue(body.Contains("\"mrkdwn\":true"), $"mrkdwn flag missing in body: {body}");
        }

        [Test]
        public void Send_OkFalseResponse_LogsErrorDoesNotThrow()
        {
            using var server = new MockSlackServer();
            server.StubPostMessageError("users_not_found");

            var logger = Substitute.For<ILogger>();
            var messenger = new SlackMessenger("xoxb-FAKE-TEST-TOKEN", server.PostMessageUrl, logger: logger);

            Assert.DoesNotThrow(() => messenger.Send(new Message
            {
                To = "Uinvalid",
                TextBody = "anything"
            }));

            // We don't assert exact message text — count of Error calls is enough signal.
            var errorCalls = logger.ReceivedCalls()
                .Count(c => c.GetMethodInfo().Name == "Error");
            Assert.GreaterOrEqual(errorCalls, 1,
                "Expected ILogger.Error when chat.postMessage returns ok:false");
        }

        [Test]
        public void Send_HttpError_LogsErrorDoesNotThrow()
        {
            using var server = new MockSlackServer();
            server.StubPostMessageHttpError(500);

            var logger = Substitute.For<ILogger>();
            var messenger = new SlackMessenger("xoxb-FAKE-TEST-TOKEN", server.PostMessageUrl, logger: logger);

            Assert.DoesNotThrow(() => messenger.Send(new Message
            {
                To = "U123",
                TextBody = "anything"
            }));

            var errorCalls = logger.ReceivedCalls()
                .Count(c => c.GetMethodInfo().Name == "Error");
            Assert.GreaterOrEqual(errorCalls, 1,
                "Expected ILogger.Error on HTTP 5xx");
        }
    }
}
