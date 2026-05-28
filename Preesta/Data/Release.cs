using System;

namespace Preesta.Data
{
    public class Release
    {
        public string? Description { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool Archived { get; set; }
        public bool Released { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? ReleaseDate { get; set; }

        /// <summary>"Open in Jira" link for the release-watch digest. Populated
        /// by <see cref="HttpJiraService.GetReleases"/> from rootUri + project
        /// code + version id (e.g. <c>https://host/projects/SCRUM/versions/10001</c>).
        /// Jira Cloud redirects that to the "All work" view filtered by
        /// fixVersion — the digest reader's question "what's in this version?".</summary>
        public string? Url { get; set; }
    }
}
