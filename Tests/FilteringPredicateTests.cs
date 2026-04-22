using System.Linq;
using Bender.Configuration;
using Bender.Configuration.Action;
using Bender.Data;
using Bender.Data.Supplying;
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
            var jira = Substitute.For<Bender.IJiraService>();
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