using System.Collections.Generic;
using System.Text;
using Preesta.Data;
using Preesta.Data.Supplying;

namespace Preesta.Formatting
{
    internal static class BuildFormatter
    {
        public static string ToHtml(IEnumerable<Package<SendsNotification, Build>> packages)
        {
            var sb = new StringBuilder();
            foreach (var package in packages)
            {
                sb.Append($"<h3 style=\"font-family:sans-serif;color:#333\">{package.Reaction.Subject}</h3>\n");
                if (!string.IsNullOrEmpty(package.Reaction.Recommendations))
                    sb.Append($"<p style=\"font-family:sans-serif;color:#666;font-style:italic\">{package.Reaction.Recommendations}</p>\n");

                sb.Append("<table style=\"border-collapse:collapse;font-family:sans-serif;font-size:13px\">\n");
                sb.Append("<tr style=\"background:#f4f5f7;font-weight:bold\">\n");
                foreach (var h in new[] { "Name", "Start Date", "Release Date", "Description" })
                    sb.Append($"  <td style=\"padding:8px 6px;border:1px solid #dfe1e6\">{h}</td>\n");
                sb.Append("</tr>\n");

                foreach (var build in package.Items)
                {
                    sb.Append("<tr>\n");
                    sb.Append($"  <td style=\"padding:6px;border:1px solid #dfe1e6\">{build.Name ?? ""}</td>\n");
                    sb.Append($"  <td style=\"padding:6px;border:1px solid #dfe1e6\">{build.StartDate?.ToString("dd.MM.yyyy") ?? ""}</td>\n");
                    sb.Append($"  <td style=\"padding:6px;border:1px solid #dfe1e6;color:#de350b;font-weight:bold\">{build.ReleaseDate?.ToString("dd.MM.yyyy") ?? ""}</td>\n");
                    sb.Append($"  <td style=\"padding:6px;border:1px solid #dfe1e6\">{build.Description ?? ""}</td>\n");
                    sb.Append("</tr>\n");
                }
                sb.Append("</table>\n");
            }
            return sb.ToString();
        }

        public static string ToText(IEnumerable<Package<SendsNotification, Build>> packages)
        {
            var sb = new StringBuilder();
            foreach (var package in packages)
            {
                sb.AppendLine($"<b>{package.Reaction.Subject}</b>");
                if (!string.IsNullOrEmpty(package.Reaction.Recommendations))
                    sb.AppendLine($"<i>{package.Reaction.Recommendations}</i>");
                sb.AppendLine();

                foreach (var build in package.Items)
                {
                    sb.AppendLine($"<b>{build.Name}</b>");
                    if (build.StartDate != null)
                        sb.Append($"  Start: {build.StartDate:dd.MM.yyyy}");
                    if (build.ReleaseDate != null)
                        sb.Append($"  Release: {build.ReleaseDate:dd.MM.yyyy}");
                    sb.AppendLine();
                    if (!string.IsNullOrEmpty(build.Description))
                        sb.AppendLine($"  {build.Description}");
                    sb.AppendLine();
                }
            }
            return sb.ToString().TrimEnd();
        }
    }
}
