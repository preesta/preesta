using static System.String;

namespace Preesta.Data.Supplying
{
    internal class SendsNotification
    {
        public Addressees Addressees { get; set; } = new Addressees();
        public string Subject { get; set; } = Empty;
        public string? Recommendations { get; set; }
    }
}
