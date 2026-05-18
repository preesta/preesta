using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using PlaneRest;
using Preesta.Configuration.Action;
using Preesta.Data;
using Serilog;

namespace Preesta
{
    /// <summary>
    /// Maps Plane's REST <c>/work-items/</c> response into the shared
    /// <see cref="Issue"/> model. Sibling of <see cref="GithubIssueSource"/> /
    /// <see cref="LinearIssueSource"/> — same shape, REST transport instead of
    /// GraphQL.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Plane returns assignees / created_by / updated_by as raw user UUIDs in the
    /// work-item payload, not embedded user objects. To route digests by email
    /// (the <c>assignee</c> / <c>reporter</c> markers in <c>mailTo</c>), we need
    /// a UUID → email map. The map is fetched once via
    /// <see cref="IPlaneGateway.ListWorkspaceMembers"/> and lazy-cached on this
    /// instance; lookup failures (HTTP error, no permissions) leave the map empty,
    /// which makes <c>Issue.Participants.Assignee.Email</c> blank and routing
    /// falls back to whatever direct addresses the rule set — same degraded path
    /// as GitHub's hidden-email case.
    /// </para>
    /// <para>
    /// Plane Cloud's public list endpoint accepts limited filter params (state,
    /// search, external_id, external_source, …). We pass the rule's
    /// <see cref="PlaneRule.Filter"/> map verbatim — Plane silently ignores
    /// unknown params, and pinning a curated set in our code would just lag
    /// behind Plane's API.
    /// </para>
    /// </remarks>
    public class PlaneIssueSource
    {
        private readonly IPlaneGateway _gateway;
        private readonly ILogger? _logger;
        private readonly Lazy<IReadOnlyDictionary<string, User>> _membersById;

        public PlaneIssueSource(IPlaneGateway gateway, ILogger? logger = null)
        {
            _gateway = gateway;
            _logger = logger;
            _membersById = new Lazy<IReadOnlyDictionary<string, User>>(LoadMembers);
        }

        public virtual Issue[] GetIssues(PlaneRule rule)
        {
            if (string.IsNullOrWhiteSpace(rule.ProjectId))
            {
                _logger?.Warning("Plane rule has no projectId; skipping");
                return Array.Empty<Issue>();
            }

            JObject response;
            try
            {
                response = _gateway.ListWorkItems(rule.ProjectId!, rule.Filter ?? new Dictionary<string, string>());
            }
            catch (Exception ex)
            {
                _logger?.Warning(ex, "Failed to fetch work items from Plane for projectId '{ProjectId}'", rule.ProjectId);
                return Array.Empty<Issue>();
            }

            // Plane returns either { results: [...] } (paginated) or a bare array
            // for endpoints that don't paginate. List endpoint is always paginated;
            // be defensive in case a self-hosted Plane variant returns the bare form.
            var items = response["results"] as JArray;
            if (items == null) return Array.Empty<Issue>();

            return items.OfType<JObject>().Select(MapNode).ToArray();
        }

        internal Issue MapNode(JObject node)
        {
            // Plane returns the project id as a UUID with no project key/code in the
            // work-item payload. Use sequence_id (auto-incrementing per-project) for
            // the human-visible part of Key — same role as GitHub's "#NN" or Jira's
            // "-NNN". Without the project prefix the key still uniquely identifies
            // the issue within a project; if multiple Plane projects feed the same
            // digest, the project field (rendered as a chip via ProjectKey) keeps
            // them distinguishable.
            var sequenceId = (int?)node["sequence_id"];
            var projectId = (string?)node["project"];
            var key = sequenceId == null ? (projectId ?? string.Empty) : $"#{sequenceId}";

            var assigneeIds = (node["assignees"] as JArray)?.Select(t => (string?)t).Where(s => !string.IsNullOrEmpty(s)).ToArray()
                              ?? Array.Empty<string?>();
            var createdById = (string?)node["created_by"];

            var members = _membersById.Value;

            return new Issue
            {
                Key = key,
                PlaneId = (string?)node["id"],
                Summary = (string?)node["name"] ?? string.Empty,
                // Plane's REST payload does not include a canonical browse URL — the
                // formatter would fall back to "<root>/browse/{key}", which doesn't
                // route to anything sensible. Leave Url null and let the converter
                // build it from the workspace slug + project + sequence_id.
                Url = null,
                // Plane returns `state` as a UUID string by default. With expand=state
                // it inlines the full state object into the same key (not a separate
                // state_detail field as the docs suggest). Handle both shapes.
                Status = NormaliseStatus(node["state"] as JObject ?? node["state_detail"] as JObject,
                    node["state"]?.Type == JTokenType.String ? (string?)node["state"] : null),
                Priority = NormalisePriority((string?)node["priority"]),
                Participants = new IssueParticipants
                {
                    Assignee = LookupUser(assigneeIds.FirstOrDefault(), members),
                    // Plane has no separate "reporter" concept — use created_by for both,
                    // mirroring Linear / GitHub.
                    Reporter = LookupUser(createdById, members),
                    Creator = LookupUser(createdById, members)
                },
                ProjectKey = projectId,
                Labels = string.Join(", ", LabelNames(node, members)),
                DueDate = ParseNullableDate(node["target_date"]),
                CreatedDate = ParseNullableDate(node["created_at"]) ?? DateTime.MinValue,
                UpdatedDate = ParseNullableDate(node["updated_at"]),
                Resolution = node["completed_at"]?.Type == JTokenType.Null || node["completed_at"] == null
                    ? null
                    : "Completed"
            };
        }

        // Plane returns either { state_detail: { name, group } } (when expanded) or
        // a bare state UUID in { state: "<uuid>" }. Prefer the human name; fall back
        // to the UUID if that's all we have, with a "state-<short>" prefix so it's
        // obvious in the digest that this is an unresolved id.
        private static string? NormaliseStatus(JObject? stateDetail, string? stateUuid)
        {
            if (stateDetail != null)
            {
                var name = (string?)stateDetail["name"];
                if (!string.IsNullOrEmpty(name)) return name;
            }
            if (!string.IsNullOrEmpty(stateUuid))
                return stateUuid!.Length > 8 ? $"state-{stateUuid.Substring(0, 8)}" : stateUuid;
            return null;
        }

        private static string? NormalisePriority(string? priority)
        {
            if (string.IsNullOrEmpty(priority)) return null;
            // Plane uses lowercase: none, urgent, high, medium, low. Map to the
            // title-case names the formatter's icon table expects.
            return priority switch
            {
                "urgent" => "Urgent",
                "high" => "High",
                "medium" => "Medium",
                "low" => "Low",
                "none" => null,
                _ => priority
            };
        }

        private static IEnumerable<string> LabelNames(JObject node, IReadOnlyDictionary<string, User> members)
        {
            // Plane returns labels as UUIDs by default. With expand=labels they
            // come as objects with `name`. Handle both — strings get rendered as
            // "label-<short>" so the digest doesn't show opaque UUIDs.
            var labels = node["labels"] as JArray;
            if (labels == null) yield break;
            foreach (var l in labels)
            {
                if (l is JObject obj)
                {
                    var name = (string?)obj["name"];
                    if (!string.IsNullOrEmpty(name)) yield return name!;
                }
                else
                {
                    var s = (string?)l;
                    if (!string.IsNullOrEmpty(s))
                        yield return s!.Length > 8 ? $"label-{s.Substring(0, 8)}" : s;
                }
            }
        }

        private static User? LookupUser(string? id, IReadOnlyDictionary<string, User> members)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (members.TryGetValue(id!, out var u)) return u;
            // Member not found (left workspace, deactivated, or members lookup
            // failed). Return a minimal User so the digest renders something
            // sensible and the marker resolver gets an Email="" — same fallback
            // as GitHub's hidden-email case.
            return new User { Key = id, Email = string.Empty };
        }

        private IReadOnlyDictionary<string, User> LoadMembers()
        {
            try
            {
                var arr = _gateway.ListWorkspaceMembers();
                var map = new Dictionary<string, User>(StringComparer.Ordinal);
                foreach (var entry in arr.OfType<JObject>())
                {
                    // Plane wraps member info under "member" on workspace endpoints;
                    // on some endpoints the fields are flat. Probe both.
                    var member = entry["member"] as JObject ?? entry;
                    var id = (string?)member["id"];
                    if (string.IsNullOrEmpty(id)) continue;

                    var email = (string?)member["email"] ?? string.Empty;
                    var displayName = (string?)member["display_name"]
                                      ?? (string?)member["first_name"]
                                      ?? email;

                    map[id!] = new User
                    {
                        Key = id,
                        Email = email,
                        DisplayName = displayName,
                        Name = displayName
                    };
                }
                return map;
            }
            catch (Exception ex)
            {
                _logger?.Warning(ex,
                    "Failed to load Plane workspace members; assignee email routing will be degraded");
                return new Dictionary<string, User>();
            }
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
    }
}
