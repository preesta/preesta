using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Preesta.Data;
using Preesta.Data.Supplying;
using Scriban;
using Scriban.Runtime;

namespace Preesta.Formatting
{
    internal static class ReleaseFormatter
    {
        private static readonly Lazy<Template> HtmlTemplate =
            new(() => LoadTemplate("ReleaseDigest.scriban-html"));

        private static readonly Lazy<Template> TextTemplate =
            new(() => LoadTemplate("ReleaseDigest.scriban-text"));

        public static string ToHtml(IEnumerable<Package<NotificationReaction, Release>> packages) =>
            Render(HtmlTemplate.Value, BuildModel(packages));

        public static string ToText(IEnumerable<Package<NotificationReaction, Release>> packages) =>
            Render(TextTemplate.Value, BuildModel(packages));

        // Phase 10 (Slack): minimal Slack mrkdwn — bold version names, ⚠ for
        // expired releases. Lives inline (no Scriban template) — release digest
        // is shorter than the issue one and uses fewer chips.
        public static string ToSlackMrkdwn(IEnumerable<Package<NotificationReaction, Release>> packages)
        {
            var today = DateTime.Now.Date;
            var sb = new StringBuilder();
            var first = true;
            foreach (var package in packages)
            {
                if (!first) sb.AppendLine("———");
                first = false;

                if (!string.IsNullOrEmpty(package.Reaction.Followup))
                    sb.AppendLine(package.Reaction.Followup);

                sb.AppendLine();

                foreach (var build in package.Items)
                {
                    var expired = build.ReleaseDate != null && build.ReleaseDate.Value.Date < today;
                    var date = build.ReleaseDate?.ToString("dd.MM.yyyy") ?? "";
                    var prefix = expired ? ":warning: " : "";
                    // Slack mrkdwn: <url|label> links; emphasis inside the link
                    // body doesn't render, so we drop the bold when linking.
                    var named = string.IsNullOrEmpty(build.Url)
                        ? $"*{build.Name ?? ""}*"
                        : $"<{build.Url}|{build.Name ?? ""}>";
                    sb.Append(prefix).Append(named);
                    if (!string.IsNullOrEmpty(date))
                        sb.Append(" — ").Append(date);
                    sb.AppendLine();
                }
            }
            return sb.ToString();
        }

        private static ReleaseDigestModel BuildModel(IEnumerable<Package<NotificationReaction, Release>> packages)
        {
            var today = DateTime.Now.Date;
            var sections = packages.Select(package => new ReleaseDigestSection
            {
                Subject = package.Reaction.Subject,
                Followup = package.Reaction.Followup,
                Builds = package.Items.Select(b => new ReleaseRow
                {
                    Name = b.Name ?? "",
                    Url = b.Url,
                    ReleaseDate = b.ReleaseDate?.ToString("dd.MM.yyyy") ?? "",
                    Expired = b.ReleaseDate != null && b.ReleaseDate.Value.Date < today
                }).ToList()
            }).ToList();

            return new ReleaseDigestModel { Sections = sections };
        }

        private static Template LoadTemplate(string name)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Templates", name);
            return Template.Parse(File.ReadAllText(path));
        }

        private static string Render(Template template, object model)
        {
            var ctx = new TemplateContext();
            var script = new ScriptObject();
            script.Import(model, renamer: m => StandardMemberRenamer.Default(m));
            ctx.PushGlobal(script);
            return template.Render(ctx);
        }
    }
}
