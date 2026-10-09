using System;
using System.Linq;
using System.Xml;
using Telemedicina.Editor;

class Program
{
    static int checks;
    static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message); checks++;
    }
    static void Main()
    {
        const string ns = "http://schemas.android.com/apk/res/android";
        string original = "<manifest xmlns:android=\"" + ns + "\" package=\"mx.example\"><application><activity android:name=\"com.unity3d.player.UnityPlayerGameActivity\"/><meta-data android:name=\"com.oculus.supportedDevices\" android:value=\"quest3\"/></application></manifest>";
        var manifest = new XmlDocument(); manifest.LoadXml(AndroidNetworkPolicy.Patch(original));
        Check(manifest.DocumentElement.GetAttribute("package") == "mx.example", "Se alteró el paquete.");
        Check(((XmlElement)manifest.SelectSingleNode("/manifest/application/activity")).GetAttribute("name", ns) == "com.unity3d.player.UnityPlayerGameActivity", "Se perdió la actividad Unity.");
        Check(manifest.SelectNodes("/manifest/application/meta-data").Count == 1, "Se perdieron ajustes Quest.");
        Check(((XmlElement)manifest.SelectSingleNode("/manifest/application")).GetAttribute("networkSecurityConfig", ns) == "@xml/telemedicina_network_security", "No se enlazó la política.");
        Check(((XmlElement)manifest.SelectSingleNode("/manifest/application")).GetAttribute("usesCleartextTraffic", ns) == "false", "HTTP global permitido.");
        Check(manifest.SelectNodes("/manifest/uses-permission").Count == 2, "Permisos de llamada incompletos.");
        Check(((XmlElement)manifest.SelectSingleNode("/manifest/uses-permission")).GetAttribute("name", ns) == "android.permission.INTERNET", "Falta INTERNET.");
        Check(manifest.SelectNodes("/manifest/uses-permission").Cast<XmlElement>().Any(e=>e.GetAttribute("name", ns)=="android.permission.RECORD_AUDIO"), "Falta micrófono.");
        Check(manifest.SelectNodes("/manifest/uses-feature").Cast<XmlElement>().All(e=>e.GetAttribute("required",ns)=="false"), "Hardware opcional convertido en obligatorio.");
        string patched = manifest.OuterXml;
        Check(AndroidNetworkPolicy.Patch(patched) == patched, "La operación no es idempotente.");
        var network = new XmlDocument(); network.LoadXml(AndroidNetworkPolicy.Config);
        Check(((XmlElement)network.SelectSingleNode("/network-security-config/base-config")).GetAttribute("cleartextTrafficPermitted") == "false", "La política base permite HTTP.");
        Check(network.SelectNodes("/network-security-config/domain-config").Count == 1, "Se añadieron excepciones HTTP.");
        var domains = network.SelectNodes("//domain").Cast<XmlElement>().ToArray();
        Check(domains.Length == 3 && domains.Select(d => d.InnerText).OrderBy(x=>x).SequenceEqual(new[]{"localhost", "127.0.0.1", "::1"}.OrderBy(x=>x)), "Las excepciones no se limitan a loopback.");
        Check(domains.All(d => d.GetAttribute("includeSubdomains") == "false"), "Se habilitaron subdominios.");
        foreach (string invalid in new[]{"<manifest/>", "<otro/>"})
        {
            bool rejected = false; try { AndroidNetworkPolicy.Patch(invalid); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Un manifiesto incompleto fue aceptado.");
        }
        Console.WriteLine("Política Android: " + checks + " comprobaciones aprobadas.");
    }
}
