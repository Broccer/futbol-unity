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

        PlayerSettings.productName = "Futbol";
        PlayerSettings.companyName = "Bavsla Games";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback = false;
        PlayerSettings.WebGL.dataCaching = true;

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

    static string ArgValue(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }
}
