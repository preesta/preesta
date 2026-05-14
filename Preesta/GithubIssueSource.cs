using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using GithubGraphQL;
using Newtonsoft.Json.Linq;
using Preesta.Configuration.Action;
using Preesta.Data;
using Serilog;

namespace Preesta
{
    /// <summary>
    /// Maps GitHub's GraphQL <c>search(type: ISSUE)</c> response into the shared
    /// <see cref="Issue"/> model. Sibling of <see cref="LinearIssueSource"/> — same
    /// shape, simpler logic (one query, no AI hop, no view fallback).
    /// </summary>
    /// <remarks>
    /// <para>
    /// GitHub's <c>type: ISSUE</c> covers both real issues and pull requests (a PR is
    /// a subtype of Issue in GitHub's data model). <see cref="Issue.Type"/> is set to
    /// "Issue" or "PR" based on the <c>__typename</c> of each node so users can filter
    /// at digest time if they want only one kind.
    /// </para>
    /// <para>
    /// Rate limits: classic PATs get 5000 GraphQL requests/hour, fine-grained PATs the
    /// same. We make one request per rule, so a sane number of rules stays well under
    /// the limit.
    /// </para>
    /// </remarks>
    public class GithubIssueSource
    {
        // Inline fragments duplicate the field list, but the two object types
        // (Issue and PullRequest) don't share a common interface that exposes
        // them all, so duplication is the simplest readable option.
        private const string SearchQuery = @"
query($q: String!) {
  search(query: $q, type: ISSUE, first: 100) {
    nodes {
      __typename
      ... on Issue {
        id number title url state createdAt updatedAt closedAt
        author { login ... on User { email } }
        assignees(first: 5) { nodes { login email name } }
        labels(first: 20) { nodes { name } }
        repository { nameWithOwner }
        milestone { title }
      }
      ... on PullRequest {
        id number title url state createdAt updatedAt closedAt
        author { login ... on User { email } }
        assignees(first: 5) { nodes { login email name } }
        labels(first: 20) { nodes { name } }
        repository { nameWithOwner }
        milestone { title }
      }
    }
  }
}";

        private readonly IGithubGateway _gateway;
        private readonly ILogger? _logger;

        public GithubIssueSource(IGithubGateway gateway, ILogger? logger = null)
        {
            _gateway = gateway;
            _logger = logger;
        }

        public GithubIssueSource(string token, HttpClient? httpClient = null, ILogger? logger = null)
            : this(new GithubConnection(token, httpClient), logger)
        {
        }

        public virtual Issue[] GetIssues(GithubRule rule)
        {
            if (string.IsNullOrWhiteSpace(rule.Filter))
            {
                _logger?.Warning("GitHub rule has no filter set; skipping");
                return Array.Empty<Issue>();
            }

            JObject response;
            try
            {
                response = _gateway.Query(SearchQuery, new { q = rule.Filter });
            }
            catch (Exception ex)
            {
                _logger?.Warning(ex, "Failed to fetch issues from GitHub for filter '{Filter}'", rule.Filter);
                return Array.Empty<Issue>();
            }

            var errors = response["errors"] as JArray;
            if (errors != null && errors.Count > 0)
            {
                _logger?.Error("GitHub GraphQL returned errors for filter '{Filter}': {Errors}",
                    rule.Filter, errors.ToString(Newtonsoft.Json.Formatting.None));
                return Array.Empty<Issue>();
            }

            var nodes = response.SelectToken("data.search.nodes") as JArray;
            if (nodes == null) return Array.Empty<Issue>();
            return nodes.OfType<JObject>().Select(MapNode).ToArray();
        }

        internal static Issue MapNode(JObject node)
        {
            var typename = (string?)node["__typename"];
            var isPr = string.Equals(typename, "PullRequest", StringComparison.Ordinal);
            var state = (string?)node["state"];

            var nameWithOwner = (string?)node.SelectToken("repository.nameWithOwner") ?? string.Empty;
            var number = (int?)node["number"];

            return new Issue
            {
                Key = number == null ? nameWithOwner : $"{nameWithOwner}#{number}",
                GithubNodeId = (string?)node["id"],
                Summary = (string?)node["title"] ?? string.Empty,
                Url = (string?)node["url"],
                Status = NormalizeState(state),
                Type = isPr ? "PR" : "Issue",
                Participants = new IssueParticipants
                {
                    Assignee = ToUser(node.SelectToken("assignees.nodes[0]") as JObject),
                    // GitHub has no separate reporter — use author for both, mirroring Linear.
                    Reporter = ToUser(node["author"] as JObject),
                    Creator = ToUser(node["author"] as JObject)
                },
                ProjectKey = (string?)node.SelectToken("milestone.title"),
                Labels = string.Join(", ", LabelNames(node)),
                CreatedDate = ParseNullableDate(node["createdAt"]) ?? DateTime.MinValue,
                UpdatedDate = ParseNullableDate(node["updatedAt"]),
                Resolution = string.Equals(state, "CLOSED", StringComparison.OrdinalIgnoreCase)
                    ? "Closed" : null
            };
        }

        private static string? NormalizeState(string? state)
        {
            if (string.IsNullOrEmpty(state)) return null;
            // GitHub returns OPEN/CLOSED for Issue, OPEN/CLOSED/MERGED for PR — title-case
            // for display so it sits naturally in chip pills alongside Jira's "In Progress".
            return state.Length == 0 ? state
                : char.ToUpperInvariant(state[0]) + state.Substring(1).ToLowerInvariant();
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
            var login = (string?)user["login"];
            var email = (string?)user["email"];
            var displayName = (string?)user["name"] ?? login;
            return new User
            {
                DisplayName = displayName,
                Name = displayName,
                // GitHub returns an empty string when the user has hidden their email.
                // Treat that as "no email" so the marker-resolution path can fall back
                // (or skip routing for this issue) instead of producing an invalid To: "".
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
