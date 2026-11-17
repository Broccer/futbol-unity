using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// GitHub Actions (game-ci) bu metodu çağırır. GitHub Pages sıkıştırılmış .br/.gz
/// dosyaları için gerekli başlıkları göndermediğinden sıkıştırmayı kapatıyoruz.
/// </summary>
public static class WebGLBuild
{
    const string ScenePath = "Assets/Scenes/Saha.unity";

    public static void Build()
    {
        if (!System.IO.File.Exists(ScenePath)) FutbolSceneSetup.CreateScene();
        EnsureBaseMaterial();

        PlayerSettings.productName = "Futbol";
        PlayerSettings.companyName = "Bavsla Games";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback = false;
        PlayerSettings.WebGL.dataCaching = true;
        // Sahne koddan kurulduğu için motor sınıfları (SphereCollider vb.) atılmasın
        PlayerSettings.stripEngineCode = false;

        string outPath = ArgValue("-customBuildPath") ?? "build/WebGL/WebGL";
        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = outPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"WebGL derleme sonucu: {report.summary.result}, {report.summary.totalSize} bayt → {outPath}");
        if (Application.isBatchMode)
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    /// <summary>
    /// Malzemeler koddan üretiliyor; sahnede hiçbir asset Standard shader'ı göstermediği için
    /// derleme onu atıyordu ve her şey pembe çıkıyordu. Resources'a bir malzeme koyup
    /// shader'ı ayrıca "Always Included Shaders" listesine ekliyoruz.
    /// </summary>
    public static void EnsureBaseMaterial()
    {
        const string path = "Assets/Resources/FutbolBase.mat";
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline == null || shader == null)
            shader = Shader.Find("Standard");

        if (AssetDatabase.LoadAssetAtPath<Material>(path) == null)
        {
            System.IO.Directory.CreateDirectory("Assets/Resources");
            var mat = new Material(shader) { name = "FutbolBase" };
            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
        }

        var gs = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("ProjectSettings/GraphicsSettings.asset");
        if (gs != null && shader != null)
        {
            var so = new SerializedObject(gs);
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            bool found = false;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) found = true;
            if (!found)
            {
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
                so.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
            }
        }
        Debug.Log("Futbol: temel malzeme shader'ı = " + (shader ? shader.name : "YOK"));
    }

    static string ArgValue(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }
}
