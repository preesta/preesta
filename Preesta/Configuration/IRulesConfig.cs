using System.Collections.Generic;

namespace Preesta.Configuration
{
    public interface IRulesConfig
    {
        JqlRule[] GetJqlRules(string @group);
        ReleaseRule[] GetReleaseRules(string @group);
        IReadOnlyDictionary<string, string> GetRedirectionMap();
        IReadOnlyDictionary<string, string> GetTelegramUserMap();
        void ValidateSchema();
    }
}
