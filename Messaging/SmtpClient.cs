using System;
using System.Collections.Generic;
using System.Threading;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Utils;
using Microsoft.Extensions.Configuration;

namespace Messaging
{
    public class SmtpClient : IMessenger
    {
        private readonly IConfigurationSection _config;

        public SmtpClient(IConfigurationSection config)
        {
            _config = config;
        }

        public int SendRetriesCount { get; set; } = 20;

        public TimeSpan DelayAfterErrorInterval { get; set; } = TimeSpan.FromMinutes(1);

        public virtual void Send(Message data)
        {
            var mailMsg = new MimeMessage();
            mailMsg.From.Add(MailboxAddress.Parse(_config["From"]));
            mailMsg.Subject = data.Subject;

            mailMsg.To.AddRange(InternetAddressList.Parse(data.To.Trim(", ".ToCharArray())));

            if (Enum.TryParse<MessagePriority>(data.Importance, true, out var priority))
            {
                mailMsg.Priority = priority;
            }

            if (!string.IsNullOrWhiteSpace(data.Cc))
            {
                mailMsg.Cc.AddRange(InternetAddressList.Parse(data.Cc.Trim(", ".ToCharArray())));
            }

            if (!string.IsNullOrWhiteSpace(data.Bcc))
            {
                mailMsg.Bcc.AddRange(InternetAddressList.Parse(data.Bcc.Trim(", ".ToCharArray())));
            }

            var bodyBuilder = new BodyBuilder();

            if (!string.IsNullOrWhiteSpace(data.LogoFileName) && data.IsBodyHtml)
            {
                var logo = bodyBuilder.LinkedResources.Add(data.LogoFileName);
                logo.ContentId = MimeUtils.GenerateMessageId();
                bodyBuilder.HtmlBody = data.Body + $@"<img src=""cid:{logo.ContentId}"" />";
            }
            else if (data.IsBodyHtml)
            {
                bodyBuilder.HtmlBody = data.Body;
            }
            else
            {
                bodyBuilder.TextBody = data.Body;
            }

            mailMsg.Body = bodyBuilder.ToMessageBody();

            using var client = new MailKit.Net.Smtp.SmtpClient();
            var enableSsl = bool.Parse(_config["EnableSsl"]);
            client.Connect(_config["Host"], int.Parse(_config["Port"]),
                enableSsl ? SecureSocketOptions.Auto : SecureSocketOptions.None);
            client.Authenticate(_config["User"], _config["Password"]);
            client.Send(mailMsg);
            client.Disconnect(true);
        }

        public virtual void SendAll(IEnumerable<Message> messages)
        {
            Send(this, messages, DelayAfterErrorInterval, SendRetriesCount);
        }

        internal static void Send(IMessenger messenger, IEnumerable<Message> messages, TimeSpan delayInterval, int retryCount)
        {
            foreach (var message in messages)
            {
                SendMessageWithRetries(messenger, message, delayInterval, retryCount);
            }
        }

        private static void SendMessageWithRetries(IMessenger messenger, Message message, TimeSpan delayInterval, int retryCount)
        {
            for (var i = 0;; i++)
            {
                try
                {
                    messenger.Send(message);
                    return;
                }
                catch (Exception e) when (e is SmtpCommandException || e is SmtpProtocolException)
                {
                    if (i >= retryCount)
                    {
                        throw;
                    }
                    Thread.Sleep(delayInterval);
                }
            }
        }
    }
}
