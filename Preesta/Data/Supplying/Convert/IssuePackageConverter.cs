using System;
using System.Collections.Generic;
using System.Linq;
using Preesta.Formatting;
using JiraRest;
using Preesta.Extensions;

namespace Preesta.Data.Supplying.Convert
{
    internal class IssuePackageConverter : PackageConverterBase<Issue>
    {
        private readonly string _rootUri;

        public IssuePackageConverter(string rootUri, string subjectPrefix = "[Jira] Unprocessed Issues ")
            : base(subjectPrefix)
        {
            _rootUri = rootUri;
        }

        public override HttpRequest[] ToHttpRequests(IEnumerable<Package<SelfUpdate, Issue>> packages)
        {
            return
            (
                from package in packages
                from issue in package.Items
                select new HttpRequest
                {
                    Verb = package.Reaction.Verb,
                    Body = ReplaceKnownMarkers(package.Reaction.BodyPattern, issue) ?? string.Empty,
                    Uri = new Uri(ReplaceKnownMarkers(package.Reaction.UrlPattern, issue) ?? string.Empty)
                }
            ).ToArray();
        }

        private string? ReplaceKnownMarkers(string? template, Issue issue)
        {
            return template == null ? null
            : template
                .Replace("{{@jiraRoot}}", _rootUri)
                .Replace("{{@issueKey}}", issue.Key)
                .Replace("{{@assignee.email}}", issue.Participants.Assignee?.Email)
                .Replace("{{@assignee.key}}", issue.Participants.Assignee?.Key)
                .Replace("{{@assignee.name}}", issue.Participants.Assignee?.Name)
                .Replace("{{@assignee.displayName}}", issue.Participants.Assignee?.DisplayName)
                .Replace("{{@reporter.email}}", issue.Participants.Reporter?.Email)
                .Replace("{{@reporter.key}}", issue.Participants.Reporter?.Key)
                .Replace("{{@reporter.name}}", issue.Participants.Reporter?.Name)
                .Replace("{{@reporter.displayName}}", issue.Participants.Reporter?.DisplayName)
                .Replace("{{@creator.email}}", issue.Participants.Creator?.Email)
                .Replace("{{@creator.key}}", issue.Participants.Creator?.Key)
                .Replace("{{@creator.name}}", issue.Participants.Creator?.Name)
                .Replace("{{@creator.displayName}}", issue.Participants.Creator?.DisplayName)
                .EvaluateScriptingInjections(new ScriptingContext { issue = issue, rootUri = _rootUri })
                ;
        }

        protected internal override string FormatHtml(IEnumerable<Package<NotificationReaction, Issue>> packages)
        {
            return IssueFormatter.ToHtml(packages, _rootUri);
        }

        protected internal override string FormatText(IEnumerable<Package<NotificationReaction, Issue>> packages)
        {
            return IssueFormatter.ToText(packages, _rootUri);
        }
    }
}
