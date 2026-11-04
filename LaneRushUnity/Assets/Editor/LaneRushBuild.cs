using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class LaneRushBuild
{
    [MenuItem("Lane Rush/Configure Android")]
    public static void ConfigureAndroid()
    {
        EnsureMaterials();
        Directory.CreateDirectory("Assets/Scenes");
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Main.unity");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Main.unity", true) };
        PlayerSettings.companyName = "Lane Rush";
        PlayerSettings.productName = "Lane Rush";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.lanerush.mobile");
        PlayerSettings.bundleVersion = "2.0.5";
        PlayerSettings.Android.bundleVersionCode = 13;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
        EditorUserBuildSettings.androidCreateSymbols = AndroidCreateSymbols.Public;
        PlayerSettings.Android.androidIsGame = true;
        PlayerSettings.Android.optimizedFramePacing = true;
        PlayerSettings.Android.startInFullscreen = true;
        PlayerSettings.gcIncremental = true;
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Medium);
        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = Path.GetFullPath("../lane_rush_android/android/app/upload-keystore.jks");
        PlayerSettings.Android.keystorePass = ReadProperty("../lane_rush_android/android/key.properties", "storePassword");
        PlayerSettings.Android.keyaliasName = "upload";
        PlayerSettings.Android.keyaliasPass = ReadProperty("../lane_rush_android/android/key.properties", "keyPassword");
        EditorUserBuildSettings.buildAppBundle = true;
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        AssetDatabase.SaveAssets();
    }

    private static void EnsureMaterials()
    {
        Directory.CreateDirectory("Assets/Resources/Materials");
        CreateMaterial("Road", new Color(0.075f, 0.085f, 0.09f), 0.28f);
        CreateMaterial("Grass", new Color(0.07f, 0.30f, 0.12f), 0.12f);
        CreateMaterial("White", new Color(0.92f, 0.94f, 0.88f), 0.4f);
        CreateMaterial("Curb", new Color(0.47f, 0.52f, 0.52f), 0.25f);
        CreateMaterial("GrassLight", new Color(0.085f, 0.315f, 0.125f), 0.08f);
        CreateMaterial("GrassDark", new Color(0.075f, 0.29f, 0.115f), 0.08f);
        string shadowPath = "Assets/Resources/Materials/ContactShadow.mat";
        Material shadow = AssetDatabase.LoadAssetAtPath<Material>(shadowPath);
        if (shadow == null)
        {
            Shader shadowShader = Shader.Find("Unlit/Transparent");
            if (shadowShader == null) throw new BuildFailedException("Unlit/Transparent shader is missing.");
            shadow = new Material(shadowShader);
            AssetDatabase.CreateAsset(shadow, shadowPath);
        }
        string skyPath = "Assets/Resources/Materials/Sky.mat";
        Material sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
        if (sky == null)
        {
            Shader skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader == null) throw new BuildFailedException("Procedural sky shader is missing.");
            sky = new Material(skyShader);
            AssetDatabase.CreateAsset(sky, skyPath);
        }
        sky.SetColor("_SkyTint", new Color(0.36f, 0.59f, 0.88f));
        sky.SetColor("_GroundColor", new Color(0.43f, 0.57f, 0.69f));
        sky.SetFloat("_SunDisk", 0f);
        sky.SetFloat("_AtmosphereThickness", 0.48f);
        sky.SetFloat("_Exposure", 0.82f);
        EditorUtility.SetDirty(sky);
        Material amber = CreateMaterial("Amber", new Color(1f, 0.48f, 0.04f), 0.35f);
        amber.EnableKeyword("_EMISSION");
        amber.SetColor("_EmissionColor", new Color(1f, 0.25f, 0.01f) * 2f);
        EditorUtility.SetDirty(amber);
        Material boostGlow = CreateMaterial("BoostGlow", new Color(0.12f, 0.82f, 1f), 0.5f);
        boostGlow.EnableKeyword("_EMISSION");
        boostGlow.SetColor("_EmissionColor", new Color(0.1f, 0.82f, 1f) * 2.5f);
        EditorUtility.SetDirty(boostGlow);
        AssetDatabase.SaveAssets();
    }

    private static Material CreateMaterial(string name, Color color, float smoothness)
    {
        string path = $"Assets/Resources/Materials/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Standard");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        material.SetFloat("_Glossiness", smoothness);
        EditorUtility.SetDirty(material);
        return material;
    }

    public static void AuditAssets()
    {
        long vertices = 0;
        long triangles = 0;
        int renderers = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/Resources/Models" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) continue;
            long modelVertices = 0;
            long modelTriangles = 0;
            foreach (MeshFilter filter in asset.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                modelVertices += filter.sharedMesh.vertexCount;
                modelTriangles += filter.sharedMesh.triangles.Length / 3;
            }
            int modelRenderers = asset.GetComponentsInChildren<Renderer>(true).Length;
            vertices += modelVertices;
            triangles += modelTriangles;
            renderers += modelRenderers;
            Debug.Log($"ASSET_AUDIT {Path.GetFileName(path)} vertices={modelVertices} triangles={modelTriangles} renderers={modelRenderers}");
        }
        Debug.Log($"ASSET_AUDIT TOTAL vertices={vertices} triangles={triangles} renderers={renderers}");
    }

    public static void AuditPlayerOrientation()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Models/wings_of_freedom_car.glb");
        if (prefab == null) throw new BuildFailedException("Player GLB was not imported.");
        var rotations = new[]
        {
            new Vector3(0, 0, 0), new Vector3(0, 90, 0), new Vector3(0, -90, 0),
            new Vector3(90, 0, 0), new Vector3(-90, 0, 0),
            new Vector3(90, 90, 0), new Vector3(-90, 90, 0),
            new Vector3(90, -90, 0), new Vector3(-90, -90, 0),
            new Vector3(0, 0, 90), new Vector3(0, 0, -90)
        };
        foreach (Vector3 rotation in rotations)
        {
            GameObject instance = Object.Instantiate(prefab);
            instance.transform.rotation = Quaternion.Euler(rotation);
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            Debug.Log($"ORIENTATION_AUDIT euler={rotation} size={bounds.size}");
            Object.DestroyImmediate(instance);
        }
    }

    public static void BuildAab()
    {
        ConfigureAndroid();
        Directory.CreateDirectory("Builds/Android");
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EnabledScenes(),
            locationPathName = "Builds/Android/Lane-Rush-2.0.5.aab",
            target = BuildTarget.Android,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException($"Android build failed: {report.summary.result}");
    }

    public static void BuildApk()
    {
        ConfigureAndroid();
        EditorUserBuildSettings.buildAppBundle = false;
        Directory.CreateDirectory("Builds/Android");
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EnabledScenes(),
            locationPathName = "Builds/Android/Lane-Rush-2.0.5.apk",
            target = BuildTarget.Android,
            options = BuildOptions.Development
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException($"Android APK build failed: {report.summary.result}");
    }

    private static string[] EnabledScenes()
    {
        var scenes = new System.Collections.Generic.List<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            if (scene.enabled) scenes.Add(scene.path);
        return scenes.ToArray();
    }

    private static string ReadProperty(string path, string key)
    {
        foreach (string line in File.ReadAllLines(path))
            if (line.StartsWith(key + "=")) return line[(key.Length + 1)..].Trim();
        return string.Empty;
    }
}
