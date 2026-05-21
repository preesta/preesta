using System;
using MailKit.Security;
using Messaging;
using Microsoft.Extensions.Configuration;

namespace Preesta.AppConfig
{
    /// <summary>
    /// Adapter between Preesta's <see cref="IConfigurationSection"/>-based
    /// settings layer and the transport-layer <see cref="SmtpConfig"/> record.
    /// Returns <c>null</c> when the section is absent so the rest of the app
    /// can pattern-match <c>appSettings.Smtp is not null</c> to decide whether
    /// to wire up the SMTP channel.
    /// </summary>
    internal static class SmtpConfigLoader
    {
        public static SmtpConfig? Load(IConfigurationSection section)
        {
            if (!section.Exists()) return null;

            var host = section["Host"]
                ?? throw new InvalidOperationException("Smtp:Host is required.");
            var from = section["From"]
                ?? throw new InvalidOperationException("Smtp:From is required.");

            var port = ParsePort(section["Port"]);
            var mode = ParseSecurityMode(section["SecurityMode"]);

            var user = section["User"];
            var pass = section["Password"];
            var hasUser = !string.IsNullOrEmpty(user);
            var hasPass = !string.IsNullOrEmpty(pass);
            if (hasUser != hasPass)
                throw new InvalidOperationException(
                    "Smtp:User and Smtp:Password must be set together (or both omitted for unauthenticated relays).");

            return new SmtpConfig(host, from, port, mode,
                hasUser ? user : null,
                hasUser ? pass : null);
        }

        // Port defaults to 0 — MailKit interprets that as "auto-pick by
        // SecurityMode" (587 for STARTTLS, 465 for SSL-on-connect, 25 for None).
        private static int ParsePort(string? raw) =>
            string.IsNullOrEmpty(raw) ? 0 : int.Parse(raw);

        // SecurityMode default = Auto, same as MailKit's Connect(..., options=Auto).
        // Auto negotiates STARTTLS when the server advertises it, falls back to
        // plain otherwise. Explicit values: None, SslOnConnect, StartTls,
        // StartTlsWhenAvailable.
        private static SecureSocketOptions ParseSecurityMode(string? raw) =>
            string.IsNullOrEmpty(raw)
                ? SecureSocketOptions.Auto
                : Enum.Parse<SecureSocketOptions>(raw, ignoreCase: true);
    }
}
