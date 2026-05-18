using System;
using System.Collections.Generic;
using Preesta.Configuration.Action;
using Serilog;

namespace Preesta.Data.Supplying
{
    /// <summary>
    /// Mirrors <see cref="GithubIssueSupplier"/> but pulls issues from
    /// <see cref="ShortcutIssueSource"/>. The base <see cref="IssueSupplier{TRule}.JiraService"/>
    /// is still required by the inherited grouping logic; it's only consulted for
    /// <c>AdditionalPredicateName</c> resolution, which Shortcut rules don't currently use.
    /// </summary>
    /// <remarks>
    /// Shortcut mutations are raw REST (verb + url + body) — exactly the shape the
    /// inherited <see cref="IssueSupplier{TRule}.GetMutationPackages"/> already
    /// produces from <see cref="Configuration.Rule.Mutations"/>, so no override is
    /// needed (unlike Linear/GitHub, which switch to GraphQL packages).
    /// </remarks>
    internal class ShortcutIssueSupplier : IssueSupplier<ShortcutRule>
    {
        private readonly ShortcutIssueSource _source;
        private readonly ILogger _logger;

        public ShortcutIssueSupplier(
            ShortcutIssueSource source,
            IJiraService jiraService,
            IEnumerable<ShortcutRule> rules,
            ILogger logger)
            : base(jiraService, rules)
        {
            _source = source;
            _logger = logger;
        }

        protected override Issue[] GetIssues(ShortcutRule rule)
        {
            try
            {
                return _source.GetIssues(rule);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to get issues from Shortcut for rule: {@rule}", rule);
                return Array.Empty<Issue>();
            }
        }

        protected internal override PackageBase Enrich(PackageBase basePackage, ShortcutRule rule)
        {
            if (!string.IsNullOrEmpty(rule.Filter))
            {
                basePackage.Properties["ShortcutFilter"] = rule.Filter!;
                // Round-trip link to the Shortcut stories page pre-filtered with the
                // same query string. Slug resolved lazily via /api/v3/member —
                // returns null when the call fails, in which case we just skip the
                // link rather than producing a broken one.
                var slug = _source.WorkspaceSlug;
                // Shortcut encodes the search query as a URL fragment on /search,
                // not as a query param on /stories (which redirects to a default
                // space and ignores ?query=). Verified live: the web UI generates
                // exactly this form when you type into the in-page Search Stories box.
                // Fragment-encoding is intentionally minimal: percent-encode only
                // whitespace + the fragment delimiter itself. Uri.EscapeDataString
                // would escape `:` and `!` too, which Shortcut then renders back to
                // the user as literal `%3A` / `%21` in the search box.
                if (!string.IsNullOrEmpty(slug))
                    basePackage.Properties["ShortcutSearchUri"] =
                        $"https://app.shortcut.com/{slug}/search#{rule.Filter!.Replace("#", "%23").Replace(" ", "%20")}";
            }
            return basePackage;
        }
    }
}
