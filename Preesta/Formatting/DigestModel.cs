using System.Collections.Generic;

namespace Preesta.Formatting
{
    internal class DigestModel
    {
        public IReadOnlyList<DigestSection> Sections { get; set; } = new List<DigestSection>();
    }

    internal class DigestSection
    {
        public string? Recommendations { get; set; }
        // Jira: pre-Phase-12.2 link to the JQL view. Kept verbatim — Jira section
        // rendering must not change byte-for-byte.
        public string? JqlUri { get; set; }
        // Linear (Phase 12.2): saved-view permalink. Only set for viewId-mode rules
        // when LinearWorkspace is configured; the AI-prompt and raw-filter modes
        // have no canonical URL (Linear stores filter state in localStorage), so
        // we deliberately leave this null rather than fall back to "My Issues".
        public string? LinearViewUri { get; set; }
        // Linear (Phase 12.2): human-readable description of what produced the list,
        // shown under the recommendations. Format depends on the rule's filter mode:
        //   AI prompt   → "AI filter: «<prompt>»"
        //   raw filter  → "Filter: <compact JSON, truncated to 200 chars>"
        //   saved view  → "View: <name or id>"
        public string? FilterDescription { get; set; }
        public IReadOnlyList<DigestItem> Items { get; set; } = new List<DigestItem>();
    }

    internal class DigestItem
    {
        public string Key { get; set; } = "";
        public string Summary { get; set; } = "";
        public string BrowseUri { get; set; } = "";
        public IReadOnlyList<string> MetaChips { get; set; } = new List<string>();
        public string MetaText { get; set; } = "";
    }

    internal class ReleaseDigestModel
    {
        public IReadOnlyList<ReleaseDigestSection> Sections { get; set; } = new List<ReleaseDigestSection>();
    }

    internal class ReleaseDigestSection
    {
        public string Subject { get; set; } = "";
        public string? Recommendations { get; set; }
        public IReadOnlyList<ReleaseRow> Builds { get; set; } = new List<ReleaseRow>();
    }

    internal class ReleaseRow
    {
        public string Name { get; set; } = "";
        public string ReleaseDate { get; set; } = "";
        public bool Expired { get; set; }
    }
}
