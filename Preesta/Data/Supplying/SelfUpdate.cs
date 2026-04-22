using static System.String;

namespace Preesta.Data.Supplying
{
    internal class SelfUpdate
    {
        public string Verb { get; set; } = Empty;
        public string UrlPattern { get; set; } = Empty;
        public string? BodyPattern { get; set; }

    }
}