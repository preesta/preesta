using static System.String;
namespace Preesta.Configuration.Action
{
    public class NotificationSpec
    {
        public string Subject { get; set; } = Empty;

        public string? Recommendations { get; set; }
        public string[] RawRecipients { get; set; } = new string[]{};
        public string[] RawCc { get; set; } = new string[]{};
        public string[] TelegramChatIds { get; set; } = new string[]{};
        public string[]? Columns { get; set; }
    }
}