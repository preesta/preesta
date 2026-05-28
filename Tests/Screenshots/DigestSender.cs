using System;
using System.Collections.Generic;
using System.IO;
using Messaging;
using NUnit.Framework;
using Preesta.Data;
using Preesta.Data.Supplying;
using Preesta.Formatting;
using YamlDotNet.RepresentationModel;

namespace Tests.Screenshots
{
    /// <summary>
    /// Sends the same four digest scenarios as <see cref="DigestScreenshotGen"/>
    /// to live Telegram and Slack so the docs author can capture real
    /// desktop-client screenshots. Reads bot tokens AND the docs-author's
    /// chat/user IDs from <c>Preesta/secrets/appsettings.secrets.yaml</c>:
    /// <code>
    ///   Telegram: { botToken: "...", docsTestChatId: "..." }
    ///   Slack:    { botToken: "...", docsTestUserId: "..." }
    /// </code>
    /// Both <c>docsTest*</c> keys are ignored by production Preesta — only
    /// this fixture reads them. If absent, the test self-ignores.
    /// Run:
    ///   dotnet test --filter "FullyQualifiedName~Screenshots.DigestSender"
    /// </summary>
    [TestFixture, Explicit("Sends live messages to the docs author's accounts")]
    public class DigestSender
    {
        private string? _telegramToken;
        private string? _slackToken;
        private string? _telegramChatId;
        private string? _slackUserId;

        [OneTimeSetUp]
        public void LoadTokens()
        {
            var secretsPath = Path.GetFullPath(Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                "..", "..", "..", "..", "Preesta", "secrets", "appsettings.secrets.yaml"));

            if (!File.Exists(secretsPath))
                Assert.Ignore($"No secrets file at {secretsPath}");

            using var reader = new StringReader(File.ReadAllText(secretsPath));
            var yaml = new YamlStream();
            yaml.Load(reader);
            var root = (YamlMappingNode)yaml.Documents[0].RootNode;
            _telegramToken  = ReadField(root, "Telegram", "botToken");
            _slackToken     = ReadField(root, "Slack",    "botToken");
            // docsTestChatId / docsTestUserId are not real Preesta config keys;
            // they live here only so this fixture knows where to send while
            // staying out of the committed source.
            _telegramChatId = ReadField(root, "Telegram", "docsTestChatId");
            _slackUserId    = ReadField(root, "Slack",    "docsTestUserId");

            if (string.IsNullOrEmpty(_telegramToken) && string.IsNullOrEmpty(_slackToken))
                Assert.Ignore("Neither Telegram nor Slack bot token configured");
        }

        [Test]
        public void Telegram_SingleTracker()  => SendTelegram("Daily standup", SingleTrackerPackage());
        [Test]
        public void Telegram_MultiTracker()   => SendTelegramMulti("Cross-tracker digest", MultiTrackerPackages());
        [Test]
        public void Telegram_PerAssignee()    => SendTelegram("Your open work", PerAssigneeOwnerSlice());
        [Test]
        public void Telegram_Stale()          => SendTelegram("Stale PRs — needs your attention", StalePackage());

        [Test]
        public void Slack_SingleTracker()     => SendSlack("Daily standup", SingleTrackerPackage());
        [Test]
        public void Slack_MultiTracker()      => SendSlackMulti("Cross-tracker digest", MultiTrackerPackages());
        [Test]
        public void Slack_PerAssignee()       => SendSlack("Your open work", PerAssigneeOwnerSlice());
        [Test]
        public void Slack_Stale()             => SendSlack("Stale PRs — needs your attention", StalePackage());

        // ---------- senders ----------

        private void SendTelegram(string subject, Package<NotificationReaction, Issue> package)
            => SendTelegramMulti(subject, new[] { package }, rootUri: InferRootUri(package));

        private void SendTelegramMulti(string subject, Package<NotificationReaction, Issue>[] packages,
            string? rootUri = null)
        {
            if (string.IsNullOrEmpty(_telegramToken)) Assert.Ignore("No Telegram token");
            if (string.IsNullOrEmpty(_telegramChatId)) Assert.Ignore("Telegram:docsTestChatId not set in secrets");

            var body = IssueFormatter.ToText(packages, rootUri ?? "https://jira.example.com/", linearWorkspace: "acme");
            var messenger = new TelegramMessenger(_telegramToken!);
            messenger.Send(new Message
            {
                To = _telegramChatId!,
                Subject = $"[Preesta docs] {subject}",
                TextBody = $"<b>{System.Net.WebUtility.HtmlEncode(subject)}</b>\n\n{body}"
            });
        }

        private void SendSlack(string subject, Package<NotificationReaction, Issue> package)
            => SendSlackMulti(subject, new[] { package }, rootUri: InferRootUri(package));

        private void SendSlackMulti(string subject, Package<NotificationReaction, Issue>[] packages,
            string? rootUri = null)
        {
            if (string.IsNullOrEmpty(_slackToken)) Assert.Ignore("No Slack token");
            if (string.IsNullOrEmpty(_slackUserId)) Assert.Ignore("Slack:docsTestUserId not set in secrets");

            var body = IssueFormatter.ToSlackMrkdwn(packages, rootUri ?? "https://jira.example.com/", linearWorkspace: "acme");
            var messenger = new SlackMessenger(_slackToken!);
            messenger.Send(new Message
            {
                To = _slackUserId!,
                Subject = $"[Preesta docs] {subject}",
                TextBody = $"*{subject}*\n\n{body}"
            });
        }

        // ---------- scenario builders (mirror DigestScreenshotGen) ----------

        private static Package<NotificationReaction, Issue> SingleTrackerPackage() => new()
        {
            Reaction = new NotificationReaction
            {
                Subject = "Daily standup",
                Columns = new[] { "Status", "Priority", "Assignee" }
            },
            Properties = { ["Jql"] = "assignee = currentUser() AND resolution = Unresolved ORDER BY priority DESC" },
            Items = new[]
            {
                MkIssue("PRE-142", "Refactor mutation executor to share error envelope handling",
                    status: "In Progress", priority: "High", assignee: "Alice Chen"),
                MkIssue("PRE-138", "Add per-channel circuit breaker for SMTP auth failures",
                    status: "In Review", priority: "Medium", assignee: "Alice Chen"),
                MkIssue("PRE-131", "Telegram digest truncation at 4096 char limit",
                    status: "To Do", priority: "Urgent", assignee: "Alice Chen"),
                MkIssue("PRE-127", "Document the secrets file location on every delivery page",
                    status: "Blocked", priority: "Low", assignee: "Alice Chen"),
            }
        };

        private static Package<NotificationReaction, Issue>[] MultiTrackerPackages()
        {
            var jira = new Package<NotificationReaction, Issue>
            {
                Reaction = new NotificationReaction
                {
                    Subject = "Cross-tracker digest",
                    Columns = new[] { "Status", "Priority", "Assignee" }
                },
                Properties = { ["Jql"] = "project = PLATFORM AND assignee = currentUser() AND status != Done" },
                Items = new[]
                {
                    MkIssue("PLAT-2041", "Migrate auth middleware to JWT v3",
                        status: "In Progress", priority: "High", assignee: "Alice Chen"),
                    MkIssue("PLAT-2038", "Investigate p99 latency on /api/users",
                        status: "To Do", priority: "Medium", assignee: "Alice Chen"),
                }
            };
            var github = new Package<NotificationReaction, Issue>
            {
                Reaction = new NotificationReaction
                {
                    Subject = "Cross-tracker digest",
                    Columns = new[] { "Status", "Type", "Assignee" }
                },
                Properties = { ["GithubFilter"] = "is:open repo:acme/api review-requested:@me" },
                Items = new[]
                {
                    MkGithub("acme/api#412", "Add retry hook to outbound webhook dispatcher",
                        status: "Open", type: "PR", assignee: "Alice Chen",
                        url: "https://github.com/acme/api/pull/412"),
                    MkGithub("acme/api#408", "Race condition in session refresh path",
                        status: "Open", type: "Issue", assignee: "Alice Chen",
                        url: "https://github.com/acme/api/issues/408"),
                }
            };
            var linear = new Package<NotificationReaction, Issue>
            {
                Reaction = new NotificationReaction
                {
                    Subject = "Cross-tracker digest",
                    Columns = new[] { "Status", "Priority", "Assignee" }
                },
                Properties =
                {
                    ["LinearViewId"]   = "0e8a3b41-1234-4321-aaaa-bbbbbbbbbbbb",
                    ["LinearViewName"] = "My Sprint Blockers"
                },
                Items = new[]
                {
                    MkLinear("DES-58", "Polish onboarding empty state",
                        status: "In Progress", priority: "Medium", assignee: "Alice Chen",
                        url: "https://linear.app/acme/issue/DES-58"),
                }
            };
            return new[] { jira, github, linear };
        }

        private static Package<NotificationReaction, Issue> PerAssigneeOwnerSlice() => new()
        {
            // Just one slice — the recipient is the docs author, so showing
            // their personal digest is the most authentic per-assignee shot.
            Reaction = new NotificationReaction
            {
                Subject = "Your open work",
                Columns = new[] { "Status", "Priority" }
            },
            Properties = { ["Jql"] = "assignee = currentUser() AND resolution = Unresolved" },
            Items = new[]
            {
                MkIssue("PRE-142", "Refactor mutation executor to share error envelope handling",
                    status: "In Progress", priority: "High",   assignee: "Alice Chen"),
                MkIssue("PRE-138", "Add per-channel circuit breaker for SMTP auth failures",
                    status: "In Review",   priority: "Medium", assignee: "Alice Chen"),
            }
        };

        private static Package<NotificationReaction, Issue> StalePackage() => new()
        {
            Reaction = new NotificationReaction
            {
                Subject = "Stale PRs — needs your attention",
                Followup = "These PRs have been waiting on review for more than 7 days. " +
                                  "Please review, ping the author, or close if no longer relevant.",
                Columns = new[] { "Status", "Type", "Assignee", "Updated" }
            },
            Properties = { ["GithubFilter"] = "is:open is:pr review-requested:@me updated:<2026-05-12" },
            Items = new[]
            {
                MkGithub("acme/api#412", "Add retry hook to outbound webhook dispatcher",
                    status: "Open", type: "PR", assignee: "Alice Chen",
                    url: "https://github.com/acme/api/pull/412",
                    updated: new DateTime(2026, 5, 8)),
                MkGithub("acme/web#1108", "Migrate settings page to React 19",
                    status: "Open", type: "PR", assignee: "Bob Martinez",
                    url: "https://github.com/acme/web/pull/1108",
                    updated: new DateTime(2026, 5, 6)),
                MkGithub("acme/api#398", "Backport rate-limit fix to 2025.04 release",
                    status: "Open", type: "PR", assignee: "Clara Volkov",
                    url: "https://github.com/acme/api/pull/398",
                    updated: new DateTime(2026, 5, 3)),
            }
        };

        // ---------- helpers ----------

        private static string? ReadField(YamlMappingNode root, string section, string key)
        {
            if (!root.Children.TryGetValue(new YamlScalarNode(section), out var sectionNode)) return null;
            if (sectionNode is not YamlMappingNode mapping) return null;
            if (!mapping.Children.TryGetValue(new YamlScalarNode(key), out var valueNode)) return null;
            return (valueNode as YamlScalarNode)?.Value;
        }

        private static string InferRootUri(Package<NotificationReaction, Issue> package) =>
            package.Properties.ContainsKey("GithubFilter") ? "https://github.com/" :
            package.Properties.ContainsKey("LinearViewId") ? "https://linear.app/acme/" :
            "https://jira.example.com/";

        private static Issue MkIssue(string key, string summary,
            string status, string priority, string assignee, DateTime? updated = null)
        {
            return new Issue
            {
                Key = key,
                Summary = summary,
                Status = status,
                Priority = priority,
                CreatedDate = new DateTime(2026, 5, 1),
                UpdatedDate = updated ?? new DateTime(2026, 5, 18),
                Participants = new IssueParticipants
                {
                    Assignee = new User { DisplayName = assignee }
                }
            };
        }

        private static Issue MkGithub(string key, string summary,
            string status, string type, string assignee, string url, DateTime? updated = null)
        {
            return new Issue
            {
                Key = key,
                Summary = summary,
                Url = url,
                Status = status,
                Type = type,
                CreatedDate = new DateTime(2026, 5, 1),
                UpdatedDate = updated ?? new DateTime(2026, 5, 18),
                Participants = new IssueParticipants
                {
                    Assignee = new User { DisplayName = assignee }
                }
            };
        }

        private static Issue MkLinear(string key, string summary,
            string status, string priority, string assignee, string url)
        {
            return new Issue
            {
                Key = key,
                Summary = summary,
                Url = url,
                Status = status,
                Priority = priority,
                CreatedDate = new DateTime(2026, 5, 1),
                UpdatedDate = new DateTime(2026, 5, 18),
                Participants = new IssueParticipants
                {
                    Assignee = new User { DisplayName = assignee }
                }
            };
        }
    }
}
