using UnityEditor;
using System.Linq;

/// <summary>
/// Script de build utilisé par le CI (game-ci/unity-builder) et en local.
/// Construit le jeu pour la plateforme cible spécifiée.
/// 
/// Utilisation en ligne de commande :
///   Unity -batchmode -quit -executeMethod BuildScript.BuildWindows
///   Unity -batchmode -quit -executeMethod BuildScript.BuildLinux
/// </summary>
public static class BuildScript
{
    /// <summary>
    /// Récupère toutes les scènes activées dans les Build Settings.
    /// </summary>
    private static string[] GetEnabledScenes() =>
        EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

    /// <summary>Build Windows (StandaloneWindows64).</summary>
    public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Build/Windows");

    /// <summary>Build Linux (StandaloneLinux64).</summary>
    public static void BuildLinux() => Build(BuildTarget.StandaloneLinux64, "Build/Linux");

    /// <summary>Build WebGL (conservé pour compatibilité).</summary>
    public static void PerformBuild() => Build(BuildTarget.WebGL, "Build/WebGL");

    /// <summary>
    /// Méthode de build générique.
    /// </summary>
    /// <param name="target">Plateforme cible.</param>
    /// <param name="outputPath">Dossier de sortie.</param>
    private static void Build(BuildTarget target, string outputPath)
    {
        BuildPipeline.BuildPlayer(
            GetEnabledScenes(),
            outputPath,
            target,
            BuildOptions.None
        );
    }
}
