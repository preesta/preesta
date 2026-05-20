using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Preesta.Data;
using Preesta.Data.Supplying;
using Preesta.Formatting;

namespace Tests.Screenshots
{
    /// <summary>
    /// Generates HTML snapshots of the email digest for the docs site.
    /// Run explicitly:
    ///   dotnet test --filter "FullyQualifiedName~Screenshots.DigestScreenshotGen"
    /// Writes to <c>docs/assets/rendered-html/email-*.html</c>. The corresponding
    /// PNGs in <c>docs/assets/screenshots/</c> are taken by hand in a browser.
    /// Not part of CI — these are visual artifacts for the documentation site.
    /// </summary>
    [TestFixture, Explicit("Generates documentation artifacts; not part of CI")]
    public class DigestScreenshotGen
    {
        private static string OutDir =>
            Path.GetFullPath(Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                "..", "..", "..", "..", "docs", "assets", "rendered-html"));

        [OneTimeSetUp]
        public void EnsureOutDir() => Directory.CreateDirectory(OutDir);

        [Test]
        public void Email_SingleTracker_SimpleDigest()
        {
            var package = new Package<NotificationReaction, Issue>
            {
                Reaction = new NotificationReaction
                {
                    Subject = "Morning standup",
                    Columns = new[] { "Status", "Priority", "Assignee" }
                },
                Properties = { ["Jql"] = "assignee = currentUser() AND resolution = Unresolved ORDER BY priority DESC" },
                Items = new[]
                {
                    Issue("PRE-142", "Refactor mutation executor to share error envelope handling",
                        status: "In Progress", priority: "High",     assignee: "Alice Chen"),
                    Issue("PRE-138", "Add per-channel circuit breaker for SMTP auth failures",
                        status: "In Review",  priority: "Medium",   assignee: "Alice Chen"),
                    Issue("PRE-131", "Telegram digest truncation at 4096 char limit",
                        status: "To Do",      priority: "Urgent",   assignee: "Alice Chen"),
                    Issue("PRE-127", "Document the secrets file location on every delivery page",
                        status: "Blocked",    priority: "Low",      assignee: "Alice Chen"),
                }
            };

            WriteWithFrame("email-single-tracker.html",
                "Daily standup — Jira",
                IssueFormatter.ToHtml(new[] { package }, "https://jira.example.com/"));
        }

        [Test]
        public void Email_MultiTracker_JiraGithubLinear()
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
                    Issue("PLAT-2041", "Migrate auth middleware to JWT v3",
                        status: "In Progress", priority: "High", assignee: "Alice Chen"),
                    Issue("PLAT-2038", "Investigate p99 latency on /api/users",
                        status: "To Do",       priority: "Medium", assignee: "Alice Chen"),
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
                    GithubIssue("acme/api#412", "Add retry hook to outbound webhook dispatcher",
                        status: "Open", type: "PR", assignee: "Alice Chen",
                        url: "https://github.com/acme/api/pull/412"),
                    GithubIssue("acme/api#408", "Race condition in session refresh path",
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
                    LinearIssue("DES-58", "Polish onboarding empty state",
                        status: "In Progress", priority: "Medium", assignee: "Alice Chen",
                        url: "https://linear.app/acme/issue/DES-58"),
                }
            };

            WriteWithFrame("email-multi-tracker.html",
                "Cross-tracker digest — Jira + GitHub + Linear",
                IssueFormatter.ToHtml(new[] { jira, github, linear },
                    "https://jira.example.com/", linearWorkspace: "acme"));
        }

        [Test]
        public void Email_PerAssignee_OneRuleThreeRecipients()
        {
            var alice = new Package<NotificationReaction, Issue>
            {
                Reaction = new NotificationReaction
                {
                    Subject = "Your open work",
                    Columns = new[] { "Status", "Priority" }
                },
                Properties = { ["Jql"] = "assignee = currentUser() AND resolution = Unresolved" },
                Items = new[]
                {
                    Issue("PRE-142", "Refactor mutation executor to share error envelope handling",
                        status: "In Progress", priority: "High",   assignee: "Alice Chen"),
                    Issue("PRE-138", "Add per-channel circuit breaker for SMTP auth failures",
                        status: "In Review",   priority: "Medium", assignee: "Alice Chen"),
                }
            };

            var bob = new Package<NotificationReaction, Issue>
            {
                Reaction = new NotificationReaction
                {
                    Subject = "Your open work",
                    Columns = new[] { "Status", "Priority" }
                },
                Properties = { ["Jql"] = "assignee = currentUser() AND resolution = Unresolved" },
                Items = new[]
                {
                    Issue("PRE-201", "Wire up Slack circuit breaker mirror of Telegram path",
                        status: "To Do",       priority: "Urgent", assignee: "Bob Martinez"),
                    Issue("PRE-197", "Cookbook: per-team routing recipe",
                        status: "In Progress", priority: "Low",    assignee: "Bob Martinez"),
                    Issue("PRE-186", "Investigate Linear rate-limit spike on EU instance",
                        status: "Blocked",     priority: "High",   assignee: "Bob Martinez"),
                }
            };

            var clara = new Package<NotificationReaction, Issue>
            {
                Reaction = new NotificationReaction
                {
                    Subject = "Your open work",
                    Columns = new[] { "Status", "Priority" }
                },
                Properties = { ["Jql"] = "assignee = currentUser() AND resolution = Unresolved" },
                Items = new[]
                {
                    Issue("PRE-211", "Add screenshot generator for docs",
                        status: "In Progress", priority: "Medium", assignee: "Clara Volkov"),
                }
            };

            WriteWithFrame("email-per-assignee.html",
                "One impersonal rule, three separate digests (per assignee)",
                ThreeColumnLayout(
                    ("alice@example.com",  IssueFormatter.ToHtml(new[] { alice  }, "https://jira.example.com/")),
                    ("bob@example.com",    IssueFormatter.ToHtml(new[] { bob    }, "https://jira.example.com/")),
                    ("clara@example.com",  IssueFormatter.ToHtml(new[] { clara  }, "https://jira.example.com/"))
                ),
                wide: true);
        }

        [Test]
        public void Email_StaleWithRecommendations()
        {
            var stale = new Package<NotificationReaction, Issue>
            {
                Reaction = new NotificationReaction
                {
                    Subject = "Stale PRs — needs your attention",
                    Recommendations = "These PRs have been waiting on review for more than 7 days. " +
                                      "Please review, ping the author, or close if no longer relevant.",
                    Columns = new[] { "Status", "Type", "Assignee", "Updated" }
                },
                Properties = { ["GithubFilter"] = "is:open is:pr review-requested:@me updated:<2026-05-12" },
                Items = new[]
                {
                    GithubIssue("acme/api#412", "Add retry hook to outbound webhook dispatcher",
                        status: "Open", type: "PR", assignee: "Alice Chen",
                        url: "https://github.com/acme/api/pull/412",
                        updated: new DateTime(2026, 5, 8)),
                    GithubIssue("acme/web#1108", "Migrate settings page to React 19",
                        status: "Open", type: "PR", assignee: "Bob Martinez",
                        url: "https://github.com/acme/web/pull/1108",
                        updated: new DateTime(2026, 5, 6)),
                    GithubIssue("acme/api#398", "Backport rate-limit fix to 2025.04 release",
                        status: "Open", type: "PR", assignee: "Clara Volkov",
                        url: "https://github.com/acme/api/pull/398",
                        updated: new DateTime(2026, 5, 3)),
                }
            };

            WriteWithFrame("email-stale-with-recommendations.html",
                "Stale-PR digest with recommendations header",
                IssueFormatter.ToHtml(new[] { stale }, "https://github.com/"));
        }

        // ===== Slack mockups =====
        // The mrkdwn body comes from the real IssueFormatter.ToSlackMrkdwn path
        // (so the text, emoji shortcodes, and `<url|label>` links match what a
        // production bot would post); the surrounding chrome — sidebar, header,
        // bot avatar, timestamp — is a Slack-styled HTML mock since logging
        // into app.slack.com requires user credentials.

        [Test]
        public void Slack_SingleTracker()
        {
            var package = new Package<NotificationReaction, Issue>
            {
                Reaction = new NotificationReaction
                {
                    Subject = "Daily standup",
                    Columns = new[] { "Status", "Priority", "Assignee" }
                },
                Items = new[]
                {
                    Issue("PRE-142", "Refactor mutation executor to share error envelope handling",
                        status: "In Progress", priority: "High",   assignee: "Alice Chen"),
                    Issue("PRE-138", "Add per-channel circuit breaker for SMTP auth failures",
                        status: "In Review",   priority: "Medium", assignee: "Alice Chen"),
                    Issue("PRE-131", "Telegram digest truncation at 4096 char limit",
                        status: "To Do",       priority: "Urgent", assignee: "Alice Chen"),
                    Issue("PRE-127", "Document the secrets file location on every delivery page",
                        status: "Blocked",     priority: "Low",    assignee: "Alice Chen"),
                }
            };
            WriteSlackMock("slack-single-tracker.html", "Daily standup",
                IssueFormatter.ToSlackMrkdwn(new[] { package }, "https://jira.example.com/"));
        }

        [Test]
        public void Slack_MultiTracker()
        {
            var packages = new[]
            {
                new Package<NotificationReaction, Issue>
                {
                    Reaction = new NotificationReaction { Subject = "Cross-tracker digest",
                        Columns = new[] { "Status", "Priority", "Assignee" } },
                    Items = new[]
                    {
                        Issue("PLAT-2041", "Migrate auth middleware to JWT v3",
                            status: "In Progress", priority: "High", assignee: "Alice Chen"),
                        Issue("PLAT-2038", "Investigate p99 latency on /api/users",
                            status: "To Do", priority: "Medium", assignee: "Alice Chen"),
                    }
                },
                new Package<NotificationReaction, Issue>
                {
                    Reaction = new NotificationReaction { Subject = "Cross-tracker digest",
                        Columns = new[] { "Status", "Type", "Assignee" } },
                    Items = new[]
                    {
                        GithubIssue("acme/api#412", "Add retry hook to outbound webhook dispatcher",
                            status: "Open", type: "PR",    assignee: "Alice Chen",
                            url: "https://github.com/acme/api/pull/412"),
                        GithubIssue("acme/api#408", "Race condition in session refresh path",
                            status: "Open", type: "Issue", assignee: "Alice Chen",
                            url: "https://github.com/acme/api/issues/408"),
                    }
                },
                new Package<NotificationReaction, Issue>
                {
                    Reaction = new NotificationReaction { Subject = "Cross-tracker digest",
                        Columns = new[] { "Status", "Priority", "Assignee" } },
                    Items = new[]
                    {
                        LinearIssue("DES-58", "Polish onboarding empty state",
                            status: "In Progress", priority: "Medium", assignee: "Alice Chen",
                            url: "https://linear.app/acme/issue/DES-58"),
                    }
                },
            };
            WriteSlackMock("slack-multi-tracker.html", "Cross-tracker digest",
                IssueFormatter.ToSlackMrkdwn(packages, "https://jira.example.com/", linearWorkspace: "acme"));
        }

        [Test]
        public void Slack_PerAssignee()
        {
            var package = new Package<NotificationReaction, Issue>
            {
                Reaction = new NotificationReaction { Subject = "Your open work",
                    Columns = new[] { "Status", "Priority" } },
                Items = new[]
                {
                    Issue("PRE-142", "Refactor mutation executor to share error envelope handling",
                        status: "In Progress", priority: "High",   assignee: "Alice Chen"),
                    Issue("PRE-138", "Add per-channel circuit breaker for SMTP auth failures",
                        status: "In Review",   priority: "Medium", assignee: "Alice Chen"),
                }
            };
            WriteSlackMock("slack-per-assignee.html", "Your open work",
                IssueFormatter.ToSlackMrkdwn(new[] { package }, "https://jira.example.com/"));
        }

        [Test]
        public void Slack_Stale()
        {
            var package = new Package<NotificationReaction, Issue>
            {
                Reaction = new NotificationReaction
                {
                    Subject = "Stale PRs — needs your attention",
                    Recommendations = "These PRs have been waiting on review for more than 7 days. " +
                                      "Please review, ping the author, or close if no longer relevant.",
                    Columns = new[] { "Status", "Type", "Assignee", "Updated" }
                },
                Items = new[]
                {
                    GithubIssue("acme/api#412", "Add retry hook to outbound webhook dispatcher",
                        status: "Open", type: "PR", assignee: "Alice Chen",
                        url: "https://github.com/acme/api/pull/412",
                        updated: new DateTime(2026, 5, 8)),
                    GithubIssue("acme/web#1108", "Migrate settings page to React 19",
                        status: "Open", type: "PR", assignee: "Bob Martinez",
                        url: "https://github.com/acme/web/pull/1108",
                        updated: new DateTime(2026, 5, 6)),
                    GithubIssue("acme/api#398", "Backport rate-limit fix to 2025.04 release",
                        status: "Open", type: "PR", assignee: "Clara Volkov",
                        url: "https://github.com/acme/api/pull/398",
                        updated: new DateTime(2026, 5, 3)),
                }
            };
            WriteSlackMock("slack-stale.html", "Stale PRs — needs your attention",
                IssueFormatter.ToSlackMrkdwn(new[] { package }, "https://github.com/"));
        }

        // ---------- helpers ----------

        private static Issue Issue(string key, string summary,
            string status, string priority, string assignee,
            DateTime? updated = null)
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

        private static Issue GithubIssue(string key, string summary,
            string status, string type, string assignee, string url,
            DateTime? updated = null)
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

        private static Issue LinearIssue(string key, string summary,
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

        private static string ThreeColumnLayout(params (string label, string html)[] columns)
        {
            var cells = string.Join("", columns.Select(c =>
                $@"<td style=""vertical-align:top;padding:0 8px;width:33%"">
                       <div style=""font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;
                                    font-size:11px;color:#5E6C84;text-align:center;margin-bottom:8px;
                                    text-transform:uppercase;letter-spacing:0.5px"">To: {c.label}</div>
                       <div style=""background:#FFFFFF;border:1px solid #DFE1E6;border-radius:6px;
                                    padding:16px 8px"">{c.html}</div>
                   </td>"));
            return $@"<table style=""width:100%;border-collapse:separate;border-spacing:0""><tr>{cells}</tr></table>";
        }

        private static void WriteWithFrame(string fileName, string caption, string body, bool wide = false)
        {
            var bodyMax = wide ? 1280 : 720;
            var html = $@"<!doctype html>
<html><head><meta charset=""utf-8"">
<title>{caption}</title>
<style>
  body {{ background:#F4F5F7; margin:0; padding:32px 16px;
         font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif; }}
  .frame {{ max-width:{bodyMax}px; margin:0 auto; }}
  .header {{ font-size:13px; color:#5E6C84; margin-bottom:8px; line-height:1.5; }}
  .header .from {{ color:#172B4D; font-weight:600; }}
  .subject {{ font-size:20px; color:#172B4D; font-weight:600; margin-bottom:18px;
             padding-bottom:14px; border-bottom:1px solid #DFE1E6; }}
  .card {{ background:#FFFFFF; border:1px solid #DFE1E6; border-radius:6px;
          padding:24px 28px; box-shadow:0 1px 2px rgba(9,30,66,0.08); }}
</style>
</head><body><div class=""frame"">
  <div class=""card"">
    <div class=""header"">
      <div><span class=""from"">Preesta</span> &lt;preesta@example.com&gt;</div>
      <div>to me</div>
    </div>
    <div class=""subject"">{caption}</div>
    {body}
  </div>
</div></body></html>";
            File.WriteAllText(Path.Combine(OutDir, fileName), html);
            TestContext.WriteLine($"Wrote {Path.Combine(OutDir, fileName)}");
        }

        // Convert Slack mrkdwn into HTML for the mockup:
        //   *bold*      → <strong>bold</strong>
        //   <url|label> → <a href="url">label</a>
        //   :emoji:     → the Unicode glyph Preesta's status/priority chips map to
        // Newlines map to <br>, surrounding HTML escaping handled before regex.
        private static string MrkdwnToHtml(string mrkdwn)
        {
            var emojiMap = new Dictionary<string, string>
            {
                [":white_check_mark:"]        = "✅",
                [":hourglass_flowing_sand:"]  = "⏳",
                [":black_square_button:"]     = "🔲",
                [":open_file_folder:"]        = "📂",
                [":x:"]                       = "❌",
                [":no_entry:"]                = "⛔",
                [":grey_question:"]           = "❔",
                [":red_circle:"]              = "🔴",
                [":large_orange_circle:"]     = "🟠",
                [":large_yellow_circle:"]     = "🟡",
                [":large_green_circle:"]      = "🟢",
            };

            var s = System.Net.WebUtility.HtmlEncode(mrkdwn);
            foreach (var (shortcode, glyph) in emojiMap)
                s = s.Replace(shortcode, glyph);
            // <url|label> — but only ours where url is http(s)
            s = System.Text.RegularExpressions.Regex.Replace(
                s, @"&lt;(https?://[^|]+)\|([^&]+)&gt;",
                "<a href=\"$1\" style=\"color:#1264A3;text-decoration:none\">$2</a>");
            // *bold* — single asterisks, Slack style. Match * + non-space-then-non-asterisk + *
            s = System.Text.RegularExpressions.Regex.Replace(
                s, @"\*([^*\n]+)\*",
                "<strong>$1</strong>");
            // _italic_
            s = System.Text.RegularExpressions.Regex.Replace(
                s, @"(^|[\s>])_([^_\n]+)_(?=[\s<]|$)",
                "$1<em>$2</em>");
            return s.Replace("\n", "<br>");
        }

        private static void WriteSlackMock(string fileName, string caption, string mrkdwn)
        {
            var bodyHtml = MrkdwnToHtml(mrkdwn);
            var html = $@"<!doctype html>
<html><head><meta charset=""utf-8"">
<title>{System.Net.WebUtility.HtmlEncode(caption)}</title>
<style>
  body {{ background:#FFFFFF; margin:0; padding:0;
         font-family:'Slack-Lato','Lato','Helvetica Neue',Helvetica,Arial,sans-serif;
         color:#1D1C1D; font-size:15px; line-height:1.46668; }}
  .app {{ display:flex; min-height:100vh; }}
  .sidebar {{ width:72px; background:#1A1D21; padding:14px 0; flex-shrink:0; }}
  .ws-icon {{ width:36px; height:36px; border-radius:9px; background:#611F69;
             color:#fff; display:flex; align-items:center; justify-content:center;
             font-weight:900; font-size:18px; margin:0 auto; }}
  .channels {{ width:240px; background:#19171D; color:#D1D2D3; padding:14px 0;
              flex-shrink:0; }}
  .ws-name {{ color:#fff; font-weight:900; padding:0 16px 12px; font-size:18px;
             border-bottom:1px solid #2C2D30; }}
  .channel-item {{ padding:6px 16px; color:#D1D2D3; }}
  .channel-item.active {{ background:#1164A3; color:#fff; }}
  .channel-item .hash {{ color:#9A9BA0; margin-right:8px; }}
  .channels-heading {{ padding:14px 16px 6px; font-size:13px; font-weight:700;
                      color:#9A9BA0; }}
  .main {{ flex:1; display:flex; flex-direction:column; min-width:0; }}
  .topbar {{ height:48px; padding:0 20px; border-bottom:1px solid #E1E1E1;
            display:flex; align-items:center; font-weight:900; }}
  .topbar .at {{ color:#1D1C1D; margin-right:6px; opacity:0.6; }}
  .feed {{ flex:1; padding:24px 20px; }}
  .msg {{ display:flex; gap:10px; align-items:flex-start; }}
  .avatar {{ width:36px; height:36px; border-radius:4px; background:#611F69;
            color:#fff; display:flex; align-items:center; justify-content:center;
            font-weight:900; flex-shrink:0; }}
  .msg-body {{ flex:1; min-width:0; }}
  .meta {{ font-size:15px; margin-bottom:2px; }}
  .meta .name {{ font-weight:900; }}
  .meta .app-tag {{ background:#E8E8E8; color:#616061; font-size:10px;
                   padding:1px 4px; border-radius:2px; font-weight:700;
                   text-transform:uppercase; margin:0 6px; vertical-align:2px; }}
  .meta .time {{ color:#616061; font-size:12px; }}
  .text {{ white-space:normal; }}
  .text a {{ color:#1264A3; }}
</style></head><body>
<div class=""app"">
  <div class=""sidebar""><div class=""ws-icon"">P</div></div>
  <div class=""channels"">
    <div class=""ws-name"">preesta-test</div>
    <div class=""channels-heading"">Direct messages</div>
    <div class=""channel-item active""><span class=""hash"">●</span>Preesta</div>
    <div class=""channel-item""><span class=""hash"">●</span>Slackbot</div>
  </div>
  <div class=""main"">
    <div class=""topbar""><span class=""at"">@</span>Preesta</div>
    <div class=""feed"">
      <div class=""msg"">
        <div class=""avatar"">P</div>
        <div class=""msg-body"">
          <div class=""meta""><span class=""name"">Preesta</span><span class=""app-tag"">APP</span><span class=""time"">{DateTime.Now:h:mm tt}</span></div>
          <div class=""text""><strong>{System.Net.WebUtility.HtmlEncode(caption)}</strong><br><br>{bodyHtml}</div>
        </div>
      </div>
    </div>
  </div>
</div></body></html>";
            File.WriteAllText(Path.Combine(OutDir, fileName), html);
            TestContext.WriteLine($"Wrote {Path.Combine(OutDir, fileName)}");
        }
    }
}
