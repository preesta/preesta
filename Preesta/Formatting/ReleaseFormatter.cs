using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        private static ReleaseDigestModel BuildModel(IEnumerable<Package<NotificationReaction, Release>> packages)
        {
            var today = DateTime.Now.Date;
            var sections = packages.Select(package => new ReleaseDigestSection
            {
                Subject = package.Reaction.Subject,
                Recommendations = package.Reaction.Recommendations,
                Builds = package.Items.Select(b => new ReleaseRow
                {
                    Name = b.Name ?? "",
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
