using System;
using System.Diagnostics;
using System.Threading;
using MailKit.Net.Smtp;
using Messaging;
using NSubstitute;
using NUnit.Framework;
using SmtpClient = Messaging.SmtpClient;

namespace Tests.MailSending
{
    [TestFixture]
    public class MailSendingTests
    {
        private Message[] Messages { get; }

        public MailSendingTests()
        {
            var m1 = new Message { Subject = "m1" };
            var m2 = new Message { Subject = "m2" };
            var m3 = new Message { Subject = "m3" };

            Messages = new[] { m1, m2, m3 };

        }

        [Test]
        public void TestEachMessageIsSent()
        {
            var messenger = Substitute.For<IMessenger>();

            SmtpClient.Send(messenger, Messages, TimeSpan.FromSeconds(3), 2);

            messenger.Received(1).Send(Arg.Is<Message>(s => s.Subject == "m1"));
            messenger.Received(1).Send(Arg.Is<Message>(s => s.Subject == "m2"));
            messenger.Received(1).Send(Arg.Is<Message>(s => s.Subject == "m3"));
        }

        [Test]
        public void TestRetryWorks()
        {
            var count = 0;
            var messenger = Substitute.For<IMessenger>();
            messenger
                .When(a => a.Send(Arg.Any<Message>()))
                .Do(_ =>
                {
                    Debug.WriteLine(Thread.CurrentThread.ManagedThreadId);
                    count++;
                    if (count == 2)
                        throw new SmtpCommandException(SmtpErrorCode.MessageNotAccepted, SmtpStatusCode.TransactionFailed, "test");
                });

            SmtpClient.Send(messenger, Messages, TimeSpan.FromSeconds(1), 3);

            messenger.Received(4).Send(Arg.Any<Message>());

            messenger.Received().Send(Arg.Is<Message>(s => s.Subject == "m1"));
            messenger.Received().Send(Arg.Is<Message>(s => s.Subject == "m2"));
            messenger.Received().Send(Arg.Is<Message>(s => s.Subject == "m3"));
        }

    }
}