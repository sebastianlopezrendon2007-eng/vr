using System.Xml;

namespace Telemedicina.Editor
{
    // Código sin dependencias del motor para verificar la política de red por separado.
    public static class AndroidNetworkPolicy
    {
        private const string Android = "http://schemas.android.com/apk/res/android";
        public const string Config = "<network-security-config><base-config cleartextTrafficPermitted=\"false\"/><domain-config cleartextTrafficPermitted=\"true\"><domain includeSubdomains=\"false\">localhost</domain><domain includeSubdomains=\"false\">127.0.0.1</domain><domain includeSubdomains=\"false\">::1</domain></domain-config></network-security-config>";
        public static string Patch(string xml)
        {
            var doc = new XmlDocument(); doc.LoadXml(xml);
            var root = doc.DocumentElement;
            if (root == null || root.Name != "manifest") throw new System.ArgumentException("No es un AndroidManifest válido.");
            var app = root.SelectSingleNode("application") as XmlElement;
            if (app == null) throw new System.ArgumentException("Falta application en AndroidManifest.");
            app.SetAttribute("networkSecurityConfig", Android, "@xml/telemedicina_network_security");
            app.SetAttribute("usesCleartextTraffic", Android, "false");
            foreach (string name in new[] { "android.permission.INTERNET", "android.permission.RECORD_AUDIO" })
            {
                bool found = false;
                foreach (XmlNode node in root.SelectNodes("uses-permission"))
                    if (node is XmlElement element && element.GetAttribute("name", Android) == name) found = true;
                if (!found)
                {
                    var permission = doc.CreateElement("uses-permission"); permission.SetAttribute("name", Android, name);
                    root.InsertBefore(permission, app);
                }
            }
            foreach (string name in new[] { "android.hardware.camera", "android.hardware.microphone" })
            {
                XmlElement feature = null;
                foreach (XmlNode node in root.SelectNodes("uses-feature"))
                    if (node is XmlElement element && element.GetAttribute("name", Android) == name) feature = element;
                if (feature == null) { feature = doc.CreateElement("uses-feature"); feature.SetAttribute("name", Android, name); root.InsertBefore(feature, app); }
                feature.SetAttribute("required", Android, "false");
            }
            return doc.OuterXml;
        }
    }
}
