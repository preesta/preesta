using MailKit.Security;

namespace Messaging
{
    /// <summary>
    /// Validated transport-layer SMTP parameters. The loader on the Preesta
    /// side (<c>SmtpConfigLoader.Load</c>) decides whether a section is
    /// present at all (returns <c>null</c> when absent) and enforces the
    /// "User+Password must be set together" rule; by the time a
    /// <see cref="SmtpConfig"/> exists, every required field is non-null
    /// and the optional pair is internally consistent.
    /// </summary>
    public sealed record SmtpConfig(
        string Host,
        string From,
        int Port = 0,
        SecureSocketOptions SecurityMode = SecureSocketOptions.Auto,
        string? User = null,
        string? Password = null);
}
