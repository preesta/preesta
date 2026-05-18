using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using Preesta.Configuration.Action;
using Preesta.Data;
using Serilog;
using ShortcutRest;

namespace Preesta
{
    /// <summary>
    /// Maps Shortcut's REST <c>/api/v3/search/stories</c> response into the shared
    /// <see cref="Issue"/> model. REST-only sibling of <see cref="LinearIssueSource"/>
    /// and <see cref="GithubIssueSource"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each <see cref="GetIssues(ShortcutRule)"/> call issues exactly one
    /// <c>GET /api/v3/search/stories?query=…&amp;page_size=…&amp;detail=slim</c>.
    /// Shortcut's search syntax (the same one users see in the web UI) lives in
    /// <see cref="ShortcutRule.Filter"/> verbatim.
    /// </para>
    /// <para>
    /// <b>Resolving foreign IDs to human-readable names</b>. Shortcut returns
    /// integer/UUID identifiers for workflow state, owners and requester. To produce
    /// useful digests we need names + emails — that requires two auxiliary REST
    /// roundtrips (<c>GET /api/v3/workflows</c>, <c>GET /api/v3/members</c>) which we
    /// run lazily on first use and cache for the lifetime of this source instance.
    /// Failures fall back to the raw ID as display name (with empty email — routing
    /// via <c>mailTo: assignee</c> will then skip silently rather than producing a
    /// <c>To: </c> line with no address).
    /// </para>
    /// </remarks>
    public class ShortcutIssueSource
    {
        private readonly IShortcutGateway _gateway;
        private readonly ILogger? _logger;

        // Lazy + thread-safe via Lazy<T>: the first GetIssues call populates these
        // dictionaries with a single REST hop each; subsequent calls reuse them.
        // For digests that batch dozens of rules this trades one cold hit for n-1
        // warm lookups.
        private readonly Lazy<Dictionary<long, string>> _stateNames;
        private readonly Lazy<Dictionary<string, MemberInfo>> _members;
        private readonly Lazy<string?> _workspaceSlug;

        public ShortcutIssueSource(IShortcutGateway gateway, ILogger? logger = null)
        {
            _gateway = gateway;
            _logger = logger;
            _stateNames = new Lazy<Dictionary<long, string>>(LoadStateNames);
            _members = new Lazy<Dictionary<string, MemberInfo>>(LoadMembers);
            _workspaceSlug = new Lazy<string?>(LoadWorkspaceSlug);
        }

        /// <summary>
        /// Workspace URL slug (segment in <c>app.shortcut.com/&lt;slug&gt;/</c>).
        /// Populated lazily from <c>/api/v3/member</c> on first access. Returns
        /// <c>null</c> if the call fails — the supplier then skips the round-trip
        /// link in the digest header.
        /// </summary>
        public virtual string? WorkspaceSlug => _workspaceSlug.Value;

        private string? LoadWorkspaceSlug()
        {
            try
            {
                var member = _gateway.GetCurrentMember();
                return (string?)member.SelectToken("workspace2.url_slug");
            }
            catch (Exception ex)
            {
                _logger?.Warning(ex, "Failed to load Shortcut workspace slug — round-trip link will be omitted");
                return null;
            }
        }

        public ShortcutIssueSource(string apiToken, HttpClient? httpClient = null, ILogger? logger = null)
            : this(new ShortcutConnection(apiToken, httpClient), logger)
        {
        }

        public virtual Issue[] GetIssues(ShortcutRule rule)
        {
            if (string.IsNullOrWhiteSpace(rule.Filter))
            {
                _logger?.Warning("Shortcut rule has no filter set; skipping");
                return Array.Empty<Issue>();
            }

            JObject response;
            try
            {
                response = _gateway.SearchStories(rule.Filter, pageSize: 100);
            }
            catch (Exception ex)
            {
                _logger?.Warning(ex, "Failed to fetch stories from Shortcut for filter '{Filter}'", rule.Filter);
                return Array.Empty<Issue>();
            }

            var data = response["data"] as JArray;
            if (data == null) return Array.Empty<Issue>();

            return data.OfType<JObject>().Select(node => MapNode(node, _stateNames.Value, _members.Value)).ToArray();
        }

        internal static Issue MapNode(
            JObject node,
            IReadOnlyDictionary<long, string> stateNames,
            IReadOnlyDictionary<string, MemberInfo> members)
        {
            var id = (long?)node["id"];
            var stateId = (long?)node["workflow_state_id"];
            var storyType = (string?)node["story_type"];
            var stateName = stateId.HasValue && stateNames.TryGetValue(stateId.Value, out var sn) ? sn : null;

            var assigneeId = (node["owner_ids"] as JArray)?.OfType<JValue>().Select(v => v.Value?.ToString())
                                                          .FirstOrDefault(s => !string.IsNullOrEmpty(s));
            var requesterId = (string?)node["requested_by_id"];

            return new Issue
            {
                // Key: "sc-{id}" — short, unambiguous, mirrors Shortcut's own branch
                // naming convention (e.g. "sc-1234/some-slug" on Git branches). Shortcut
                // doesn't expose a project abbreviation on the search result itself.
                Key = id.HasValue ? $"sc-{id.Value}" : string.Empty,
                ShortcutId = id?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Summary = (string?)node["name"] ?? string.Empty,
                Url = (string?)node["app_url"],
                Status = stateName,
                Type = storyType,
                Participants = new IssueParticipants
                {
                    Assignee = ToUser(assigneeId, members),
                    // Shortcut has no separate reporter — use requester for both,
                    // mirroring Linear (creator → reporter+creator) and GitHub
                    // (author → reporter+creator).
                    Reporter = ToUser(requesterId, members),
                    Creator = ToUser(requesterId, members)
                },
                Labels = string.Join(", ", LabelNames(node)),
                CreatedDate = ParseNullableDate(node["created_at"]) ?? DateTime.MinValue,
                UpdatedDate = ParseNullableDate(node["updated_at"]),
                DueDate = ParseNullableDate(node["deadline"])
                // No Resolution mapping — Shortcut doesn't expose a "resolution" concept;
                // the workflow state name already carries the meaning ("Completed", "Cancelled").
            };
        }

        private static IEnumerable<string> LabelNames(JObject node)
        {
            var labels = node["labels"] as JArray;
            if (labels == null) yield break;
            foreach (var l in labels.OfType<JObject>())
            {
                var name = (string?)l["name"];
                if (!string.IsNullOrEmpty(name))
                    yield return name!;
            }
        }

        private static User? ToUser(string? memberId, IReadOnlyDictionary<string, MemberInfo> members)
        {
            if (string.IsNullOrEmpty(memberId)) return null;
            if (!members.TryGetValue(memberId, out var info))
            {
                // Unknown member (cache miss, disabled member, or members fetch failed):
                // return a User with the bare ID so the digest still renders something
                // and obezlichennye-rules routing through `mailTo: assignee` gracefully
                // skips this issue (empty email → no recipient).
                return new User
                {
                    DisplayName = memberId,
                    Name = memberId,
                    Email = string.Empty,
                    Key = memberId
                };
            }
            return new User
            {
                DisplayName = info.Name ?? info.MentionName ?? memberId,
                Name = info.Name ?? info.MentionName ?? memberId,
                Email = info.Email ?? string.Empty,
                Key = memberId
            };
        }

        private static DateTime? ParseNullableDate(JToken? token)
        {
            if (token == null || token.Type == JTokenType.Null) return null;
            var s = (string?)token;
            if (string.IsNullOrEmpty(s)) return null;
            if (DateTimeOffset.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal, out var dto))
                return dto.UtcDateTime;
            return null;
        }

        private Dictionary<long, string> LoadStateNames()
        {
            var map = new Dictionary<long, string>();
            try
            {
                var workflows = _gateway.GetWorkflows();
                foreach (var wf in workflows.OfType<JObject>())
                {
                    var states = wf["states"] as JArray;
                    if (states == null) continue;
                    foreach (var st in states.OfType<JObject>())
                    {
                        var sid = (long?)st["id"];
                        var name = (string?)st["name"];
                        if (sid.HasValue && !string.IsNullOrEmpty(name))
                            map[sid.Value] = name!;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Warning(ex, "Failed to fetch Shortcut workflows — story status will fall back to raw state id");
            }
            return map;
        }

        private Dictionary<string, MemberInfo> LoadMembers()
        {
            var map = new Dictionary<string, MemberInfo>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var members = _gateway.GetMembers();
                foreach (var m in members.OfType<JObject>())
                {
                    var id = (string?)m["id"];
                    if (string.IsNullOrEmpty(id)) continue;
                    var profile = m["profile"] as JObject;
                    map[id!] = new MemberInfo
                    {
                        Name = (string?)profile?["name"],
                        MentionName = (string?)profile?["mention_name"],
                        Email = (string?)profile?["email_address"]
                    };
                }
            }
            catch (Exception ex)
            {
                _logger?.Warning(ex, "Failed to fetch Shortcut members — assignee/requester routing will skip silently for unresolved IDs");
            }
            return map;
        }

        /// <summary>Internal projection of a Shortcut Member's profile fields we care about.</summary>
        internal sealed class MemberInfo
        {
            public string? Name { get; init; }
            public string? MentionName { get; init; }
            public string? Email { get; init; }
        }
    }
}
