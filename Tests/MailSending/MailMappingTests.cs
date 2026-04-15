using System;
using System.Linq;
using System.Xml.Linq;
using Messaging;
using Bender.Configuration;
using NUnit.Framework;
using NSubstitute;
using Bender.Notification;
using Serilog;

namespace Tests.MailSending
{
    [TestFixture]
    public class MailMappingTests
    {
        [Test]
        public void TestSimpleMapping()
        {
            var config = XDocument.Parse(
@"
<root>
    <redirection_rules>
        <rule from=""Ivanov"" to=""Petrov""/>
    </redirection_rules>
</root>
");

            var logger = Substitute.For<ILogger>();
            var redirector = new Redirector(new XmlRulesConfig(config, logger).GetRedirectionMap(), Enumerable.Empty<string>(), Enumerable.Empty<string>());
            var initialMessage = new Message { To = "ivanov" };
            var actualizedMessage = redirector.ActualizeAddressees(initialMessage);
                           
            Assert.AreEqual("Petrov", actualizedMessage.To);
        }
    }
}
