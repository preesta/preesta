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
    internal static class BuildFormatter
    {
        private static readonly Lazy<Template> HtmlTemplate =
            new(() => LoadTemplate("BuildDigest.scriban-html"));

        private static readonly Lazy<Template> TextTemplate =
            new(() => LoadTemplate("BuildDigest.scriban-text"));

        public static string ToHtml(IEnumerable<Package<SendsNotification, Build>> packages) =>
            Render(HtmlTemplate.Value, BuildModel(packages));

        public static string ToText(IEnumerable<Package<SendsNotification, Build>> packages) =>
            Render(TextTemplate.Value, BuildModel(packages));

        private static BuildDigestModel BuildModel(IEnumerable<Package<SendsNotification, Build>> packages)
        {
            var today = DateTime.Now.Date;
            var sections = packages.Select(package => new BuildDigestSection
            {
                Subject = package.Reaction.Subject,
                Recommendations = package.Reaction.Recommendations,
                Builds = package.Items.Select(b => new BuildRow
                {
                    Name = b.Name ?? "",
                    ReleaseDate = b.ReleaseDate?.ToString("dd.MM.yyyy") ?? "",
                    Expired = b.ReleaseDate != null && b.ReleaseDate.Value.Date < today
                }).ToList()
            }).ToList();

            return new BuildDigestModel { Sections = sections };
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
