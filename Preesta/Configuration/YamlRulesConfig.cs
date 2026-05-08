using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Preesta.Configuration.Action;
using Serilog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Preesta.Configuration
{
    internal class YamlRulesConfig : IRulesConfig
    {
        private readonly YamlConfigModel _config;
        private readonly ILogger _logger;

        public static YamlRulesConfig FromFile(string path, ILogger logger)
            => new(File.ReadAllText(path), logger);

        public YamlRulesConfig(string yaml, ILogger logger)
        {
            _logger = logger;
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
            _config = deserializer.Deserialize<YamlConfigModel>(yaml) ?? new YamlConfigModel();
        }

        public void ValidateSchema()
        {
            if (_config.Rules == null)
            {
                _logger.Warning("YAML rules file contains no rules");
                return;
            }

            foreach (var rule in _config.Rules)
            {
                if (string.IsNullOrEmpty(rule.Type))
                    _logger.Warning("Rule is missing 'type' field");
                if (string.IsNullOrEmpty(rule.Group))
                    _logger.Warning("Rule of type {Type} is missing 'group' field", rule.Type);
            }
        }

        public JqlRule[] GetJqlRules(string @group)
        {
            return GetRules<JqlRule>(@group, "jql", entry =>
            {
                var rule = ToBaseRule<JqlRule>(entry);
                rule.Jql = entry.Jql ?? string.Empty;
                return rule;
            });
        }

        public ReleaseRule[] GetReleaseRules(string @group)
        {
            return GetRules<ReleaseRule>(@group, "build", entry =>
            {
                var rule = ToBaseRule<ReleaseRule>(entry);
                rule.Mask = entry.Mask ?? string.Empty;
                rule.RemainingDays = entry.RemainingDays ?? 0;
                rule.ExpiredOnly = entry.ExpiredOnly ?? false;
                rule.ProjectCode = entry.ProjectCode ?? string.Empty;
                return rule;
            });
        }

        public Action.LinearRule[] GetLinearRules(string @group)
        {
            return GetRules<Action.LinearRule>(@group, "linear", entry =>
            {
                var rule = ToBaseRule<Action.LinearRule>(entry);
                rule.Filter = string.IsNullOrWhiteSpace(entry.Filter) ? null : entry.Filter;
                rule.FilterRaw = ConvertFilterRaw(entry.FilterRaw);
                rule.ViewId = string.IsNullOrWhiteSpace(entry.ViewId) ? null : entry.ViewId;

                // For linear rules `mutations:` is GraphQL, not REST — discard the
                // REST array that ToBaseRule eagerly populated and re-read the same
                // entries as GraphQL specs.
                rule.Mutations = Array.Empty<RestMutationSpec>();
                if (entry.Mutations != null)
                {
                    rule.GraphQLMutations = entry.Mutations
                        .Where(m => !string.IsNullOrEmpty(m.Mutation))
                        .Select(m => new Action.GraphQLMutationSpec { MutationBody = m.Mutation! })
                        .ToArray();
                }

                if (!ValidateLinearFilterModes(rule))
                    return null!;

                return rule;
            });
        }

        /// <summary>
        /// Enforces "exactly one of {filter, filterRaw, viewId}" on a Linear rule.
        /// Returns false (and logs) when zero or 2+ are set; the converter then drops the rule.
        /// </summary>
        private bool ValidateLinearFilterModes(Action.LinearRule rule)
        {
            var which = new List<string>();
            if (rule.Filter != null) which.Add("filter");
            if (rule.FilterRaw != null) which.Add("filterRaw");
            if (rule.ViewId != null) which.Add("viewId");

            if (which.Count == 0)
            {
                _logger.Error("Linear rule must specify one of: filter (AI prompt), filterRaw (GraphQL filter), or viewId (saved view ID)");
                return false;
            }

            if (which.Count > 1)
            {
                _logger.Error("Linear rule has multiple filter sources set ({Sources}); pick exactly one",
                    string.Join(", ", which));
                return false;
            }

            return true;
        }

        /// <summary>
        /// YamlDotNet deserialises nested mappings into <c>Dictionary&lt;object, object&gt;</c>.
        /// Round-trip via JSON to obtain a Newtonsoft <see cref="JObject"/> that we can
        /// pass straight into the GraphQL <c>variables</c> envelope.
        /// </summary>
        private static JObject? ConvertFilterRaw(object? raw)
        {
            if (raw == null) return null;
            var json = JsonConvert.SerializeObject(raw);
            var token = JsonConvert.DeserializeObject<JToken>(json);
            return token as JObject;
        }

        public IReadOnlyDictionary<string, string> GetRedirectionMap()
        {
            return new ReadOnlyDictionary<string, string>(
                _config.RedirectionRules ?? new Dictionary<string, string>());
        }

        public IReadOnlyDictionary<string, string> GetTelegramUserMap()
        {
            return new ReadOnlyDictionary<string, string>(
                _config.TelegramUsers ?? new Dictionary<string, string>());
        }

        private TRule[] GetRules<TRule>(string group, string type, Func<YamlRuleEntry, TRule> converter) where TRule : Rule
        {
            if (_config.Rules == null)
                return Array.Empty<TRule>();

            var foundRules = _config.Rules
                .Where(e => string.Equals(e.Type, type, StringComparison.OrdinalIgnoreCase))
                .Where(e => e.Active != false)
                .Where(e => string.IsNullOrEmpty(group) || string.Equals(e.Group, group, StringComparison.Ordinal))
                .Select(e => TryConvert(e, converter))
                .Where(r => r != null)
                .ToArray()!;

            _logger?.Information("{Count} rules of type {Type} found in schedule group '{Group}'", foundRules.Length, type, group);
            return foundRules!;
        }

        private TRule? TryConvert<TRule>(YamlRuleEntry entry, Func<YamlRuleEntry, TRule> converter) where TRule : Rule
        {
            try
            {
                return converter(entry);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Rule conversion failed");
                return null;
            }
        }

        private static TRule ToBaseRule<TRule>(YamlRuleEntry entry) where TRule : Rule, new()
        {
            var rule = new TRule
            {
                AdditionalPredicateName = entry.AdditionalPredicate
            };

            if (entry.Notify != null)
            {
                rule.Notification = new NotificationSpec
                {
                    Subject = entry.Notify.Subject ?? string.Empty,
                    Recommendations = entry.Notify.Recommendations,
                    RawRecipients = (entry.Notify.MailTo ?? string.Empty).ToLower()
                        .Split(',', StringSplitOptions.RemoveEmptyEntries),
                    RawCc = (entry.Notify.Cc ?? string.Empty).ToLower()
                        .Split(',', StringSplitOptions.RemoveEmptyEntries),
                    TelegramChatIds = (entry.Notify.TelegramChatId ?? string.Empty)
                        .Split(',', StringSplitOptions.RemoveEmptyEntries),
                    Columns = entry.Notify.Columns?.ToArray()
                };
            }

            if (entry.Mutations != null)
            {
                rule.Mutations = entry.Mutations.Select(cr => new RestMutationSpec
                {
                    Verb = cr.Verb ?? string.Empty,
                    UrlPattern = cr.UrlPattern ?? string.Empty,
                    BodyPattern = cr.Body
                }).ToArray();
            }

            return rule;
        }
    }

    internal class YamlConfigModel
    {
        public List<YamlRuleEntry>? Rules { get; set; }
        public Dictionary<string, string>? RedirectionRules { get; set; }
        public Dictionary<string, string>? TelegramUsers { get; set; }
    }

    internal class YamlRuleEntry
    {
        public string? Type { get; set; }
        public string? Group { get; set; }
        public bool? Active { get; set; }
        public string? AdditionalPredicate { get; set; }

        // JQL
        public string? Jql { get; set; }

        // Release
        public string? Mask { get; set; }
        public int? RemainingDays { get; set; }
        public bool? ExpiredOnly { get; set; }
        public string? ProjectCode { get; set; }

        // Linear — three mutually exclusive filter modes (validated in GetLinearRules):
        //   filter:    string — AI prompt (primary, user-facing)
        //   filterRaw: nested mapping — raw Linear GraphQL filter object (escape hatch)
        //   viewId:    string — Linear saved-view ID (escape hatch)
        public string? Filter { get; set; }
        public object? FilterRaw { get; set; }
        public string? ViewId { get; set; }

        // Actions
        public YamlNotifyEntry? Notify { get; set; }
        public List<YamlMutationsEntry>? Mutations { get; set; }
    }

    internal class YamlNotifyEntry
    {
        public string? Subject { get; set; }
        public string? MailTo { get; set; }
        public string? Cc { get; set; }
        public string? Recommendations { get; set; }
        public string? TelegramChatId { get; set; }
        public List<string>? Columns { get; set; }
    }

    /// <summary>
    /// One entry in a rule's <c>mutations:</c> list. Shape differs by rule type:
    /// <list type="bullet">
    /// <item><description><b>jql</b>: REST request — populates <see cref="Verb"/>, <see cref="UrlPattern"/>, <see cref="Body"/>.</description></item>
    /// <item><description><b>linear</b>: GraphQL — populates <see cref="Mutation"/>.</description></item>
    /// </list>
    /// All four fields are nullable; the relevant converter (in <see cref="YamlRulesConfig"/>) picks the ones it needs.
    /// </summary>
    internal class YamlMutationsEntry
    {
        // REST (jql rules)
        public string? Verb { get; set; }
        public string? UrlPattern { get; set; }
        public string? Body { get; set; }

        // GraphQL (linear rules)
        public string? Mutation { get; set; }
    }
}
