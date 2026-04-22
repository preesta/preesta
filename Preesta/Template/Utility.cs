using System.Web;

namespace Preesta.Template
{
    public static class Utility
    {
        public static string EscapeHtml(string unescaped) => HttpUtility.HtmlEncode(unescaped);
    }
}