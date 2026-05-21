using System.Collections.Generic;
using System.Linq;
using Messaging;
using JiraRest;
using Preesta;
using Preesta.Configuration;
using Preesta.Configuration.Action;
using Preesta.Data;
using Preesta.Data.Supplying;
using Preesta.Data.Supplying.Convert;
using Preesta.Notification;
using NSubstitute;
using NUnit.Framework;
using Serilog;
using Tests.Mocks;

using static System.String;

namespace End2End.Tests
{
	[TestFixture]
	public class NotificationReactionsWithIssues
	{
		#region "Raw Data"
		private const string JsonIssueWithNullPriority = @"
{
	""issues"" : [{
			""expand"" : ""operations,versionedRepresentations,editmeta,changelog,transitions,renderedFields"",
			""key"" : ""BENDER-2301"",
			""fields"" : {
				""fixVersions"" : [],
				""resolution"" : null,
				""priority"" : null,
				""labels"" : [""collector""],
				""versions"" : [],
				""assignee"" : null,
				""status"" : {
					""name"" : ""Open""
				},
				""components"" : [{
						""name"" : ""Employee Profile & OrgChart""
					}
				],
				""reporter"" : {
					""displayName"" : ""John DiMaggio"",
				},
				""issuetype"" : {
					""name"" : ""Improvement""
				},
				""created"" : ""2017-02-08T07:04:57.000+0000"",
				""summary"" : ""Add option of editing order in which training courses are itemized""
			}
		}
	]
}
";

		private const string ExpectedNotificationWithEmptyPriority =
@"<h3>subject</h3>
<br>
<a href=""http://jira/issues/?jql=any"">JQL</a>
<br>
<table width=""100%"" border=""1"" cellspacing=""0"" cellpadding=""4"">
	<tr align=""center"" bgcolor=""#999999"">
		<td>Type</td>
		<td>Key</td>
		<td>Summary</td>
		<td>Assignee</td>
		<td>Reporter</td>
		<td>Status</td>
		<td>Priority</td>
		<td>Components</td>
		<td>Labels</td>
		<td>Time Spent (hrs)</td>
		<td>Affects Versions</td>
		<td>Fix Versions</td>
		<td>Due Date</td>
		<td>Created</td>
	</tr>
	<tr>
		<td>Improvement</td>
		<td><a href=""http://jira/browse/BENDER-2301"">BENDER-2301</a></td>
		<td>Add option of editing order in which training courses are itemized</td>
		<td>UNASSIGNED</td>
		<td>John DiMaggio</td>
		<td>Open</td>
		<td></td>
		<td>Employee Profile & OrgChart</td>
		<td>collector</td>
		<td>0</td>
		<td></td>
		<td></td>
		<td></td>
		<td>08.02.2017</td>
	</tr>
</table>
";

        private const string JsonIssueWithNullFixAndAffectedVersion = @"
{
    ""issues"": [
        {
            ""key"": ""BENDER-5753"",
            ""fields"": {
                ""labels"": [],
                ""assignee"": {
                    ""emailAddress"": ""Katey_Sagal@express.ship""
                },
                ""components"": [
                    {
                        ""name"": ""Sale: RFx""
                    }
                ]
            }
        }
    ]
}";

        #endregion

        [Test]
		public void IssueWithEmptyPriorityHandledWithoutException()
		{
			using var server = new MockJiraServer();
			server.StubGetIssuesByJql("any", JsonIssueWithNullPriority);

			var connection = new Connection(server.Url, "any", "any");

			var svc = new HttpJiraService(server.Url, Empty, Empty)
			{
				Connection = connection
			};

			var jqlRule = new JqlRule
			{
				Jql = "any",
				Notification = new NotificationSpec
				{
					Subject = "subject",
					RawRecipients = new[] { "assignee" },
					RawCc = new string[] { },
					Columns = new[] { "Type", "Key", "Summary", "Assignee", "Reporter", "Status", "Created" }
				}
			};

			var jqlSupplier = new JqlSupplier(svc, new[] { jqlRule }, Substitute.For<ILogger>());

			var messenger = Substitute.For<IMessenger>();
			var pipe = new ReactionPipeline<Issue>
			{
				PackageSupplier = jqlSupplier,
				PackageConverter = new IssuePackageConverter("http://jira"),
				Channels = global::Tests.TestSupport.Channels.Email(messenger),
				Mutations = new global::Preesta.Notification.Mutation.RestMutations(Substitute.For<IHttpHandler>())
			};
			pipe.Run();

			Assert.AreEqual(1, server.CountRequests("GET", $"{server.Url}/rest/api/2/search?jql=any&maxResults=50&fields=*all"));

			messenger.Received(1).SendAll(Arg.Is<IEnumerable<Message>>(
				msgs => msgs.Single().Subject.Contains("subject")
				     && msgs.Single().Body.Contains("BENDER-2301")
				     && msgs.Single().Body.Contains("UNASSIGNED")
				     && msgs.Single().Body.Contains("John DiMaggio")
				     && msgs.Single().Body.Contains("08.02.2017")));
		}


		[Test]
		public void IssueWithAbsentFixAndAffectedVersionHandledWithoutException()
		{
			using var server = new MockJiraServer();
			server.StubGetIssuesByJql("any", JsonIssueWithNullFixAndAffectedVersion);

			var connection = new Connection(server.Url, "any", "any");

			var svc = new HttpJiraService(server.Url, Empty, Empty)
			{
				Connection = connection
			};

			var jqlRule = new JqlRule
			{
				Jql = "any",
				Notification = new NotificationSpec
				{
					Subject = "subject",
					RawRecipients = new[] { "assignee" },
					RawCc = new string[] { }
				}
			};

			var jqlSupplier = new JqlSupplier(svc, new[] { jqlRule }, Substitute.For<ILogger>());

			var messenger = Substitute.For<IMessenger>();
			var pipe = new ReactionPipeline<Issue>
			{
				PackageSupplier = jqlSupplier,
				PackageConverter = new IssuePackageConverter("http://jira"),
				Channels = global::Tests.TestSupport.Channels.Email(messenger),
				Mutations = new global::Preesta.Notification.Mutation.RestMutations(Substitute.For<IHttpHandler>())
			};
			pipe.Run();

			Assert.AreEqual(1, server.CountRequests("GET", $"{server.Url}/rest/api/2/search?jql=any&maxResults=50&fields=*all"));
		}
	}
}
