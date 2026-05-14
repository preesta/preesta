using System.Collections.Generic;
using Preesta.Configuration.Action;

namespace Preesta.Configuration
{
    public interface IRulesConfig
    {
        JqlRule[] GetJqlRules(string @group);
        ReleaseRule[] GetReleaseRules(string @group);
        LinearRule[] GetLinearRules(string @group);
        GithubRule[] GetGithubRules(string @group);
        IReadOnlyDictionary<string, string> GetRedirectionMap();
        IReadOnlyDictionary<string, string> GetTelegramUserMap();
        IReadOnlyDictionary<string, string> GetSlackUserMap();
        void ValidateSchema();
    }
}
