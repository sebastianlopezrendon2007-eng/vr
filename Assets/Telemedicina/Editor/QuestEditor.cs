using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace Telemedicina.Editor
{
    public static class QuestEditor
    {
        public const string ScenePath = "Assets/Scenes/Consultorio.unity";
        public const string PackageId = "mx.telemedicina.consultorio.beta";
        public static string ApkPath => Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Quest3/ConsultorioQuest3.apk"));
        private static readonly string[] RequiredFeatures = { "MetaQuestFeature", "ARSessionFeature", "ARCameraFeature", "HandInteractionProfile", "OculusTouchControllerProfile", "MetaQuestTouchPlusControllerProfile" };

        [MenuItem("Telemedicina/Quest 3/1. Preparar Android")]
        public static void PrepareMenu()
        {
            PrepareBatch();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        }
        public static void PrepareBatch()
        {
            string expectedVersion = File.ReadAllLines("ProjectSettings/ProjectVersion.txt")
                .First(line => line.StartsWith("m_EditorVersion:")).Split(':')[1].Trim();
            if (Application.unityVersion != expectedVersion)
                throw new BuildFailedException("Abre este proyecto con Unity " + expectedVersion + ". Versión actual: " + Application.unityVersion);
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new BuildFailedException("Instala Android Build Support, Android SDK & NDK Tools y OpenJDK en Unity Hub para " + Application.unityVersion + ".");
            Directory.CreateDirectory("Assets/XR/Settings"); AssetDatabase.Refresh();
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey, out XRGeneralSettingsPerBuildTarget all);
            if (!all)
            {
                all = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>("Assets/XR/Settings/QuestXR.asset");
                if (!all)
                {
                    all = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                    AssetDatabase.CreateAsset(all, "Assets/XR/Settings/QuestXR.asset");
                }
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, all, true);
            }
            if (!all.SettingsForBuildTarget(BuildTargetGroup.Android)) all.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
            if (!all.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android)) all.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            var general = all.SettingsForBuildTarget(BuildTargetGroup.Android);
            general.InitManagerOnStart = true;
            general.Manager.automaticLoading = true; general.Manager.automaticRunning = true;
            if (!XRPackageMetadataStore.AssignLoader(general.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Android))
                throw new BuildFailedException("No se pudo habilitar el proveedor OpenXR para Android.");
            // Esta API pública crea los ajustes internamente antes de refrescar las funciones.
            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
            var xr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (!xr) throw new BuildFailedException("No se crearon los ajustes OpenXR Android.");
            var features = xr.GetFeatures<OpenXRFeature>();
            foreach (string name in RequiredFeatures)
            {
                var feature = features.FirstOrDefault(f => f && f.GetType().Name == name);
                if (!feature) throw new BuildFailedException("Falta " + name + ". Espera a que terminen de importarse los paquetes.");
                feature.enabled = true;
                if (name == "ARCameraFeature")
                {
                    var serialized = new SerializedObject(feature);
                    var capture = serialized.FindProperty("m_CameraImageSupport");
                    if (capture != null) { capture.boolValue = false; serialized.ApplyModifiedPropertiesWithoutUndo(); }
                }
                EditorUtility.SetDirty(feature);
            }
            xr.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            EditorUtility.SetDirty(xr); EditorUtility.SetDirty(general); EditorUtility.SetDirty(general.Manager); EditorUtility.SetDirty(all);
            PlayerSettings.companyName = "Telemedicina"; PlayerSettings.productName = "Consultorio Quest";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageId);
            PlayerSettings.bundleVersion = "0.3.0"; PlayerSettings.Android.bundleVersionCode = 3;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.Android.optimizedFramePacing = false;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.insecureHttpOption = InsecureHttpOption.DevelopmentOnly;
            PlayerSettings.runInBackground = false;
            EditorUserBuildSettings.buildAppBundle = false;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (string path in new[] { "Assets/Settings/PC_RPAsset.asset", "Assets/Settings/Mobile_RPAsset.asset" })
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(path); if (!asset) continue;
                var serialized = new SerializedObject(asset);
                var hdr = serialized.FindProperty("m_SupportsHDR"); if (hdr != null) hdr.boolValue = false;
                var alpha = serialized.FindProperty("m_AllowPostProcessAlphaOutput"); if (alpha != null) alpha.boolValue = true;
                var samples = serialized.FindProperty("m_MSAA"); if (samples != null) samples.intValue = 2;
                serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(asset);
            }
            EnsureArBackgroundRenderer("Assets/Settings/Mobile_Renderer.asset");
            AssetDatabase.SaveAssets(); ValidateConfig();
            Debug.Log("QUEST_CONFIG_OK: Android, ARM64, IL2CPP, OpenXR, Touch Plus, passthrough.");
        }
        private static void EnsureArBackgroundRenderer(string path)
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (!renderer) throw new BuildFailedException("No se encontró el renderer URP " + path);
            var existing = renderer.rendererFeatures.FirstOrDefault(f => f && f.GetType().Name == "ARBackgroundRendererFeature");
            if (existing)
            {
                var serialized = new SerializedObject(existing);
                var active = serialized.FindProperty("m_Active");
                if (active != null) active.boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(existing);
                EditorUtility.SetDirty(renderer); return;
            }
            var feature = ScriptableObject.CreateInstance<ARBackgroundRendererFeature>();
            feature.name = "AR Background Renderer Feature";
            AssetDatabase.AddObjectToAsset(feature, renderer);
            renderer.rendererFeatures.Add(feature);
            EditorUtility.SetDirty(feature); EditorUtility.SetDirty(renderer);
        }
        private static bool HasActiveArBackgroundRenderer()
        {
            var quality = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
            if (!quality) return false;
            var pipeline = new SerializedObject(quality);
            var renderers = pipeline.FindProperty("m_RendererDataList");
            if (renderers == null || renderers.arraySize == 0 ||
                !(renderers.GetArrayElementAtIndex(0).objectReferenceValue is UniversalRendererData renderer)) return false;
            return renderer.rendererFeatures.Any(f => f && f.isActive && f.GetType().Name == "ARBackgroundRendererFeature");
        }
        [MenuItem("Telemedicina/Quest 3/2. Crear APK")]
        public static void BuildMenu()
        {
            if (EditorApplication.isPlaying) throw new BuildFailedException("Detén Play antes de compilar.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new BuildFailedException("Primero pulsa Quest 3 > 1. Preparar Android y espera el cambio de plataforma.");
            BuildAndroidBatch(); EditorUtility.RevealInFinder(ApkPath);
        }
        public static void BuildAndroidBatch()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new BuildFailedException("La compilación por terminal necesita -buildTarget Android.");
            PrepareBatch(); Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = ApkPath,
                target = BuildTarget.Android, options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded || !File.Exists(ApkPath))
                throw new BuildFailedException("No se generó el APK. Revisa Logs/android-build.log o Console.");
            Debug.Log("QUEST_APK_OK: " + ApkPath);
        }
        public static void ValidateConfig()
        {
            if (PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) != PackageId ||
                PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP ||
                PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64 ||
                (int)PlayerSettings.Android.minSdkVersion < 32 || EditorUserBuildSettings.buildAppBundle)
                throw new BuildFailedException("Configuración Android incompleta. Usa Quest 3 > 1. Preparar Android.");
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey, out XRGeneralSettingsPerBuildTarget all);
            var general = all ? all.SettingsForBuildTarget(BuildTargetGroup.Android) : null;
            if (!general || !general.InitManagerOnStart || !general.Manager ||
                !general.Manager.activeLoaders.Any(l => l && l.GetType().FullName == "UnityEngine.XR.OpenXR.OpenXRLoader"))
                throw new BuildFailedException("OpenXR no está habilitado al inicio de Android.");
            var xr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (!xr || xr.renderMode != OpenXRSettings.RenderMode.SinglePassInstanced ||
                RequiredFeatures.Any(name => !xr.GetFeatures<OpenXRFeature>().Any(f => f && f.enabled && f.GetType().Name == name)) ||
                !HasActiveArBackgroundRenderer())
                throw new BuildFailedException("Faltan funciones OpenXR/Meta/Touch Plus o el render estéreo.");
        }
    }
    public sealed class QuestBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => -500;
        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.Android)
            {
                QuestEditor.ValidateConfig();
                if ((report.summary.options & BuildOptions.Development) == 0)
                    throw new BuildFailedException("Esta entrega requiere Development Build para la conexión de pruebas por USB.");
            }
        }
    }
}
