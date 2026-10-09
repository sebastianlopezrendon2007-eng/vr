using System.IO;
using Telemedicina;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ConsultorioEditor
{
    public const string ScenePath = "Assets/Scenes/Consultorio.unity";

    [InitializeOnLoadMethod]
    private static void PreparePlay()
    {
        if (Application.isBatchMode) return;
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (scene) EditorSceneManager.playModeStartScene = scene;
            string key = "Telemedicina.Opened." + Application.dataPath;
            if (!SessionState.GetBool(key, false))
            {
                SessionState.SetBool(key, true);
                var active = SceneManager.GetActiveScene();
                if (!active.isDirty && (active.path == "" || active.path == "Assets/Scenes/SampleScene.unity"))
                    EditorSceneManager.OpenScene(ScenePath);
            }
        };
    }

    [MenuItem("Telemedicina/Abrir consultorio")]
    public static void OpenScene()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
    }

    [MenuItem("Telemedicina/Crear aplicación Linux")]
    public static void BuildLinux()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Detén la prueba", "Pulsa Play para salir de la prueba antes de crear la aplicación.", "Aceptar"); return; }
        string folder = EditorUtility.SaveFolderPanel("Carpeta para la aplicación Linux", "", "TelemedicinaXR_Linux");
        if (string.IsNullOrEmpty(folder)) return;
        var report = Build(folder);
        if (report.summary.result == BuildResult.Succeeded)
            EditorUtility.RevealInFinder(Path.Combine(folder, "TelemedicinaXR.x86_64"));
        else EditorUtility.DisplayDialog("No se pudo crear", "Revisa los errores de la pestaña Console de Unity.", "Aceptar");
    }

    // Unity -batchmode -quit -projectPath ... -executeMethod ConsultorioEditor.BuildLinuxBatch
    public static void BuildLinuxBatch()
    {
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Linux"));
        Directory.CreateDirectory(folder);
        var report = Build(folder);
        if (report.summary.result != BuildResult.Succeeded) throw new System.Exception("La compilación Linux falló.");
    }
    private static BuildReport Build(string folder)
    {
        return BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath }, locationPathName = Path.Combine(folder, "TelemedicinaXR.x86_64"),
            target = BuildTarget.StandaloneLinux64, options = BuildOptions.None
        });
    }
}
