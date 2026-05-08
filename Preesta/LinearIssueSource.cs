using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using LinearGraphQL;
using Newtonsoft.Json.Linq;
using Preesta.Configuration.Action;
using Preesta.Data;
using Serilog;

namespace Preesta
{
    /// <summary>
    /// Maps Linear's GraphQL response into the shared <see cref="Issue"/> model.
    /// Sibling of <see cref="HttpJiraService"/> — kept as a concrete class
    /// (no <c>IIssueSource</c> abstraction yet, per Phase 12 architecture decision).
    /// </summary>
    /// <remarks>
    /// Each call to <see cref="GetIssues(LinearRule)"/> dispatches on the rule's filter
    /// mode (see <see cref="LinearRule"/> for the three options). Per-rule failures
    /// are logged and turned into an empty array — the pipeline keeps going for the
    /// other rules in the group.
    /// </remarks>
    public class LinearIssueSource
    {
        // Shared field projection — used by both `issues(filter:)` and `customView.issues`.
        private const string IssueFields =
            "identifier title url state { name type } priority priorityLabel " +
            "assignee { id name email } creator { id name email } " +
            "project { id name } labels { nodes { name } } " +
            "dueDate createdAt updatedAt";

        // Hop 1 of the AI-prompt path: ask Linear to translate a natural-language prompt
        // into a Linear filter object. Returns { data: { issueFilterSuggestion: { filter } } }.
        private const string FilterSuggestionQuery =
            "query($prompt: String!) { issueFilterSuggestion(prompt: $prompt) { filter } }";

        // Used by both AI-prompt (hop 2) and raw-filter paths.
        private static readonly string IssuesByFilterQuery =
            "query($filter: IssueFilter!) { issues(filter: $filter) { nodes { " + IssueFields + " } } }";

        // Saved-view path — Linear evaluates the view server-side; we just unwrap nodes.
        // We also pull `name` so the digest header can show "View: My Sprint Blockers"
        // instead of the opaque UUID.
        private static readonly string CustomViewQuery =
            "query($id: String!) { customView(id: $id) { name issues { nodes { " + IssueFields + " } } } }";

        private readonly ILinearGateway _gateway;
        private readonly ILogger? _logger;

        // Populated by GetByViewId on each successful customView fetch. Cleared per
        // process — there's no TTL because the supplier reads it inside the same
        // GetPackages() pass that triggers the fetch.
        private readonly ConcurrentDictionary<string, string> _viewNamesById = new();

        public LinearIssueSource(ILinearGateway gateway, ILogger? logger = null)
        {
            _gateway = gateway;
            _logger = logger;
        }

        public LinearIssueSource(string apiKey, HttpClient? httpClient = null, ILogger? logger = null)
            : this(new LinearConnection(apiKey, httpClient), logger)
        {
        }

        /// <summary>
        /// Dispatches on the rule's filter mode. Validation in the YAML converter
        /// guarantees exactly one of {Filter, FilterRaw, ViewId} is set; if a caller
        /// somehow hands us a malformed rule we log and return empty.
        /// </summary>
        public virtual Issue[] GetIssues(LinearRule rule)
        {
            try
            {
                if (rule.Filter != null)
                    return GetByPrompt(rule.Filter);
                if (rule.FilterRaw != null)
                    return GetByRawFilter(rule.FilterRaw);
                if (rule.ViewId != null)
                    return GetByViewId(rule.ViewId);

                _logger?.Warning("Linear rule has no filter source set; skipping");
                return Array.Empty<Issue>();
            }
            catch (Exception ex)
            {
                _logger?.Warning(ex, "Failed to fetch issues from Linear");
                return Array.Empty<Issue>();
            }
        }

        private Issue[] GetByPrompt(string prompt)
        {
            var suggestion = _gateway.Query(FilterSuggestionQuery, new { prompt });
            if (HasErrors(suggestion, "issueFilterSuggestion"))
                return Array.Empty<Issue>();

            var filter = suggestion.SelectToken("data.issueFilterSuggestion.filter") as JObject;
            if (filter == null)
            {
                _logger?.Warning("Linear issueFilterSuggestion returned no filter for prompt {Prompt}", prompt);
                return Array.Empty<Issue>();
            }

            return GetByRawFilter(filter);
        }

        private Issue[] GetByRawFilter(JObject filter)
        {
            var response = _gateway.Query(IssuesByFilterQuery, new { filter });
            if (HasErrors(response, "issues"))
                return Array.Empty<Issue>();

            var nodes = response.SelectToken("data.issues.nodes") as JArray;
            if (nodes == null) return Array.Empty<Issue>();
            return nodes.OfType<JObject>().Select(MapNode).ToArray();
        }

        private Issue[] GetByViewId(string viewId)
        {
            var response = _gateway.Query(CustomViewQuery, new { id = viewId });
            if (HasErrors(response, "customView"))
                return Array.Empty<Issue>();

            var name = (string?)response.SelectToken("data.customView.name");
            if (!string.IsNullOrEmpty(name))
                _viewNamesById[viewId] = name!;

            var nodes = response.SelectToken("data.customView.issues.nodes") as JArray;
            if (nodes == null) return Array.Empty<Issue>();
            return nodes.OfType<JObject>().Select(MapNode).ToArray();
        }

        /// <summary>
        /// Returns the human-readable name of the given saved view if a previous
        /// <c>GetIssues(rule)</c> call (in this process) successfully resolved it,
        /// otherwise <c>null</c>. Used by <c>LinearIssueSupplier.Enrich</c> to put the
        /// name on the package for the formatter; if missing, the supplier falls back
        /// to the id.
        /// </summary>
        public virtual string? GetCachedViewName(string viewId)
        {
            return _viewNamesById.TryGetValue(viewId, out var name) ? name : null;
        }

        private bool HasErrors(JObject response, string context)
        {
            var errors = response["errors"] as JArray;
            if (errors != null && errors.Count > 0)
            {
                _logger?.Error("Linear GraphQL ({Context}) returned errors: {Errors}", context, errors.ToString());
                return true;
            }
            return false;
        }

        internal static Issue MapNode(JObject node)
        {
            var stateName = (string?)node.SelectToken("state.name");
            var stateType = (string?)node.SelectToken("state.type");

            return new Issue
            {
                Key = (string?)node["identifier"] ?? string.Empty,
                Summary = (string?)node["title"] ?? string.Empty,
                Url = (string?)node["url"],
                Status = stateName,
                Priority = (string?)node["priorityLabel"],
                Participants = new IssueParticipants
                {
                    Assignee = ToUser(node["assignee"] as JObject),
                    // Linear has no separate "reporter" — use creator for both.
                    Reporter = ToUser(node["creator"] as JObject),
                    Creator = ToUser(node["creator"] as JObject)
                },
                ProjectKey = (string?)node.SelectToken("project.name"),
                Labels = string.Join(", ", LabelNames(node)),
                DueDate = ParseNullableDate(node["dueDate"]),
                CreatedDate = ParseNullableDate(node["createdAt"]) ?? DateTime.MinValue,
                UpdatedDate = ParseNullableDate(node["updatedAt"]),
                Resolution = string.Equals(stateType, "completed", StringComparison.OrdinalIgnoreCase) ? stateName : null
            };
        }

        private static IEnumerable<string> LabelNames(JObject node)
        {
            var labelNodes = node.SelectToken("labels.nodes") as JArray;
            if (labelNodes == null) yield break;
            foreach (var l in labelNodes.OfType<JObject>())
            {
                var name = (string?)l["name"];
                if (!string.IsNullOrEmpty(name))
                    yield return name!;
            }
        }

        private static User? ToUser(JObject? user)
        {
            if (user == null) return null;
            var name = (string?)user["name"];
            return new User
            {
                DisplayName = name,
                Name = name,
                Email = (string?)user["email"] ?? string.Empty,
                Key = (string?)user["id"]
            };
        }

        private static DateTime? ParseNullableDate(JToken? token)
        {
            if (token == null || token.Type == JTokenType.Null) return null;
            var s = (string?)token;
            if (string.IsNullOrEmpty(s)) return null;
            // Linear timestamps are ISO-8601 with explicit offsets (usually Z).
            // Parse via DateTimeOffset to keep things tz-aware, then return the UTC DateTime.
            if (System.DateTimeOffset.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal, out var dto))
                return dto.UtcDateTime;
            return null;
        }
    }
}
