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
        // Linear workspace slug (e.g. "preesta-dev"), used by IssueFormatter to build
        // a "View: My Sprint Blockers" → linear.app/{workspace}/view/{viewId} link
        // for viewId-mode rules. Null when this converter wraps a Jira pipeline or
        // when Linear:workspace isn't configured.
        private readonly string? _linearWorkspace;

        public IssuePackageConverter(string rootUri, string subjectPrefix = "[Jira] Unprocessed Issues ", string? linearWorkspace = null)
            : base(subjectPrefix)
        {
            _rootUri = string.IsNullOrEmpty(rootUri) || rootUri.EndsWith("/") ? rootUri : rootUri + "/";
            _linearWorkspace = linearWorkspace;
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
                    Body = ReplaceKnownMarkers(package.Reaction.BodyPattern, issue, _rootUri) ?? string.Empty,
                    Uri = new Uri(ReplaceKnownMarkers(package.Reaction.UrlPattern, issue, _rootUri) ?? string.Empty)
                }
            ).ToArray();
        }

        public override string[] ToGraphQLMutationBodies(IEnumerable<Package<GraphQLMutation, Issue>> packages)
        {
            return
            (
                from package in packages
                from issue in package.Items
                select ReplaceKnownMarkers(package.Reaction.MutationBody, issue, _rootUri) ?? string.Empty
            ).ToArray();
        }

        internal static string? ReplaceKnownMarkers(string? template, Issue issue, string rootUri)
        {
            return template == null ? null
            : template
                .Replace("{{@jiraRoot}}", rootUri)
                .Replace("{{@issueKey}}", issue.Key)
                .Replace("{{@issueId}}", issue.LinearId ?? string.Empty)
                .Replace("{{@title}}", issue.Summary)
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
                .EvaluateScriptingInjections(new ScriptingContext { issue = issue, rootUri = rootUri })
                ;
        }

        protected internal override string FormatHtml(IEnumerable<Package<NotificationReaction, Issue>> packages)
        {
            return IssueFormatter.ToHtml(packages, _rootUri, _linearWorkspace);
        }

        protected internal override string FormatText(IEnumerable<Package<NotificationReaction, Issue>> packages)
        {
            return IssueFormatter.ToText(packages, _rootUri, _linearWorkspace);
        }

        protected internal override string FormatMrkdwn(IEnumerable<Package<NotificationReaction, Issue>> packages)
        {
            return IssueFormatter.ToSlackMrkdwn(packages, _rootUri, _linearWorkspace);
        }
    }
}
