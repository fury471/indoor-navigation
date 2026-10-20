using IndoorNavigation.Core.Ready;
using NUnit.Framework;

namespace IndoorNavigation.Core.Tests
{
    public sealed class PackageVersionsTests
    {
        [TestCase("1.0.0", true)]
        [TestCase("1.4.2", true)]
        [TestCase("2.0.0", false)]
        [TestCase("0.9.0", false)]
        [TestCase("one", false)]
        [TestCase(null, false)]
        public void SchemaSupport_FollowsTheMajorVersion(string schema, bool supported)
        {
            Assert.AreEqual(supported, PackageVersions.IsSchemaSupported(schema));
        }

        [TestCase("0.1.0", null, true)]
        [TestCase("0.2.0", "0.1.0", true)]
        [TestCase("0.1.1", "0.1.0", true)]
        [TestCase("1.0.0", "0.9.9", true)]
        [TestCase("0.1.0", "0.1.0", false)]
        [TestCase("0.1.0", "0.2.0", false)]
        [TestCase("0.10.0", "0.9.0", true)]
        public void IsNewer_ComparesNumbersNotText(string candidate, string installed, bool newer)
        {
            Assert.AreEqual(newer, PackageVersions.IsNewer(candidate, installed));
        }
    }
}
