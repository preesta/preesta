using System;
using System.Collections.Generic;
using System.Threading;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Utils;

namespace Messaging
{
    public class SmtpClient : IMessenger
    {
        private readonly SmtpConfig _config;

        public SmtpClient(SmtpConfig config)
        {
            _config = config;
        }

        public int SendRetriesCount { get; set; } = 20;

        public TimeSpan DelayAfterErrorInterval { get; set; } = TimeSpan.FromMinutes(1);

        public virtual void Send(Message data)
        {
            var mailMsg = new MimeMessage();
            mailMsg.From.Add(MailboxAddress.Parse(_config.From));
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
            client.Connect(_config.Host, _config.Port, _config.SecurityMode);

            // MailKit's Authenticate() is opt-in — local relays / MailHog need
            // no auth. SmtpConfigLoader rejects half-credentials at startup, so
            // by the time we're here, User/Password are both null or both set.
            // Password is non-null whenever User is — they're a validated pair.
            if (_config.User is { Length: > 0 })
                client.Authenticate(_config.User, _config.Password!);

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
                catch (Exception e) when (IsTransient(e))
                {
                    if (i >= retryCount)
                    {
                        throw;
                    }
                    Thread.Sleep(delayInterval);
                }
            }
        }

        /// <summary>
        /// Auth failures (bad credentials, expired tokens, MFA-required) bypass retry —
        /// twenty minutes of <c>Thread.Sleep</c> doesn't fix a wrong password. SMTP
        /// command/protocol errors still retry: some 5xx responses are transient
        /// (greylisting, temporary rate limits) and a stale connection often clears
        /// after a brief pause.
        /// </summary>
        private static bool IsTransient(Exception e)
        {
            if (e is AuthenticationException) return false;
            return e is SmtpCommandException || e is SmtpProtocolException;
        }
    }
}
