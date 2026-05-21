using System;
using System.Text.RegularExpressions;
using Preesta.Data;
using System.Linq;

namespace Preesta
{
    internal static class ExtendedFilteringPredicates
    {
        internal static bool MoreThanOneFixVersion(IJiraService? jira, Issue issue)
        {
            return issue.FixVersions!.Count() > 1;
        }

        internal static bool DueDateExpiredMoreThan2WorkingDays(IJiraService? jira, Issue issue)
        {
            return Algorithm.DueDateExpiredMoreThan2WorkingDays(DateTime.Now.Date, issue.DueDate);
        }

        internal static bool EstimatesAttachmentIsAbsent(IJiraService? jira, Issue issue)
        {
            // Jira-dependent by definition — activating it on a non-Jira rule
            // without Jira configured is a misconfiguration, surfaced here.
            if (jira is null)
                throw new InvalidOperationException(
                    "EstimatesAttachmentIsAbsent requires Jira to be configured.");
            return
                !jira
                     .GetIssueAttachments(issue.Key)
                     .Any(
                         a =>
                         new Regex("Согласование оценки (CR|BR)-\\d+\\.msg", RegexOptions.IgnoreCase).IsMatch(a.Filename))
                ;
        }

        internal static class Algorithm
        {
            internal static bool DueDateExpiredMoreThan2WorkingDays(DateTime today, DateTime? dueDate)
            {
                var todayDate = today.Date;
                var dueDateDate = (dueDate ?? today).Date;

                var workingDaysBetweenTodayAndDueDate = Enumerable.Range(1, Math.Max(0, (int) (todayDate - dueDateDate).TotalDays))
                    .Select(i => dueDateDate + TimeSpan.FromDays(i))
                    .Count(d => !new[] { DayOfWeek.Saturday, DayOfWeek.Sunday }.Contains(d.DayOfWeek))
                    ;

                return workingDaysBetweenTodayAndDueDate >= 2;
            }
        }
    }
}