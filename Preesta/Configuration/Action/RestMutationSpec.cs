using static System.String;

namespace Preesta.Configuration.Action
{
    public class RestMutationSpec
    {
        public string Verb { get; set; } = Empty;
        public string UrlPattern { get; set; } = Empty;
        public string? BodyPattern { get; set; }
    }
}