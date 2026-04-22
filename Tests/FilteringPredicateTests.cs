using System.Linq;
using Preesta.Configuration;
using Preesta.Configuration.Action;
using Preesta.Data;
using Preesta.Data.Supplying;
using NUnit.Framework;
using NSubstitute;
using Serilog;

namespace Tests
{
    [TestFixture]
    public class FilteringPredicateTests
    {
        [Test]
        public void EnsurePredicateIsCalling()
        {
            var jira = Substitute.For<Preesta.IJiraService>();
            jira
                .GetIssuesForJql(Arg.Any<string>())
                .Returns(
                    new[]
                        {
                            new Issue
                            {
                                BuildFixed = new[] {"1"},
                                BuildFound = new string[] {},
                                Staff = new IssueStaff()
                            }
                        }
                );

            var rules = 
                new[]
                    {
                        new JqlRule
                        {
                            AdditionalPredicateName = "MoreThanOneFixVersion",
                            Jql = "any jql",
                            HowToNotify = new Notify
                            {
                                MetaAddressers = new[] {"1"},
                                MetaCarbonCopy = new string[] { },
                                Subject = "any subject",
                            }
                        }
                    };

            var logger = Substitute.For<ILogger>();
            var packages = new JqlSupplier(jira, rules, logger).GetPackages();
            Assert.IsFalse(packages.Any());
        }
   }
}