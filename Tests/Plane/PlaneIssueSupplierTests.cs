using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using PlaneRest;
using Preesta;
using Preesta.Configuration.Action;
using Preesta.Data;
using Preesta.Data.Supplying;
using Serilog;

namespace Tests.Plane
{
    /// <summary>
    /// Verifies the supplier-level Enrich step that puts PlaneProjectId / PlaneFilter
    /// on the package Properties so the formatter can render a human-readable
    /// "Plane filter: …" line in the digest header. Also exercises the base
    /// grouping path with a Plane-typed rule to confirm reuse works end-to-end.
    /// </summary>
    [TestFixture]
    public class PlaneIssueSupplierTests
    {
        private const string OneItemResponse = @"{
  ""results"": [
    {
      ""id"": ""wi-1"",
      ""name"": ""x"",
      ""sequence_id"": 1,
      ""priority"": ""medium"",
      ""state"": ""s-1"",
      ""state_detail"": { ""name"": ""Todo"" },
      ""assignees"": [],
      ""created_by"": ""u-1"",
      ""labels"": [],
      ""created_at"": ""2026-05-01T00:00:00Z"",
      ""project"": ""proj-1""
    }
  ]
}";

        private static (PlaneIssueSupplier supplier, PlaneRule rule) Make(
            IReadOnlyDictionary<string, string>? filter = null)
        {
            var gateway = Substitute.For<IPlaneGateway>();
            gateway.ListWorkItems(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>())
                .Returns(JObject.Parse(OneItemResponse));
            gateway.ListWorkspaceMembers().Returns(new JArray());
            var source = new PlaneIssueSource(gateway, Substitute.For<ILogger>());

            var rule = new PlaneRule
            {
                ProjectId = "proj-1",
                Filter = filter == null
                    ? new Dictionary<string, string>()
                    : new Dictionary<string, string>(filter),
                Notification = new NotificationSpec
                {
                    Subject = "Plane digest",
                    RawRecipients = new[] { "ops@example.com" },
                    RawCc = System.Array.Empty<string>()
                }
            };
            var supplier = new PlaneIssueSupplier(source,
                Substitute.For<IJiraService>(), new[] { rule }, Substitute.For<ILogger>(), "test-ws");
            return (supplier, rule);
        }

        [Test]
        public void Enrich_SetsPlaneProjectIdProperty()
        {
            var (supplier, _) = Make();
            var packages = supplier.GetPackages();

            var notif = packages.OfType<Package<NotificationReaction, Issue>>().Single();
            Assert.IsTrue(notif.Properties.ContainsKey("PlaneProjectId"));
            Assert.AreEqual("proj-1", notif.Properties["PlaneProjectId"]);
        }

        [Test]
        public void Enrich_RendersFilterAsSortedKVPairs()
        {
            // Sorted-by-key output makes the digest deterministic regardless of YAML
            // mapping order (Dictionary doesn't preserve insertion order across
            // .NET versions consistently for tests).
            var (supplier, _) = Make(new Dictionary<string, string>
            {
                { "search", "leak" },
                { "priority", "urgent" }
            });
            var packages = supplier.GetPackages();

            var notif = packages.OfType<Package<NotificationReaction, Issue>>().Single();
            Assert.AreEqual("priority=urgent, search=leak", notif.Properties["PlaneFilter"]);
        }

        [Test]
        public void Enrich_EmptyFilter_PlaneFilterPropertyAbsent()
        {
            var (supplier, _) = Make();
            var packages = supplier.GetPackages();

            var notif = packages.OfType<Package<NotificationReaction, Issue>>().Single();
            Assert.IsFalse(notif.Properties.ContainsKey("PlaneFilter"),
                "Empty filter should leave PlaneFilter unset so the formatter renders 'Plane project: ...' instead");
        }
    }
}
