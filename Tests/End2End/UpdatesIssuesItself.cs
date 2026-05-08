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
using System.Xml.Linq;
using Tests.Mocks;

using static System.String;

namespace End2End.Tests
{
    [TestFixture]
    public class UpdatesIssuesItself
    {
        #region "Raw Data"
        private const string JsonTypicalIssue =
@"{
	""issues"": [{
		""key"": ""BENDER-961"",
		""fields"": {
			""status"": {

			},
			""issuetype"": {

			},
			""created"": ""2018-08-14T13:50:59.000+0000"",
            ""assignee"": {
                ""name"": ""alice""
            },
            ""reporter"": {
                ""name"": ""bob""
            }
		}
	}]
}";

        #endregion

        [Test]
        public void EnsureRequestToUpdateFormedCorrectlyAndRan()
        {
            using var server = new MockJiraServer();
            server.StubGetIssuesByJql("any", JsonTypicalIssue);
            server.StubPostIssue("BENDER-961");

            var connection = new Connection(server.Url, "any", "any");

            var svc = new HttpJiraService(server.Url, Empty, Empty)
            {
                Connection = connection
            };

            var jqlRule = new JqlRule
            {
                Jql = "any",
                Updates = new[]
                {
                    new SelfUpdateSpec
                    {
                        Verb = "POST",
                        UrlPattern = "{{@jiraRoot}}/rest/api/2/issue/{{@issueKey}}/transitions",
                        BodyPattern = @"{
    ""update"":
	{
        ""comment"": [
            {
                ""add"": {
                    ""body"": ""Issue {{@issueKey}} Closed automatically because of activity absence""
                }
            }
        ]
    },

    ""transition"": {
        ""id"": ""61""
    }
}"
                    }
                }
            };

            var jqlSupplier = new JqlSupplier(svc, new[] { jqlRule }, Substitute.For<ILogger>());

            var messenger = Substitute.For<IMessenger>();

            var pipe = new ReactionPipeline<Issue>
            {
                PackageSupplier = jqlSupplier,
                PackageConverter = new IssuePackageConverter(server.Url),
                Messenger = messenger,
                HttpHandler = svc
            };
            pipe.Run();

            Assert.AreEqual(1, server.LogEntries.Count(e =>
                e.RequestMessage != null
                && e.RequestMessage.AbsoluteUrl == $"{server.Url}/rest/api/2/issue/BENDER-961/transitions"
                && e.RequestMessage.Method == "POST"
                && (e.RequestMessage.Body ?? "").Contains("Issue BENDER-961 Closed automatically because of activity absence")));
        }

        [Test]
        public void CheckExpressionsEvaluationInUrlAndBody()
		{
			// Setup
			var package = new Package<SelfUpdate, Issue>()
			{
				Items = new[] { new Issue
                {
                     Key = "T-1"
				}
				},
				Reaction = new SelfUpdate
				{
					UrlPattern = "{{@jiraRoot}}/rest/api/<<c# \"{{@jiraRoot}}\".Contains(\"jiraeu\") ? \"2\" : \"1\" #>>/issue/{{@issueKey}}/transitions",
					BodyPattern = "datetime: \"<<c# new System.DateTime(2019, 03, 18).ToString(\"yyyy-MM-dd\") #>>\""
				}

			};
			var converter1 = new IssuePackageConverter("http://jiraeu");
			var converter2 = new IssuePackageConverter("http://jira");

			// Experiment
			var result1 = converter1.ToHttpRequests(new[] { package });
            var result2 = converter2.ToHttpRequests(new[] { package });

			// Check results
			Assert.AreEqual(
				"http://jiraeu/rest/api/2/issue/T-1/transitions",   // Expected
				result1.Single().Uri.ToString()                     // Actual
				);
			Assert.AreEqual(
				"http://jira/rest/api/1/issue/T-1/transitions",     // Expected
				result2.Single().Uri.ToString()                     // Actual
				);

			Assert.AreEqual(
				"datetime: \"2019-03-18\"",                         // Expected
				result1.Single().Body                               // Actual
				);
		}

		[Test]
		public void CheckMultilineExpressionEvaluation()
		{
			// Setup
			var package = new Package<SelfUpdate, Issue>()
			{
				Items = new[] { new Issue
				{
					 Key = "T-1"
				}
				},
				Reaction = new SelfUpdate
				{
					UrlPattern = "http://any",
					BodyPattern = @" ""datetime"":[{ ""<<c#(

						new System.DateTime(2020, 03, 20) +
						System.TimeSpan.FromDays(
							(new System.DateTime(2020, 03, 20)).Hour < 16 ? 0
							: System.DateTime.Today.DayOfWeek == System.DayOfWeek.Friday ? 3
							: 1
						)
                    )
                    .ToString(""yyyy-MM-dd"")#>>"" }]"
				}

			};
			var converter = new IssuePackageConverter(string.Empty);

			// Experiment
			var result = converter.ToHttpRequests(new[] { package });

			// Check results
			Assert.AreEqual(
				@" ""datetime"":[{ ""2020-03-20"" }]",                // Expected
				result.Single().Body                                 // Actual
				);
		}

        [Test]
        public void CheckAssigneeAndReporterReplacement()
        {
            using var server = new MockJiraServer();
            server.StubGetIssuesByJql("any", JsonTypicalIssue);
            server.StubAnyWrite();

            // Setup: rule URL points at the in-process WireMock server so the
            // PUT actually leaves the HttpClient and is recorded.
            var rule =
$@"<configuration>
  <jqlRule group=""test"">
    <jql>any</jql>
    <callRest verb=""PUT"" urlPattern=""{server.Url}/swap-assignee-and-reporter-where/?assignee={{{{@assignee.name}}}}&amp;reporter={{{{@reporter.name}}}}"">
                    <body><![CDATA[
                        {{
                            ""update"": {{
                                ""assignee"": [{{""set"": {{""name"": ""{{{{@reporter.name}}}}""}}}}],
                                ""reporter"": [{{""set"": {{""name"": ""{{{{@assignee.name}}}}""}}}}]
                            }}
                        }}
                    ]]>
                </body>
    </callRest>
  </jqlRule>
</configuration>";

            var rulesConfig = new XmlRulesConfig(XDocument.Parse(rule), Substitute.For<ILogger>());

            var connection = new Connection(server.Url, Empty, Empty);

            var jiraService = new HttpJiraService(server.Url, Empty, Empty)
            {
                Connection = connection
            };

            var packageSupplier = new JqlSupplier(jiraService, rulesConfig.GetJqlRules("test"), Substitute.For<ILogger>());
            var pipe = new ReactionPipeline<Issue>()
                {
                    PackageSupplier = packageSupplier,
                    PackageConverter = new IssuePackageConverter(Empty),
                    HttpHandler = jiraService
                };

            // Experiment
            pipe.Run();

            // Check results
            Assert.AreEqual(1, server.LogEntries.Count(e =>
                e.RequestMessage != null
                && e.RequestMessage.AbsoluteUrl == $"{server.Url}/swap-assignee-and-reporter-where/?assignee=alice&reporter=bob"
                && e.RequestMessage.Method == "PUT"
                && (e.RequestMessage.Body ?? "").Contains(@"""assignee"": [{""set"": {""name"": ""bob""}}]")
                && (e.RequestMessage.Body ?? "").Contains(@"""reporter"": [{""set"": {""name"": ""alice""}}]")));
        }

       [Test]
        public void CheckSeveralExpressionsToEvaluateInOneBody()
        {
            // Setup
            var converter = new IssuePackageConverter("https://jira.example.com");

            var package = new Package<SelfUpdate, Issue>
            {
                // JQL: issueFunction in issueFieldMatch('', labels, 'linkTo=')
                Items = new[]
                {
                    new Issue
                    {
                        Key = "BUG-2",
                        Labels = "linkTo=TASK-1",
                    }
                },

                Reaction = new SelfUpdate
                {
                    UrlPattern = "{{@jiraRoot}}/rest/api/2/issue/{{@issueKey}}",
                    BodyPattern = @"
{
    ""update"": {
        ""issueLinks"": [
            {
                ""add"": {
                    ""type"": {
                        ""name"": ""Relate""
                    },
                    ""outwardIssue"": {
                        ""key"": ""<<c# System.Text.RegularExpressions.Regex.Match(issue.Labels, ""linkTo=(.+-\\d+)"").Groups[1].Value #>>""
                    }
                }
            }
        ],
        ""labels"": [
            {
                ""remove"": ""<<c# System.Text.RegularExpressions.Regex.Match(issue.Labels, ""linkTo=.+-\\d+"").Value #>>""
            }
        ]
    }
}
"
                }
            };

            // Experiment
            var actualRequest = converter.ToHttpRequests(new[] {package}).Single();

            // Check results
            var expectedUrl = "https://jira.example.com/rest/api/2/issue/BUG-2";
            var expectedBody = @"
{
    ""update"": {
        ""issueLinks"": [
            {
                ""add"": {
                    ""type"": {
                        ""name"": ""Relate""
                    },
                    ""outwardIssue"": {
                        ""key"": ""TASK-1""
                    }
                }
            }
        ],
        ""labels"": [
            {
                ""remove"": ""linkTo=TASK-1""
            }
        ]
    }
}
";

            Assert.AreEqual(expectedUrl, actualRequest.Uri.ToString());
            Assert.AreEqual(expectedBody, actualRequest.Body);
        }
	}
}
