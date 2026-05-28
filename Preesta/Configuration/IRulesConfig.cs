using System.Collections.Generic;
using Preesta.Configuration.Action;

namespace Preesta.Configuration
{
    public interface IRulesConfig
    {
        // Each Get*Rules method takes the CLI tag filter. Empty list = no tag
        // filter — every rule of that type is returned (including ones with no
        // tags). Non-empty list = lefthook-style positive selector: a rule
        // matches if intersect(rule.Tags, tags) is non-empty; untagged rules
        // are skipped.
        JqlRule[] GetJqlRules(IReadOnlyList<string> tags);
        ReleaseRule[] GetReleaseRules(IReadOnlyList<string> tags);
        LinearRule[] GetLinearRules(IReadOnlyList<string> tags);
        GithubRule[] GetGithubRules(IReadOnlyList<string> tags);
        GitlabRule[] GetGitlabRules(IReadOnlyList<string> tags);
        ShortcutRule[] GetShortcutRules(IReadOnlyList<string> tags);
        IReadOnlyDictionary<string, string> GetAliasMap();
        IReadOnlyDictionary<string, string> GetTelegramUserMap();
        IReadOnlyDictionary<string, string> GetSlackUserMap();
        void ValidateSchema();
    }
}
