using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Proje ilk açıldığında Assets/Scenes/Saha.unity sahnesini oluşturur ve açar.
/// Sahnede yalnızca kamera, ışık ve "SahaKurucu" var; saha Play'e basınca koddan kurulur.
/// Menüden de çalıştırılabilir: Futbol > Saha sahnesini oluştur
/// </summary>
[InitializeOnLoad]
public static class FutbolSceneSetup
{
    const string ScenePath = "Assets/Scenes/Saha.unity";
    const string SessionKey = "Futbol_SceneSetupDone";

    static FutbolSceneSetup()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, true);
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(ScenePath)) CreateScene();
        };
    }

    [MenuItem("Futbol/Saha sahnesini oluştur")]
    public static void CreateScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Directory.CreateDirectory("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        new GameObject("SahaKurucu").AddComponent<PitchBuilder>();
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.Refresh();
        Debug.Log("Futbol: " + ScenePath + " hazır. Play'e bas.");
    }
}
