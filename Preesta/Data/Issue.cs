using System;
using static System.String;

namespace Preesta.Data
{
    public class Issue
    {
        public string Key { get; set; } = Empty;
        public string Summary { get; set; } = Empty;
        public IssueStaff Staff { get; set; } = new IssueStaff();
        public string? Type { get; set; }
        public string? Status { get; set; }
        public string? Priority { get; set; }
        public string? Components { get; set; }
        public string Labels { get; set; } = Empty;
        public TimeSpan TimeSpent { get; set; }
        public string[]? AffectsVersions { get; set; }
        public string[]? FixVersions { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public string? Resolution { get; set; }
        public string? ProjectKey { get; set; }
    }
}