using System;
using System.Text.RegularExpressions;
using Preesta.Data;

namespace Preesta.Configuration
{
    internal static class RulesExtensions
    {
        public static bool IsMatch(this BuildRule buildRule, Build build)
        {
            Func<Build, int> getRemainingDays = b => (b.ReleaseDate.HasValue ? b.ReleaseDate.Value - DateTime.Now : TimeSpan.MaxValue).Days;
            Func<Build, bool> isExpired = b => getRemainingDays(b) < 0;
            Func<Build, bool> isFitToRemainingDays = b => (getRemainingDays(b) - buildRule.RemainingDays <= 0);
            return !build.Archived
                   && !build.Released
                   && (isExpired(build) && buildRule.ExpiredOnly
                      || !isExpired(build) && !buildRule.ExpiredOnly && isFitToRemainingDays(build))
                   && new Regex(buildRule.Mask).IsMatch(build.Name);
        }
    }
}
