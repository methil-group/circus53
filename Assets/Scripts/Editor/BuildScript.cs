using UnityEditor;
using System.Linq;

public static class BuildScript
{
    public static void PerformBuild()
    {
        // Récupère toutes les scènes activées dans les Build Settings
        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        // Dossier de sortie pour le build WebGL
        string outputPath = "Build/WebGL";

        // Lance le build en mode batch
        BuildPipeline.BuildPlayer(
            scenes,
            outputPath,
            BuildTarget.WebGL,
            BuildOptions.None
        );
    }
}
