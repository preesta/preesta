using static System.String;

namespace Preesta.Configuration
{
    public class ReleaseRule : Rule
    {
        public int RemainingDays { get; set; }
        /// <summary>
        /// Contains regex pattern.
        /// </summary>
        public string Mask { get; set; } = Empty;
        public bool ExpiredOnly { get; set; }
        public string ProjectCode { get; set; } = Empty;
    }
}
