#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Open this folder as a Unity 2019.4 project (matching KSP 1.12), assign
/// Assets/Shaders/ProceduralBolt.shader to the thunderboltshaders asset bundle,
/// then use menu Thunderbolt → Build Shader Bundle.
/// Output: GameData/Thunderbolt/Shaders/thunderboltshaders.bundle
/// </summary>
public static class BuildThunderboltShaders
{
    private const string BundleName = "thunderboltshaders";

    [MenuItem("Thunderbolt/Build Shader Bundle")]
    public static void Build()
    {
        string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
        string outDir = Path.Combine(repoRoot, "GameData", "Thunderbolt", "Shaders");
        Directory.CreateDirectory(outDir);

        // Ensure shader in this Unity project is tagged for the bundle.
        string shaderAsset = "Assets/Shaders/ProceduralBolt.shader";
        var importer = AssetImporter.GetAtPath(shaderAsset);
        if (importer == null)
        {
            Debug.LogError("Missing " + shaderAsset + " — copy ProceduralBolt.shader into Assets/Shaders/ first.");
            return;
        }

        importer.assetBundleName = BundleName;
        importer.SaveAndReimport();

        var builds = new[]
        {
            new AssetBundleBuild
            {
                assetBundleName = BundleName + ".bundle",
                assetNames = new[] { shaderAsset }
            }
        };

        BuildPipeline.BuildAssetBundles(
            outDir,
            builds,
            BuildAssetBundleOptions.None,
            BuildTarget.StandaloneWindows64);

        Debug.Log("Built Thunderbolt shader bundle → " + outDir);
    }
}
#endif
