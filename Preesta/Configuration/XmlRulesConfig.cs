using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Preesta.Configuration.Convert;
using Serilog;


namespace Preesta.Configuration
{
    internal class XmlRulesConfig : IRulesConfig
    {
        private readonly XDocument _config;
        private readonly ILogger _logger;
        public XmlRulesConfig(string path, ILogger logger)
            : this(XDocument.Load(path), logger)
        {
        }

        public XmlRulesConfig(XDocument config, ILogger logger)
        {
            _logger = logger;
            _config = config;
        }

        public void ValidateSchema()
        {
            var schemas = new XmlSchemaSet();
            var rulesXsdStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Preesta.rules.xsd");
            if (rulesXsdStream == null)
            {
                _logger.Error("rules.xsd resource is not found in Preesta assembly, cannot validate rules.xml file");
                return;
            }
            schemas.Add("", XmlReader.Create(rulesXsdStream));

            _config.Validate(schemas, (o, e) => _logger.Warning(e.Exception, "Rules file issue [{Severity}] {Message}", e.Severity, e.Message));
        }

        public JqlRule[] GetJqlRules(string @group)
        {
            return GetRules(@group, new[] {"request", "jqlRule"}, XRuleSource.ToJqlRule);
        }

        public ReleaseRule[] GetReleaseRules(string @group)
        {
            return GetRules(@group, new[] { "build" }, XRuleSource.ToReleaseRule);
        }

        // XML rules format is legacy; Linear rules are YAML-only.
        public Action.LinearRule[] GetLinearRules(string @group) => Array.Empty<Action.LinearRule>();

        public IReadOnlyDictionary<string, string> GetRedirectionMap()
        {
            return new ReadOnlyDictionary<string, string>(
                (_config.Root!.Element("redirection_rules") ?? new XElement("redirection_rules"))
                .Elements("rule")
                .ToDictionary(r => r.Attribute("from")!.Value, r => r.Attribute("to")!.Value))
                ;
        }

        public IReadOnlyDictionary<string, string> GetTelegramUserMap()
        {
            return new ReadOnlyDictionary<string, string>(
                (_config.Root!.Element("telegram_users") ?? new XElement("telegram_users"))
                .Elements("user")
                .ToDictionary(r => r.Attribute("email")!.Value, r => r.Attribute("chatId")!.Value));
        }

        private TRule[] GetRules<TRule>(string periodType, IEnumerable<string> rulesTypes, Func<XElement, TRule> converter) where TRule : Rule
        {
            var foundRules =
                    (
                        from e in _config.Root!.Elements()
                        where rulesTypes.Contains(e.Name.LocalName)
                        let a = e.Attribute("active")
                        let g = e.Attribute("group")
                        where (a == null || a.Value != "0")
                              && (string.IsNullOrEmpty(periodType) || g != null && g.Value == periodType)
                        let rule = TryConvert(e, converter)
                        where rule != default(TRule)
                        select rule
                   )
                   .ToArray();
            
            _logger?.Information("{Count} rules of type {@rulesTypes} found in schedule group '{PeriodType}'", foundRules.Count(), rulesTypes, periodType);
            _logger?.Verbose("Found rules: {@FoundRules}", foundRules);

            return foundRules;
        }

        private TRule? TryConvert<TRule>(XElement element, Func<XElement, TRule> converter) where TRule : Rule
        {
            var rule = default(TRule);
            try
            {
                rule = converter(element);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Rule conversion failed");
            }
            return rule;
        }
    }
}
