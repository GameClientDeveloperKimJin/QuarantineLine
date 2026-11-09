using NUnit.Framework;
using QL.Core;

namespace QL.Tests.EditMode
{
    public class SmokeTests
    {
        [Test]
        public void CoreInfo_Name_IsCoreAssembly()
        {
            Assert.AreEqual("QL.Core", CoreInfo.Name);
        }
    }
}
