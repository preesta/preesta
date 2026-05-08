using static System.String;

namespace Preesta.Data.Supplying
{
    internal class NotificationReaction
    {
        public Addressees Addressees { get; set; } = new Addressees();
        public string Subject { get; set; } = Empty;
        public string? Recommendations { get; set; }
        public string[] TelegramChatIds { get; set; } = new string[]{};
        public string[] SlackUserIds { get; set; } = new string[]{};
        public string[]? Columns { get; set; }
    }
}
