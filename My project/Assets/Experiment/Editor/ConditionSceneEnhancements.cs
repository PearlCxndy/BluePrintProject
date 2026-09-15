using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Scoped, repeatable upgrades to saved scenes, without rebuilding their existing geometry.</summary>
public static class ConditionSceneEnhancements
{
    public const string NatureSky = "Assets/SkySeries Freebie/6SidedFluffball.mat";
    public const string UrbanSky = "Assets/SkySeries Freebie/DayInTheClouds.mat";
    const string TreesPath = "Assets/Stylized Nature Environment/Prefabs/";
    const string GroveName = "Additional lakeside grove";
    const string WaterPath = "Assets/Experiment/EnvironmentMaterials/Animated lake.mat";

    [MenuItem("Experiment/Scenes/Apply Skyboxes Only")]
    public static void ApplySkyboxes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before saving skyboxes.");
        var skies = new[] { RequireSky(NatureSky), RequireSky(UrbanSky) };
        var paths = new[] { ConditionSceneBuilder.NaturePath, ConditionSceneBuilder.UrbanPath };
        foreach (var path in paths)
        {
            var loaded = SceneManager.GetSceneByPath(path);
            if (loaded.IsValid() && loaded.isLoaded && loaded.isDirty)
                throw new InvalidOperationException("Save your open scene before applying skyboxes: " + path);
        }
        var backup = "Temp/ConditionSceneBackups/" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        Directory.CreateDirectory(backup);
        foreach (var path in paths) File.Copy(path, Path.Combine(backup, Path.GetFileName(path)));
        var previous = SceneManager.GetActiveScene();
        for (var i = 0; i < paths.Length; i++)
        {
            var scene = SceneManager.GetSceneByPath(paths[i]);
            var alreadyOpen = scene.IsValid() && scene.isLoaded;
            if (!alreadyOpen) scene = EditorSceneManager.OpenScene(paths[i], OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                RenderSettings.skybox = skies[i];
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save skybox: " + paths[i]);
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true);
            }
        }
        SceneView.RepaintAll();
    }

    [MenuItem("Experiment/Scenes/Apply Skyboxes, Trees and Motion")]
    public static void ApplyToSavedScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before editing saved scenes.");
        RequireSky(NatureSky);
        RequireSky(UrbanSky);
        var paths = new[] { ConditionSceneBuilder.NaturePath, ConditionSceneBuilder.UrbanPath };
        // Do not save over uncommitted scene changes.
        foreach (var path in paths)
        {
            var loaded = SceneManager.GetSceneByPath(path);
            if (loaded.IsValid() && loaded.isLoaded && loaded.isDirty)
                throw new InvalidOperationException("Save your open scene before applying this update: " + path);
        }
        var backup = "Temp/ConditionSceneBackups/" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(backup);
        var previous = SceneManager.GetActiveScene();
        var report = new List<string>();
        foreach (var path in paths)
        {
            File.Copy(path, Path.Combine(backup, Path.GetFileName(path)), true);
            var scene = SceneManager.GetSceneByPath(path);
            var alreadyOpen = scene.IsValid() && scene.isLoaded;
            if (!alreadyOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var environment = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ConditionEnvironment>()).Single();
                ApplyToEnvironment(environment);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                var motion = environment.GetComponent<NatureAmbientMotion>();
                report.Add(scene.name + ": sky=" + RenderSettings.skybox.name + "; mouse=" + environment.look.mouseSensitivity +
                    (motion != null ? "; trees=" + motion.trees.Length + "; tree sway + animated lake" : "; no ambient animation"));
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true);
            }
        }
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Temp/ExperimentPreviews");
        report.Add("Pre-update scene copies: " + backup);
        File.WriteAllLines("Temp/ExperimentPreviews/enhancement-report.txt", report);
    }

    public static void ApplyToEnvironment(ConditionEnvironment environment)
    {
        RenderSettings.skybox = RequireSky(environment.condition == ConditionType.Nature ? NatureSky : UrbanSky);
        environment.viewCamera.clearFlags = CameraClearFlags.Skybox;
        environment.look.mouseSensitivity = 2.5f;
        if (environment.condition != ConditionType.Nature) return;

        var root = environment.transform;
        var grove = root.Find(GroveName);
        if (grove == null)
        {
            grove = new GameObject(GroveName).transform;
            grove.SetParent(root, false);
            AddTrees(root, grove);
        }
        var motion = environment.GetComponent<NatureAmbientMotion>();
        if (motion == null) motion = environment.gameObject.AddComponent<NatureAmbientMotion>();
        motion.trees = root.GetComponentsInChildren<Transform>().Where(IsTree).ToArray();
        var lake = root.Find("Blue lake");
        if (lake == null) throw new InvalidOperationException("The Blue lake object was not found.");
        motion.lake = lake.GetComponent<Renderer>();
        var material = AssetDatabase.LoadAssetAtPath<Material>(WaterPath);
        if (material == null)
        {
            var shader = Shader.Find("Experiment/Lake Ripples");
            if (shader == null) throw new InvalidOperationException("Import the LakeRipples shader before applying the scene update.");
            material = new Material(shader) { name = "Animated lake" };
            AssetDatabase.CreateAsset(material, WaterPath);
        }
        motion.lake.sharedMaterial = material;
        EditorUtility.SetDirty(environment.look);
        EditorUtility.SetDirty(motion);
    }

    static bool IsTree(Transform t) { return t.name.StartsWith("tree_", StringComparison.Ordinal); }

    static void AddTrees(Transform root, Transform grove)
    {
        var existing = root.GetComponentsInChildren<Transform>().Where(IsTree).Select(t => t.position).ToList();
        var random = new System.Random(24092026);
        var added = 0;
        // Fill the near/middle distance while keeping the lake, footpath and viewing position clear.
        for (var attempt = 0; attempt < 6000 && added < 120; attempt++)
        {
            var angle = (float)(random.NextDouble() * Math.PI * 2);
            var radius = added < 65 ? 28 + (float)random.NextDouble() * 22 : 48 + (float)random.NextDouble() * 33;
            var p = new Vector3(5 + Mathf.Sin(angle) * radius, 0, 17 + Mathf.Cos(angle) * radius);
            if (Mathf.Pow((p.x - 5) / 23, 2) + Mathf.Pow((p.z - 17) / 30, 2) < 1) continue;
            var pathX = -18 + Mathf.Sin((p.z + 25) / 2.6f * .14f) * 4;
            if (p.z > -28 && p.z < 70 && Mathf.Abs(p.x - pathX) < 4) continue;
            if (Vector3.Distance(p, new Vector3(0, 0, -12)) < 11) continue;
            if (existing.Any(v => (v - p).sqrMagnitude < 14)) continue;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TreesPath + "tree_" + (char)('a' + added % 11) + ".prefab");
            if (prefab == null) throw new InvalidOperationException("Missing Nature tree prefab.");
            var tree = (GameObject)PrefabUtility.InstantiatePrefab(prefab, grove);
            tree.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0, (float)random.NextDouble() * 360, 0));
            var bounds = TreeBounds(tree);
            var height = 7 + (float)random.NextDouble() * 5;
            tree.transform.localScale *= height / Mathf.Max(.01f, bounds.size.y);
            bounds = TreeBounds(tree);
            tree.transform.position += p - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            existing.Add(p);
            added++;
        }
        if (added != 120) throw new InvalidOperationException("Could not place all extra trees with the required clearance.");
    }

    static Bounds TreeBounds(GameObject tree)
    {
        var renderers = tree.GetComponentsInChildren<Renderer>();
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }
    static Material RequireSky(string path)
    {
        var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (sky == null) throw new InvalidOperationException("Missing skybox material: " + path);
        return sky;
    }
}
