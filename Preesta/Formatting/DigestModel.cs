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
        public string? JqlUri { get; set; }
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
