using System;
using System.Linq;
using System.Xml.Linq;
using Preesta.Configuration;
using NSubstitute;
using NUnit.Framework;
using Serilog;

namespace Tests
{
    [TestFixture]
    public class XmlConfigTests
    {
        [Test]
        public void GetIssueRules()
        {
            var xml =
@"<configuration>
    <request group=""test"">
        <notify subject=""test"" mailTo=""admin"" />
        <jql/>
    </request>
</configuration>";

            var config = new XmlRulesConfig(XDocument.Parse(xml), Substitute.For<ILogger>());

            Rule rule = config.GetJqlRules("test").Single();
            Assert.AreEqual(string.Join(",", rule.Notification!.RawRecipients), string.Join(",", "admin"));
            Assert.AreEqual(rule.Notification.Subject, "test");
        }

        [Test]
        public void GetReleaseRules()
        {
            var xml =
@"<configuration>
    <build
      group=""test""
      mask=""some valid regex""
      projectCode=""BENDER""
      remainingDays=""2"">

        <notify subject=""test"" mailTo=""admin""/>
    </build>
</configuration>";

            var config = new XmlRulesConfig(XDocument.Parse(xml), Substitute.For<ILogger>());

            var rule = config.GetReleaseRules("test").Single();
            Assert.IsFalse(rule.ExpiredOnly);
            Assert.AreEqual(string.Join(",", rule.Notification!.RawRecipients), string.Join(",", new[] { "admin" }));
            Assert.AreEqual(rule.Notification.Subject, "test");
            Assert.AreEqual(rule.ProjectCode, "BENDER");
        }

        [Test]
        public void GetRedirectionRules()
        {
            var xml =
@"<configuration>
    <redirection_rules>
        <rule from=""Bender"" to=""Phillip""/>
    </redirection_rules>
</configuration>";

            var config = new XmlRulesConfig(XDocument.Parse(xml), Substitute.For<ILogger>());

            var redirectionMap = config.GetRedirectionMap();
            string? to;
            Assert.IsTrue(redirectionMap.TryGetValue("Bender", out to));
            Assert.AreEqual(to, "Phillip");
        }

        [Test]
        public void CheckExceptionHandler()
        {
            // Required field 'mask' is absent
            var xml =
@"<configuration>
    <build
      group=""test""
      maskER=""some valid regex""
      projectCode=""BENDER""
      remainingDays=""2"">

        <notify subject=""test"" mailTo=""admin""/>
    </build>
</configuration>";

            var logger = Substitute.For<ILogger>();

            var config = new XmlRulesConfig(XDocument.Parse(xml), logger);

            Assert.IsFalse(config.GetReleaseRules("test").Any());
            logger.Received(1).Error(Arg.Is<Exception>(e => e != null), Arg.Any<string>());
        }
    }
}
