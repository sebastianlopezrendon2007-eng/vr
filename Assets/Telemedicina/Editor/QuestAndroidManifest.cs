#if UNITY_ANDROID
using System.IO;
using UnityEditor.Android;

namespace Telemedicina.Editor
{
    public sealed class QuestAndroidManifest : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 1000;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string main = Path.Combine(path, "src/main");
            string manifest = Path.Combine(main, "AndroidManifest.xml");
            File.WriteAllText(manifest, AndroidNetworkPolicy.Patch(File.ReadAllText(manifest)));
            Directory.CreateDirectory(Path.Combine(main, "res/xml"));
            File.WriteAllText(Path.Combine(main, "res/xml/telemedicina_network_security.xml"), AndroidNetworkPolicy.Config);
        }
    }
}
#endif
