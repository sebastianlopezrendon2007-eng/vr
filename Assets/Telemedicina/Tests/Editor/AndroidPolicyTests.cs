using NUnit.Framework;
using Telemedicina.Editor;
using System.Xml;

namespace Telemedicina.Tests
{
    public class AndroidPolicyTests
    {
        [Test]
        public void KeepsUnityActivityAndDoesNotDuplicatePermissions()
        {
            const string xml = "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\"><application><activity android:name=\"UnityActivity\"/></application></manifest>";
            string patched = AndroidNetworkPolicy.Patch(xml);
            Assert.AreEqual(patched, AndroidNetworkPolicy.Patch(patched));
            var doc = new XmlDocument(); doc.LoadXml(patched);
            Assert.AreEqual(2, doc.SelectNodes("//uses-permission").Count);
            Assert.AreEqual(1, doc.SelectNodes("//activity").Count);
        }
        [Test]
        public void RejectsIncompleteManifest() { Assert.Throws<System.ArgumentException>(() => AndroidNetworkPolicy.Patch("<manifest/>")); }
        [Test]
        public void RestrictsHttpToLoopback()
        {
            var doc = new XmlDocument(); doc.LoadXml(AndroidNetworkPolicy.Config);
            Assert.AreEqual("false", doc.SelectSingleNode("//base-config").Attributes["cleartextTrafficPermitted"].Value);
            Assert.AreEqual(3, doc.SelectNodes("//domain").Count);
        }
    }
}
