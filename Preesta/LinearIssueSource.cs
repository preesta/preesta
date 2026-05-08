using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using LinearGraphQL;
using Newtonsoft.Json.Linq;
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
    /// MVP filter (assignee = viewer, state.type != completed) is hardcoded inside
    /// <see cref="LinearGraphQL.LinearConnection.GetAssignedIssues"/>.
    /// A user-friendly DSL for filters is deferred to Phase 12.1.
    /// </remarks>
    public class LinearIssueSource
    {
        private readonly ILinearGateway _gateway;
        private readonly ILogger? _logger;

        public LinearIssueSource(ILinearGateway gateway, ILogger? logger = null)
        {
            _gateway = gateway;
            _logger = logger;
        }

        public LinearIssueSource(string apiKey, HttpClient? httpClient = null, ILogger? logger = null)
            : this(new LinearConnection(apiKey, httpClient), logger)
        {
        }

        public virtual Issue[] GetAssignedIssues()
        {
            JObject response;
            try
            {
                response = _gateway.GetAssignedIssues();
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "Failed to query Linear GraphQL API");
                return Array.Empty<Issue>();
            }

            var errors = response["errors"] as JArray;
            if (errors != null && errors.Count > 0)
            {
                _logger?.Error("Linear GraphQL returned errors: {Errors}", errors.ToString());
                return Array.Empty<Issue>();
            }

            var nodes = response.SelectToken("data.viewer.assignedIssues.nodes") as JArray;
            if (nodes == null)
                return Array.Empty<Issue>();

            return nodes.OfType<JObject>().Select(ToIssue).ToArray();
        }

        internal static Issue ToIssue(JObject node)
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
            if (DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var d))
                return d;
            return null;
        }
    }
}
