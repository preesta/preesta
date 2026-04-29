using System.Collections.Generic;

namespace Preesta.Configuration
{
    public interface IRulesConfig
    {
        JqlRule[] GetJqlRules(string @group);
        BuildRule[] GetBuildRules(string @group);
        IssueInclusionToStructRule[] GetInStructRules(string @group);
        IReadOnlyDictionary<string, string> GetRedirectionMap();
        IReadOnlyDictionary<string, string> GetTelegramUserMap();
        void ValidateSchema();
    }
}
