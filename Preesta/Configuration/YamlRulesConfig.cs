using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
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
                if (string.IsNullOrEmpty(rule.Tracker) && string.IsNullOrEmpty(rule.Type))
                    _logger.Warning("Rule is missing 'tracker' field");
                // `tags:` is optional. A rule without tags runs on every preesta
                // invocation that does not pass a tag filter, by lefthook-style
                // semantics — that's a feature, not a problem to warn about.
            }
        }

        public JqlRule[] GetJqlRules(IReadOnlyList<string> tags)
        {
            return GetTrackerRules<JqlRule>(tags, "jira", entry =>
            {
                var rule = ToBaseRule<JqlRule>(entry);
                // The Jira filter is a raw JQL string (Jira's own query language).
                // YAML may keep using the legacy `jql:` key for back-compat in this
                // pre-1.0 phase isn't a goal — `filter:` is the single canonical key.
                rule.Filter = entry.Filter as string ?? string.Empty;
                return rule;
            });
        }

        public ReleaseRule[] GetReleaseRules(IReadOnlyList<string> tags)
        {
            // Build rules keep the legacy `type: build` discriminator (no `tracker:`).
            // Releases aren't a tracker — they're a separate artefact inside Jira —
            // and the schema reflects that by living on the older key.
            return GetTypeRules<ReleaseRule>(tags, "build", entry =>
            {
                var rule = ToBaseRule<ReleaseRule>(entry);
                rule.Mask = entry.Mask ?? string.Empty;
                rule.RemainingDays = entry.RemainingDays ?? 0;
                rule.ExpiredOnly = entry.ExpiredOnly ?? false;
                rule.ProjectCode = entry.ProjectCode ?? string.Empty;
                return rule;
            });
        }

        public Action.LinearRule[] GetLinearRules(IReadOnlyList<string> tags)
        {
            return GetTrackerRules<Action.LinearRule>(tags, "linear", entry =>
            {
                var rule = ToBaseRule<Action.LinearRule>(entry);
                // Linear's `filter:` is an AI-prompt string. We accept only string-typed
                // YAML scalars here; a mapping would be `filterRaw:` instead.
                var filterString = entry.Filter as string;
                rule.Filter = string.IsNullOrWhiteSpace(filterString) ? null : filterString;
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

        public Action.GitlabRule[] GetGitlabRules(IReadOnlyList<string> tags)
        {
            return GetTrackerRules<Action.GitlabRule>(tags, "gitlab", entry =>
            {
                var rule = ToBaseRule<Action.GitlabRule>(entry);

                rule.Filter = ConvertGitlabFilter(entry.Filter);

                // GitLab rules use GraphQL mutations, not REST — drop the REST array
                // that ToBaseRule eagerly populates and re-read the same entries as
                // GraphQL specs, exactly like Linear / GitHub do.
                rule.Mutations = Array.Empty<Action.RestMutationSpec>();
                if (entry.Mutations != null)
                {
                    rule.GraphQLMutations = entry.Mutations
                        .Where(m => !string.IsNullOrEmpty(m.Mutation))
                        .Select(m => new Action.GraphQLMutationSpec { MutationBody = m.Mutation! })
                        .ToArray();
                }

                return rule;
            });
        }

        /// <summary>
        /// Maps the YAML <c>filter:</c> sub-mapping onto <see cref="Action.GitlabFilter"/>.
        /// Each chip from GitLab's web UI is a named property — fields not present in
        /// YAML stay null and are omitted from the GraphQL request. The conversion is
        /// pure mapping (no DSL): the YAML keys are identical to the field names of
        /// GraphQL's <c>Query.issues</c> arguments.
        /// </summary>
        private static Action.GitlabFilter ConvertGitlabFilter(object? raw)
        {
            var f = new Action.GitlabFilter();
            if (raw is not IDictionary<object, object> map)
                return f;

            string? GetString(string key) =>
                map.TryGetValue(key, out var v) && v != null && !string.IsNullOrWhiteSpace(v.ToString())
                    ? v.ToString()!.Trim()
                    : null;

            bool? GetBool(string key) =>
                map.TryGetValue(key, out var v) && v != null
                    && bool.TryParse(v.ToString(), out var b)
                    ? b
                    : (bool?)null;

            string[]? GetStringArray(string key)
            {
                if (!map.TryGetValue(key, out var v) || v == null) return null;
                // YAML "labels: [a, b]" → IList<object>; YAML "labels: a" → scalar string.
                if (v is IList<object> list)
                    return list.Select(x => x?.ToString() ?? string.Empty)
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Select(s => s.Trim())
                        .ToArray();
                var single = v.ToString();
                return string.IsNullOrWhiteSpace(single) ? null : new[] { single!.Trim() };
            }

            f.State = GetString("state");
            f.LabelName = GetStringArray("labelName");
            f.AssigneeUsernames = GetStringArray("assigneeUsernames");
            f.AuthorUsername = GetString("authorUsername");
            f.MilestoneTitle = GetStringArray("milestoneTitle");
            f.Search = GetString("search");
            f.CreatedAfter = GetString("createdAfter");
            f.CreatedBefore = GetString("createdBefore");
            f.UpdatedAfter = GetString("updatedAfter");
            f.UpdatedBefore = GetString("updatedBefore");
            f.Confidential = GetBool("confidential");
            f.Iids = GetStringArray("iids");
            return f;
        }

        public Action.GithubRule[] GetGithubRules(IReadOnlyList<string> tags)
        {
            return GetTrackerRules<Action.GithubRule>(tags, "github", entry =>
            {
                var rule = ToBaseRule<Action.GithubRule>(entry);
                // GitHub's `filter:` is a raw search-string scalar. A mapping there would
                // be a config mistake; we coerce non-string to null and let the empty-filter
                // branch below log+drop the rule.
                var filterString = entry.Filter as string;
                rule.Filter = string.IsNullOrWhiteSpace(filterString) ? null : filterString.Trim();

                // GitHub rules use GraphQL mutations, not REST — drop the REST array
                // that ToBaseRule eagerly populates and read the same entries again
                // through the GraphQL field.
                rule.Mutations = Array.Empty<RestMutationSpec>();
                if (entry.Mutations != null)
                {
                    rule.GraphQLMutations = entry.Mutations
                        .Where(m => !string.IsNullOrEmpty(m.Mutation))
                        .Select(m => new Action.GraphQLMutationSpec { MutationBody = m.Mutation! })
                        .ToArray();
                }

                if (rule.Filter == null)
                {
                    _logger.Error("GitHub rule must specify a non-empty 'filter' (raw GitHub search string)");
                    return null!;
                }

                return rule;
            });
        }

        public Action.ShortcutRule[] GetShortcutRules(IReadOnlyList<string> tags)
        {
            return GetTrackerRules<Action.ShortcutRule>(tags, "shortcut", entry =>
            {
                var rule = ToBaseRule<Action.ShortcutRule>(entry);
                // Shortcut's `filter:` is a raw search-string scalar. Coerce non-string
                // to null so the empty-filter branch below logs+drops the rule cleanly.
                var filterString = entry.Filter as string;
                rule.Filter = string.IsNullOrWhiteSpace(filterString) ? null : filterString.Trim();

                // Shortcut rules use REST mutations (same shape as Jira), so the
                // RestMutationSpec array eagerly populated by ToBaseRule is exactly
                // what we want — no GraphQL field to re-read.

                if (rule.Filter == null)
                {
                    _logger.Error("Shortcut rule must specify a non-empty 'filter' (raw Shortcut search string)");
                    return null!;
                }

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
        /// YamlDotNet deserialises nested mappings into <c>Dictionary&lt;object, object&gt;</c>
        /// with ALL scalars as <c>string</c> (CoreSchema tag inference doesn't apply when
        /// the target is <c>object</c> in this code path). Walk the tree, recover scalar
        /// types via TryParse so Linear's GraphQL doesn't reject e.g. <c>{ "gte": "2" }</c>
        /// where it expects <c>{ "gte": 2 }</c>.
        /// </summary>
        private static JObject? ConvertFilterRaw(object? raw) => ToJson(raw) as JObject;

        private static JToken? ToJson(object? raw) => raw switch
        {
            null => null,
            IDictionary<object, object> map => new JObject(map.Select(kv =>
                new JProperty(kv.Key.ToString()!, ToJson(kv.Value) ?? JValue.CreateNull()))),
            IList<object> list => new JArray(list.Select(v => ToJson(v) ?? JValue.CreateNull())),
            string s when long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) => new JValue(i),
            string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => new JValue(d),
            string s when bool.TryParse(s, out var b) => new JValue(b),
            string s => new JValue(s),
            _ => new JValue(raw.ToString())
        };

        public IReadOnlyDictionary<string, string> GetMailAliasMap()
        {
            return new ReadOnlyDictionary<string, string>(
                _config.MailAliases ?? new Dictionary<string, string>());
        }

        public IReadOnlyDictionary<string, string> GetTelegramUserMap()
        {
            return new ReadOnlyDictionary<string, string>(
                _config.TelegramUsers ?? new Dictionary<string, string>());
        }

        public IReadOnlyDictionary<string, string> GetSlackUserMap()
        {
            return new ReadOnlyDictionary<string, string>(
                _config.SlackUsers ?? new Dictionary<string, string>());
        }

        // Tracker rules use the new `tracker:` YAML key (jira / linear / github /
        // gitlab / shortcut). The build rule still uses the legacy `type: build`
        // key — see GetTypeRules below. Two paths, no fallback between them: each
        // YAML entry uses exactly one of the two discriminator keys.
        private TRule[] GetTrackerRules<TRule>(IReadOnlyList<string> tags, string tracker, Func<YamlRuleEntry, TRule> converter) where TRule : Rule
            => MatchRules(tags, tracker, e => e.Tracker, "tracker", converter);

        private TRule[] GetTypeRules<TRule>(IReadOnlyList<string> tags, string type, Func<YamlRuleEntry, TRule> converter) where TRule : Rule
            => MatchRules(tags, type, e => e.Type, "type", converter);

        private TRule[] MatchRules<TRule>(
            IReadOnlyList<string> tags,
            string expected,
            Func<YamlRuleEntry, string?> discriminator,
            string discriminatorName,
            Func<YamlRuleEntry, TRule> converter) where TRule : Rule
        {
            if (_config.Rules == null)
                return Array.Empty<TRule>();

            var foundRules = _config.Rules
                .Where(e => string.Equals(discriminator(e), expected, StringComparison.OrdinalIgnoreCase))
                .Where(e => e.Active != false)
                .Where(e => MatchesTags(e, tags))
                .Select(e => TryConvert(e, converter))
                .Where(r => r != null)
                .ToArray()!;

            _logger?.Information("{Count} rules with {Discriminator}={Value} found for tags [{Tags}]",
                foundRules.Length, discriminatorName, expected, string.Join(", ", tags));
            return foundRules!;
        }

        /// <summary>
        /// Lefthook-style positive tag selector. Empty <paramref name="requested"/>
        /// means "no tag filter" — every rule matches, including ones with no
        /// tags. A non-empty filter requires the rule to carry at least one of
        /// the requested tags; untagged rules drop out.
        /// </summary>
        private static bool MatchesTags(YamlRuleEntry entry, IReadOnlyList<string> requested)
        {
            if (requested.Count == 0) return true;
            var ruleTags = NormalizeTags(entry.Tags);
            if (ruleTags.Length == 0) return false;
            return ruleTags.Any(t => requested.Contains(t, StringComparer.Ordinal));
        }

        /// <summary>
        /// YAML <c>tags:</c> may be a scalar (single tag), a comma-separated
        /// scalar (lefthook quirk), or a list. Normalise to a string[]; missing
        /// or empty input returns an empty array.
        /// </summary>
        internal static string[] NormalizeTags(object? raw) => raw switch
        {
            null => Array.Empty<string>(),
            string s => s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            IList<object> list => list
                .Select(v => v?.ToString()?.Trim() ?? string.Empty)
                .Where(s => !string.IsNullOrEmpty(s))
                .ToArray(),
            _ => Array.Empty<string>()
        };

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
                    Followup = entry.Notify.Followup,
                    RawRecipients = (entry.Notify.MailTo ?? string.Empty).ToLower()
                        .Split(',', StringSplitOptions.RemoveEmptyEntries),
                    RawCc = (entry.Notify.Cc ?? string.Empty).ToLower()
                        .Split(',', StringSplitOptions.RemoveEmptyEntries),
                    TelegramChatIds = (entry.Notify.TelegramChatId ?? string.Empty)
                        .Split(',', StringSplitOptions.RemoveEmptyEntries),
                    SlackUserIds = (entry.Notify.SlackUserId ?? string.Empty)
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
        public Dictionary<string, string>? MailAliases { get; set; }
        public Dictionary<string, string>? TelegramUsers { get; set; }
        public Dictionary<string, string>? SlackUsers { get; set; }
    }

    internal class YamlRuleEntry
    {
        // Discriminator: tracker rules use Tracker (jira/linear/github/gitlab/shortcut);
        // build rules (Jira releases) keep the legacy Type key with value "build".
        // Exactly one of these is expected per entry.
        public string? Tracker { get; set; }
        public string? Type { get; set; }

        // `tags:` accepts a scalar (single tag), a comma-separated scalar, or a
        // list of strings. Normalisation lives in YamlRulesConfig.NormalizeTags.
        // Rules with no tags run only when preesta is invoked without a tag
        // filter; tagged rules require an intersect with the CLI args.
        public object? Tags { get; set; }
        public bool? Active { get; set; }
        public string? AdditionalPredicate { get; set; }

        // Release (build rules)
        public string? Mask { get; set; }
        public int? RemainingDays { get; set; }
        public bool? ExpiredOnly { get; set; }
        public string? ProjectCode { get; set; }

        // `filter:` shape depends on tracker:
        //   jira:     string — raw JQL
        //   linear:   string — AI prompt
        //   github:   string — raw GitHub search query
        //   gitlab:   mapping — structured chips (state, labelName, …)
        //   shortcut: string — raw Shortcut search query
        // Type is `object?` and each tracker's converter casts/inspects it.
        // FilterRaw / ViewId are Linear-only escape hatches (kept as their own keys).
        public object? Filter { get; set; }
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
        public string? Followup { get; set; }
        public string? TelegramChatId { get; set; }
        public string? SlackUserId { get; set; }
        public List<string>? Columns { get; set; }
    }

    /// <summary>
    /// One entry in a rule's <c>mutations:</c> list. Shape differs by tracker:
    /// <list type="bullet">
    /// <item><description><b>jira / shortcut</b>: REST request — populates <see cref="Verb"/>, <see cref="UrlPattern"/>, <see cref="Body"/>.</description></item>
    /// <item><description><b>linear / github / gitlab</b>: GraphQL — populates <see cref="Mutation"/>.</description></item>
    /// </list>
    /// All four fields are nullable; the relevant converter (in <see cref="YamlRulesConfig"/>) picks the ones it needs.
    /// </summary>
    internal class YamlMutationsEntry
    {
        // REST (jira / shortcut rules)
        public string? Verb { get; set; }
        public string? UrlPattern { get; set; }
        public string? Body { get; set; }

        // GraphQL (linear / github / gitlab rules)
        public string? Mutation { get; set; }
    }
}
