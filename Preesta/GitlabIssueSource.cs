using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using GitlabGraphQL;
using Newtonsoft.Json.Linq;
using Preesta.Configuration.Action;
using Preesta.Data;
using Serilog;

namespace Preesta
{
    /// <summary>
    /// Maps GitLab's GraphQL <c>Query.issues</c> response into the shared
    /// <see cref="Issue"/> model. Sibling of <see cref="LinearIssueSource"/> and
    /// <see cref="GithubIssueSource"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// GitLab's GraphQL <c>Query.issues</c> requires at least one filter (the
    /// <see cref="GitlabRule.Filter"/> object guarantees this on the YAML side).
    /// Fields users see as filter chips in the web UI are forwarded verbatim as
    /// GraphQL variables — the names match GraphQL argument names exactly so the
    /// mapping is mechanical.
    /// </para>
    /// <para>
    /// Phase 13 covers Issues only — not Merge Requests. GitLab's GraphQL has no
    /// top-level <c>Query.mergeRequests</c> field; MR listings live under
    /// <c>Project.mergeRequests</c> / <c>Group.mergeRequests</c>, which would need a
    /// different rule shape (mandatory project/group scope). Deferred to a follow-up
    /// phase so the MVP lands cleanly with one query and one rule shape.
    /// </para>
    /// </remarks>
    public class GitlabIssueSource
    {
        // One trip — all fields we map into the Issue model. publicEmail is optional
        // on the User type (only filled when the user has exposed it in profile
        // settings); when null we still keep the User object so display name / login
        // are usable and the marker resolver simply skips routing for that issue.
        private const string SearchQuery = @"
query(
  $state: IssuableState,
  $labelName: [String!],
  $assigneeUsernames: [String!],
  $authorUsername: String,
  $milestoneTitle: [String!],
  $search: String,
  $createdAfter: Time,
  $createdBefore: Time,
  $updatedAfter: Time,
  $updatedBefore: Time,
  $confidential: Boolean,
  $iids: [ID!]
) {
  issues(
    state: $state,
    labelName: $labelName,
    assigneeUsernames: $assigneeUsernames,
    authorUsername: $authorUsername,
    milestoneTitle: $milestoneTitle,
    search: $search,
    createdAfter: $createdAfter,
    createdBefore: $createdBefore,
    updatedAfter: $updatedAfter,
    updatedBefore: $updatedBefore,
    confidential: $confidential,
    iids: $iids,
    first: 100
  ) {
    nodes {
      id iid title webUrl state createdAt updatedAt closedAt confidential
      author { username name publicEmail }
      assignees(first: 5) { nodes { username name publicEmail } }
      labels(first: 20) { nodes { title } }
      milestone { title }
      reference(full: true)
    }
  }
}";

        private readonly IGitlabGateway _gateway;
        private readonly ILogger? _logger;

        public GitlabIssueSource(IGitlabGateway gateway, ILogger? logger = null)
        {
            _gateway = gateway;
            _logger = logger;
        }

        public GitlabIssueSource(string token, HttpClient? httpClient = null, ILogger? logger = null)
            : this(new GitlabConnection(token, httpClient), logger)
        {
        }

        public virtual Issue[] GetIssues(GitlabRule rule)
        {
            if (rule.Filter == null || !rule.Filter.HasAnyField)
            {
                _logger?.Warning("GitLab rule has no filter fields set; skipping");
                return Array.Empty<Issue>();
            }

            JObject response;
            try
            {
                response = _gateway.Query(SearchQuery, BuildVariables(rule.Filter));
            }
            catch (Exception ex)
            {
                _logger?.Warning(ex, "Failed to fetch issues from GitLab for filter '{Filter}'",
                    rule.Filter.ToHumanString());
                return Array.Empty<Issue>();
            }

            var errors = response["errors"] as JArray;
            if (errors != null && errors.Count > 0)
            {
                _logger?.Error("GitLab GraphQL returned errors for filter '{Filter}': {Errors}",
                    rule.Filter.ToHumanString(), errors.ToString(Newtonsoft.Json.Formatting.None));
                return Array.Empty<Issue>();
            }

            var nodes = response.SelectToken("data.issues.nodes") as JArray;
            if (nodes == null) return Array.Empty<Issue>();
            return nodes.OfType<JObject>().Select(MapNode).ToArray();
        }

        /// <summary>
        /// Materialises only those filter properties that are actually set, so we don't
        /// send a wall of <c>"foo": null</c> to GitLab (which is harmless but noisy in
        /// the WireMock logs and in production troubleshooting).
        /// </summary>
        internal static object BuildVariables(GitlabFilter filter)
        {
            var d = new Dictionary<string, object?>();
            if (!string.IsNullOrEmpty(filter.State)) d["state"] = filter.State!.ToLowerInvariant();
            if (filter.LabelName != null && filter.LabelName.Length > 0) d["labelName"] = filter.LabelName;
            if (filter.AssigneeUsernames != null && filter.AssigneeUsernames.Length > 0) d["assigneeUsernames"] = filter.AssigneeUsernames;
            if (!string.IsNullOrEmpty(filter.AuthorUsername)) d["authorUsername"] = filter.AuthorUsername;
            if (filter.MilestoneTitle != null && filter.MilestoneTitle.Length > 0) d["milestoneTitle"] = filter.MilestoneTitle;
            if (!string.IsNullOrEmpty(filter.Search)) d["search"] = filter.Search;
            if (!string.IsNullOrEmpty(filter.CreatedAfter)) d["createdAfter"] = filter.CreatedAfter;
            if (!string.IsNullOrEmpty(filter.CreatedBefore)) d["createdBefore"] = filter.CreatedBefore;
            if (!string.IsNullOrEmpty(filter.UpdatedAfter)) d["updatedAfter"] = filter.UpdatedAfter;
            if (!string.IsNullOrEmpty(filter.UpdatedBefore)) d["updatedBefore"] = filter.UpdatedBefore;
            if (filter.Confidential.HasValue) d["confidential"] = filter.Confidential.Value;
            if (filter.Iids != null && filter.Iids.Length > 0) d["iids"] = filter.Iids;
            return d;
        }

        internal static Issue MapNode(JObject node)
        {
            // `reference(full: true)` returns "group/project#42" — same shape as
            // GitHub's `nameWithOwner#number` so the digest header reads consistently
            // across trackers. Fallback to "#iid" if reference is absent for any reason.
            var reference = (string?)node["reference"];
            var iid = (string?)node["iid"];
            var key = !string.IsNullOrEmpty(reference)
                ? reference!
                : (iid != null ? "#" + iid : string.Empty);

            var state = (string?)node["state"];

            return new Issue
            {
                Key = key,
                GitlabGlobalId = (string?)node["id"],
                Summary = (string?)node["title"] ?? string.Empty,
                Url = (string?)node["webUrl"],
                Status = NormalizeState(state),
                Type = "Issue",
                Participants = new IssueParticipants
                {
                    Assignee = ToUser(node.SelectToken("assignees.nodes[0]") as JObject),
                    // GitLab has no separate reporter — use author for both, mirroring Linear/GitHub.
                    Reporter = ToUser(node["author"] as JObject),
                    Creator = ToUser(node["author"] as JObject)
                },
                ProjectKey = (string?)node.SelectToken("milestone.title"),
                Labels = string.Join(", ", LabelNames(node)),
                CreatedDate = ParseNullableDate(node["createdAt"]) ?? DateTime.MinValue,
                UpdatedDate = ParseNullableDate(node["updatedAt"]),
                Resolution = string.Equals(state, "closed", StringComparison.OrdinalIgnoreCase)
                    ? "Closed" : null
            };
        }

        private static string? NormalizeState(string? state)
        {
            // GitLab returns `opened` / `closed` / `locked`. Title-case for display so
            // it sits naturally in chip pills next to Jira's "In Progress".
            if (string.IsNullOrEmpty(state)) return null;
            return char.ToUpperInvariant(state[0]) + state.Substring(1).ToLowerInvariant();
        }

        private static IEnumerable<string> LabelNames(JObject node)
        {
            var labelNodes = node.SelectToken("labels.nodes") as JArray;
            if (labelNodes == null) yield break;
            foreach (var l in labelNodes.OfType<JObject>())
            {
                // GitLab's Label type uses `title`, not `name`.
                var name = (string?)l["title"];
                if (!string.IsNullOrEmpty(name))
                    yield return name!;
            }
        }

        private static User? ToUser(JObject? user)
        {
            if (user == null) return null;
            var login = (string?)user["username"];
            var displayName = (string?)user["name"] ?? login;
            var email = (string?)user["publicEmail"];
            return new User
            {
                DisplayName = displayName,
                Name = displayName,
                // GitLab returns null for `publicEmail` when the user has not exposed
                // it in profile settings. We keep the User object (login/displayName
                // still useful for the digest header) but Email="" so the marker
                // resolver simply skips routing for this issue, mirroring GitHub's
                // hidden-email behaviour.
                Email = string.IsNullOrEmpty(email) ? string.Empty : email!,
                Key = login
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
    }
}
