using System;
using System.Text.RegularExpressions;
using Preesta.Data;

namespace Preesta.Configuration
{
    internal static class RulesExtensions
    {
        public static bool IsMatch(this ReleaseRule buildRule, Release build)
        {
            Func<Release, int> getRemainingDays = b => (b.ReleaseDate.HasValue ? b.ReleaseDate.Value - DateTime.Now : TimeSpan.MaxValue).Days;
            Func<Release, bool> isExpired = b => getRemainingDays(b) < 0;
            Func<Release, bool> isFitToRemainingDays = b => (getRemainingDays(b) - buildRule.RemainingDays <= 0);
            return !build.Archived
                   && !build.Released
                   && (isExpired(build) && buildRule.ExpiredOnly
                      || !isExpired(build) && !buildRule.ExpiredOnly && isFitToRemainingDays(build))
                   && new Regex(buildRule.Mask).IsMatch(build.Name);
        }
    }
}
