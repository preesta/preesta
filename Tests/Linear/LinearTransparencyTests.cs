using System.Linq;
using LinearGraphQL;
using NSubstitute;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Preesta;
using Preesta.Configuration.Action;
using Preesta.Data;
using Preesta.Data.Supplying;
using Preesta.Formatting;
using Serilog;
using Tests.Mocks;

namespace Tests.Linear
{
    /// <summary>
    /// Phase 12.2: section-header transparency for Linear.
    ///
    /// Each Linear filter mode (AI prompt, raw GraphQL filter, saved view) should
    /// produce a different "what produced this list" line in the digest header.
    /// Only the saved-view mode gets a clickable "Open in Linear →" link — Linear's
    /// UI doesn't encode filter state in URLs, so the AI-prompt and raw-filter
    /// modes have no canonical permalink, and we deliberately don't fall back to
    /// "My Issues" or anything that doesn't reproduce the rule's filter exactly.
    /// </summary>
    [TestFixture]
    public class LinearTransparencyTests
    {
        private const string FakeApiKey = "lin_api_FAKE_TEST_KEY";

        private const string OneIssueByFilterResponse = @"{
  ""data"": {
    ""issues"": {
      ""nodes"": [
        {
          ""identifier"": ""PRE-1"",
          ""title"": ""Set up CI"",
          ""url"": ""https://linear.app/preesta-dev/issue/PRE-1"",
          ""state"": { ""name"": ""In Progress"", ""type"": ""started"" },
          ""priority"": 1,
          ""priorityLabel"": ""Urgent"",
          ""assignee"": { ""id"": ""u1"", ""name"": ""Valentin"", ""email"": ""v@example.com"" },
          ""creator"":  { ""id"": ""u1"", ""name"": ""Valentin"", ""email"": ""v@example.com"" },
          ""project"":  null,
          ""labels"":   { ""nodes"": [] },
          ""dueDate"":   null,
          ""createdAt"": ""2026-05-01T10:00:00.000Z"",
          ""updatedAt"": ""2026-05-08T12:00:00.000Z""
        }
      ]
    }
  }
}";

        private const string CustomViewOneIssueResponse = @"{
  ""data"": {
    ""customView"": {
      ""name"": ""My Sprint Blockers"",
      ""issues"": {
        ""nodes"": [
          {
            ""identifier"": ""PRE-10"",
            ""title"": ""View issue A"",
            ""url"": ""https://linear.app/preesta-dev/issue/PRE-10"",
            ""state"": { ""name"": ""Todo"", ""type"": ""unstarted"" },
            ""priority"": 2,
            ""priorityLabel"": ""High"",
            ""assignee"": null,
            ""creator"":  { ""id"": ""u1"", ""name"": ""Valentin"", ""email"": ""v@example.com"" },
            ""project"":  null,
            ""labels"":   { ""nodes"": [] },
            ""dueDate"":   null,
            ""createdAt"": ""2026-05-01T10:00:00.000Z"",
            ""updatedAt"": ""2026-05-08T12:00:00.000Z""
          }
        ]
      }
    }
  }
}";

        private const string FilterSuggestionResponse =
            @"{ ""data"": { ""issueFilterSuggestion"": { ""filter"": { ""state"": { ""type"": { ""neq"": ""completed"" } } } } } }";

        private static NotificationSpec MinimalNotify() => new()
        {
            Subject = "T",
            RawRecipients = new[] { "a@x" },
            RawCc = new string[] { },
            Followup = "Take a look",
        };

        private static (LinearIssueSource source, MockLinearServer server) AiPromptStubbed()
        {
            var server = new MockLinearServer();
            server.StubIssuesQuery(OneIssueByFilterResponse);
            server.StubFilterSuggestionQuery("not completed", JObject.Parse(@"{ ""state"": { ""type"": { ""neq"": ""completed"" } } }"));
            var connection = new LinearConnection(FakeApiKey, server.GraphQlUrl);
            return (new LinearIssueSource(connection), server);
        }

        private static (LinearIssueSource source, MockLinearServer server) RawFilterStubbed()
        {
            var server = new MockLinearServer();
            server.StubIssuesQuery(OneIssueByFilterResponse);
            var connection = new LinearConnection(FakeApiKey, server.GraphQlUrl);
            return (new LinearIssueSource(connection), server);
        }

        private static (LinearIssueSource source, MockLinearServer server) ViewStubbed(string viewId)
        {
            var server = new MockLinearServer();
            server.StubCustomViewQuery(viewId, CustomViewOneIssueResponse);
            var connection = new LinearConnection(FakeApiKey, server.GraphQlUrl);
            return (new LinearIssueSource(connection), server);
        }

        private static Package<NotificationReaction, Issue>[] PackagesFor(LinearRule rule, LinearIssueSource source)
        {
            rule.Notification = MinimalNotify();
            var supplier = new LinearIssueSupplier(source, Substitute.For<IJiraService>(), new[] { rule }, Substitute.For<ILogger>());
            return supplier.GetPackages().Cast<Package<NotificationReaction, Issue>>().ToArray();
        }

        // ----- AI-prompt mode -----

        [Test]
        public void AiPromptMode_FilterDescriptionStartsWithAiFilter_AndContainsThePrompt()
        {
            var (source, server) = AiPromptStubbed();
            using (server)
            {
                const string prompt = "issues assigned to me, not completed";
                var packages = PackagesFor(new LinearRule { Filter = prompt }, source);
                Assert.AreEqual(1, packages.Length, "Expected exactly one notification package");

                var html = IssueFormatter.ToHtml(packages, "https://linear.app/preesta-dev/", linearWorkspace: "preesta-dev");

                StringAssert.Contains("AI filter:", html);
                StringAssert.Contains(prompt, html);
                // No saved-view link — AI prompt has no canonical URL.
                StringAssert.DoesNotContain("Open in Linear", html);
                StringAssert.DoesNotContain("/view/", html);
            }
        }

        [Test]
        public void AiPromptMode_DoesNotEmitJiraJqlLink()
        {
            var (source, server) = AiPromptStubbed();
            using (server)
            {
                var packages = PackagesFor(new LinearRule { Filter = "anything" }, source);
                var html = IssueFormatter.ToHtml(packages, "https://linear.app/preesta-dev/", linearWorkspace: "preesta-dev");
                StringAssert.DoesNotContain("Open in Jira", html);
            }
        }

        // ----- Raw filter mode -----

        [Test]
        public void RawFilterMode_FilterDescriptionIsHidden()
        {
            // filterRaw is a power-user escape hatch — the user wrote the JSON
            // themselves in rules.yaml, so re-displaying it in the digest header
            // adds non-actionable clutter (Linear's UI doesn't accept these via
            // URL — verified live). AI prompt and viewId still render.
            var (source, server) = RawFilterStubbed();
            using (server)
            {
                var rawFilter = JObject.Parse(@"{ ""state"": { ""type"": { ""neq"": ""completed"" } } }");
                var packages = PackagesFor(new LinearRule { FilterRaw = rawFilter }, source);

                var html = IssueFormatter.ToHtml(packages, "https://linear.app/preesta-dev/", linearWorkspace: "preesta-dev");

                StringAssert.DoesNotContain("Filter:", html);
                StringAssert.DoesNotContain("&quot;state&quot;", html);
                StringAssert.DoesNotContain("&quot;completed&quot;", html);
                // No view link either — filterRaw has no canonical Linear URL.
                StringAssert.DoesNotContain("Open in Linear", html);
            }
        }

        // ----- ViewId mode -----

        [Test]
        public void ViewIdMode_FilterDescriptionStartsWithView_AndContainsViewName()
        {
            const string viewId = "0e8a3b41-1234-4321-aaaa-bbbbbbbbbbbb";
            var (source, server) = ViewStubbed(viewId);
            using (server)
            {
                var packages = PackagesFor(new LinearRule { ViewId = viewId }, source);

                var html = IssueFormatter.ToHtml(packages, "https://linear.app/preesta-dev/", linearWorkspace: "preesta-dev");

                StringAssert.Contains("View: My Sprint Blockers", html);
                // viewId mode is the only mode that gets a working "Open in Linear" link.
                StringAssert.Contains("Open in Linear", html);
                StringAssert.Contains($"https://linear.app/preesta-dev/view/{viewId}", html);
            }
        }

        [Test]
        public void ViewIdMode_WithoutWorkspace_ShowsDescriptionButNoLink()
        {
            // Linear:workspace not set → we know the view name from GraphQL but cannot
            // construct a permalink, so suppress the link rather than fall back.
            const string viewId = "0e8a3b41-1234-4321-aaaa-bbbbbbbbbbbb";
            var (source, server) = ViewStubbed(viewId);
            using (server)
            {
                var packages = PackagesFor(new LinearRule { ViewId = viewId }, source);

                var html = IssueFormatter.ToHtml(packages, "https://linear.app/", linearWorkspace: null);

                StringAssert.Contains("View: My Sprint Blockers", html);
                StringAssert.DoesNotContain("Open in Linear", html);
                StringAssert.DoesNotContain("/view/", html);
            }
        }

        [Test]
        public void ViewIdMode_FallsBackToIdWhenViewNameLookupFailed()
        {
            // Edge case: GraphQL request errored or returned no `name` — the supplier
            // should still surface the rule's viewId in the description rather than
            // disappear silently.
            const string viewId = "broken-view-id";
            var server = new MockLinearServer();
            // Stub returns a customView with no `name` field.
            server.StubCustomViewQuery(viewId, @"{ ""data"": { ""customView"": { ""issues"": { ""nodes"": [] } } } }");
            using (server)
            {
                var connection = new LinearConnection(FakeApiKey, server.GraphQlUrl);
                var source = new LinearIssueSource(connection);

                var rule = new LinearRule { ViewId = viewId, Notification = MinimalNotify() };
                var supplier = new LinearIssueSupplier(source, Substitute.For<IJiraService>(), new[] { rule }, Substitute.For<ILogger>());
                var packages = supplier.GetPackages().Cast<Package<NotificationReaction, Issue>>().ToArray();
                // Empty result → no notification package; nothing to assert on output.
                // We instead poke the package directly to verify Enrich's fallback.
                var basePackage = new Package<NotificationReaction, Issue> { Reaction = new NotificationReaction { Subject = "T" } };
                supplier.GetType()
                    .GetMethod("Enrich", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(supplier, new object[] { basePackage, rule });

                Assert.AreEqual(viewId, basePackage.Properties["LinearViewName"], "Should fall back to the id when name lookup yielded nothing");
            }
        }

        // ----- Negative: Jira section is unaffected -----

        [Test]
        public void JiraSection_HasNoFilterDescription_AndNoLinearLink()
        {
            // Build a Jira-flavoured package and render with linearWorkspace=null —
            // the Phase-12.2 fields must not leak into Jira output.
            var pkg = new Package<NotificationReaction, Issue>
            {
                Items = new[] { new Issue { Key = "T-1", Summary = "x" } },
                Reaction = new NotificationReaction { Subject = "T", Followup = "look" },
                Properties = { { "Jql", "project = T" } }
            };
            var html = IssueFormatter.ToHtml(new[] { pkg }, "https://jira.example.com/");
            StringAssert.Contains("Open in Jira", html);
            StringAssert.DoesNotContain("AI filter:", html);
            StringAssert.DoesNotContain("Filter:", html);
            StringAssert.DoesNotContain("View:", html);
            StringAssert.DoesNotContain("Open in Linear", html);
        }
    }
}
