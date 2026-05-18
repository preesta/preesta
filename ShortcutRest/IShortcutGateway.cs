using Newtonsoft.Json.Linq;

namespace ShortcutRest
{
    /// <summary>
    /// Minimal Shortcut REST gateway used by Preesta. REST-only (unlike Linear/GitHub
    /// which are GraphQL) — three methods cover read, mutate and member-list paths.
    /// </summary>
    public interface IShortcutGateway
    {
        /// <summary>
        /// GET <c>/api/v3/search/stories?query=…&amp;page_size=…&amp;detail=slim</c>.
        /// Returns the raw <c>StorySearchResults</c> envelope (<c>{ data: [...], total, next }</c>).
        /// </summary>
        JObject SearchStories(string query, int pageSize = 25);

        /// <summary>
        /// GET <c>/api/v3/members</c>. Returns a JSON array of <c>Member</c> objects
        /// (each carrying <c>id</c> + <c>profile.email_address</c> + <c>profile.name</c>).
        /// </summary>
        JArray GetMembers();

        /// <summary>
        /// GET <c>/api/v3/workflows</c>. Returns a JSON array of <c>Workflow</c> objects;
        /// each workflow's <c>states</c> array carries <c>id</c> + <c>name</c> + <c>type</c>.
        /// Used to resolve <c>story.workflow_state_id</c> → human-readable state name.
        /// </summary>
        JArray GetWorkflows();

        /// <summary>
        /// GET <c>/api/v3/member</c> — the authenticated user's profile, including
        /// <c>workspace2.url_slug</c> needed to build web-app deep links to the
        /// workspace (e.g. <c>app.shortcut.com/&lt;slug&gt;/...</c>).
        /// </summary>
        JObject GetCurrentMember();

        /// <summary>
        /// Generic REST passthrough for power-user mutations (Jira-style <c>mutations:</c>
        /// list). The verb / path / body come from the rule; the gateway just attaches
        /// the <c>Shortcut-Token</c> header and dispatches.
        /// </summary>
        void Send(string verb, string path, string? body);
    }
}
