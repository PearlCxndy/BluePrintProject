using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Creates editable scenes from installed assets. Only creates missing files automatically.</summary>
public static class ConditionSceneBuilder
{
    public const string NaturePath = "Assets/Experiment/Scenes/ConditionA_Nature.unity";
    public const string UrbanPath = "Assets/Experiment/Scenes/ConditionB_Urban.unity";
    const string MaterialFolder = "Assets/Experiment/EnvironmentMaterials";
    const string NaturePrefabs = "Assets/Stylized Nature Environment/Prefabs/";
    const string CityPrefabs = "Assets/WhiteCity/Prefabs/Buildings/";

    [InitializeOnLoadMethod]
    static void ScheduleBuild() { EditorApplication.delayCall += BuildMissing; }

    [MenuItem("Experiment/Scenes/Create Missing Condition Scenes")]
    public static void BuildMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        if (!Directory.Exists(NaturePrefabs) || !Directory.Exists(CityPrefabs)) return;
        EnsureFolder("Assets/Experiment/Scenes");
        EnsureFolder(MaterialFolder);
        if (!File.Exists(NaturePath)) Build(ConditionType.Nature, NaturePath);
        if (!File.Exists(UrbanPath)) Build(ConditionType.City, UrbanPath);
        RegisterBuildScenes();
    }

    [MenuItem("Experiment/Scenes/Open Nature")]
    static void OpenNature() { Open(NaturePath); }
    [MenuItem("Experiment/Scenes/Open Urban")]
    static void OpenUrban() { Open(UrbanPath); }
    static void Open(string path)
    {
        BuildMissing();
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(path);
    }

    static void Build(ConditionType condition, string path)
    {
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var nature = condition == ConditionType.Nature;
            var root = new GameObject(nature ? "Condition A · Lakeside" : "Condition B · Urban courtyard");
            var environment = root.AddComponent<ConditionEnvironment>();
            environment.condition = condition;
            var cameraObject = new GameObject("Seated viewpoint", typeof(Camera), typeof(AudioListener), typeof(ConditionLook));
            cameraObject.transform.SetParent(root.transform);
            cameraObject.transform.position = new Vector3(0, 1.65f, nature ? -12 : -20);
            var camera = cameraObject.GetComponent<Camera>();
            cameraObject.tag = "MainCamera";
            camera.nearClipPlane = .08f;
            camera.farClipPlane = 350;
            camera.fieldOfView = 68;
            camera.clearFlags = CameraClearFlags.Skybox;
            environment.viewCamera = camera;
            environment.look = cameraObject.GetComponent<ConditionLook>();

            var sunObject = new GameObject("Daylight", typeof(Light));
            sunObject.transform.SetParent(root.transform);
            sunObject.transform.rotation = Quaternion.Euler(42, -32, 0);
            var sun = sunObject.GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = nature ? 1.05f : .88f;
            sun.color = nature ? new Color(1, .95f, .84f) : new Color(.95f, .97f, 1);
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = nature ? new Color(.66f, .77f, .84f) : new Color(.65f, .68f, .72f);
            RenderSettings.ambientEquatorColor = new Color(.48f, .51f, .49f);
            RenderSettings.ambientGroundColor = new Color(.22f, .25f, .22f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 85;
            RenderSettings.fogEndDistance = 265;
            RenderSettings.fogColor = nature ? new Color(.65f, .8f, .83f) : new Color(.72f, .75f, .78f);
            var skyPath = MaterialFolder + (nature ? "/NatureSky.mat" : "/UrbanSky.mat");
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                sky.SetColor("_SkyTint", nature ? new Color(.56f, .69f, .8f) : new Color(.65f, .67f, .7f));
                sky.SetColor("_GroundColor", new Color(.38f, .42f, .42f));
                sky.SetFloat("_AtmosphereThickness", nature ? .8f : 1.15f);
                sky.SetFloat("_Exposure", 1.1f);
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            RenderSettings.skybox = sky;
            if (nature) PopulateNature(root.transform); else PopulateUrban(root.transform);
            ConditionSceneEnhancements.ApplyToEnvironment(environment);
            EditorSceneManager.SaveScene(scene, path);
        }
        finally
        {
            SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Created experiment scene: " + path);
    }

    static void PopulateNature(Transform root)
    {
        var grass = Material("Meadow", "64854D");
        var hill = Material("Hills", "587747");
        var path = Material("Sandstone path", "C2B891");
        var bank = Material("Lake bank", "8C9970");
        var water = Material("Blue water", "398FAD", .65f);
        var rock = Material("River stones", "8A9688");
        Primitive(root, "Meadow", PrimitiveType.Cube, new Vector3(0, -.5f, 30), new Vector3(260, 1, 260), grass);
        // Elliptical shore with an open foreground. A level surface keeps the seated view stable.
        Primitive(root, "Curved shoreline", PrimitiveType.Cylinder, new Vector3(5, .015f, 17), new Vector3(39, .02f, 52), bank);
        Primitive(root, "Blue lake", PrimitiveType.Cylinder, new Vector3(5, .05f, 17), new Vector3(36, .016f, 49), water);
        for (var i = 0; i < 36; i++)
        {
            var z = -25 + i * 2.6f;
            var x = -18 + Mathf.Sin(i * .14f) * 4;
            Primitive(root, "Curving footpath " + i, PrimitiveType.Cylinder, new Vector3(x, .018f, z), new Vector3(4, .015f, 4), path);
        }
        var random = new System.Random(23092026);
        var trees = new[] { "tree_a", "tree_b", "tree_c", "tree_d", "tree_e", "tree_f", "tree_g", "tree_h", "tree_i", "tree_j", "tree_k" };
        for (var i = 0; i < 80; i++)
        {
            var angle = (float)(random.NextDouble() * Math.PI * 2);
            var radius = 32 + (float)random.NextDouble() * 65;
            var position = new Vector3(Mathf.Sin(angle) * radius, 0, 16 + Mathf.Cos(angle) * radius);
            var height = 6 + (float)random.NextDouble() * 6;
            PlacePrefab(NaturePrefabs + trees[i % trees.Length] + ".prefab", root, position, height, (float)random.NextDouble() * 360);
        }
        for (var i = 0; i < 10; i++)
        {
            var angle = i * Mathf.PI * 2 / 10;
            Primitive(root, "Rounded hillside " + i, PrimitiveType.Sphere,
                new Vector3(Mathf.Sin(angle) * 105, -6, 20 + Mathf.Cos(angle) * 105), new Vector3(75, 33 + i % 3 * 8, 65), hill);
        }
        for (var i = 0; i < 22; i++)
        {
            var angle = i * Mathf.PI * 2 / 22;
            var stone = Primitive(root, "Shore stone " + i, PrimitiveType.Sphere,
                new Vector3(5 + Mathf.Sin(angle) * 19.2f, .2f, 17 + Mathf.Cos(angle) * 25.5f), new Vector3(1.4f, .8f, 1.05f), rock);
            stone.transform.rotation = Quaternion.Euler(0, i * 37, 13);
        }
    }

    static void PopulateUrban(Transform root)
    {
        var ground = Material("Urban pavement", "8B8E90");
        var road = Material("Asphalt", "45494D");
        var curb = Material("Concrete", "B4B6B4");
        var marking = Material("Road markings", "D2D1C9");
        var metal = Material("Street furniture", "51585D");
        Primitive(root, "City ground", PrimitiveType.Cube, new Vector3(0, -.5f, 30), new Vector3(240, 1, 260), ground);
        Primitive(root, "Straight avenue", PrimitiveType.Cube, new Vector3(0, .015f, 25), new Vector3(13, .03f, 235), road);
        for (var side = -1; side <= 1; side += 2)
        {
            Primitive(root, "Raised pavement", PrimitiveType.Cube, new Vector3(side * 11, .08f, 25), new Vector3(8, .16f, 235), curb);
            for (var i = 0; i < 10; i++)
            {
                var id = new[] { "1", "2", "3", "4", "5", "6", "7", "10", "11", "12" }[i];
                PlacePrefab(CityPrefabs + id + ".prefab", root, new Vector3(side * 28, 0, -58 + i * 23), 17 + i % 4 * 3, side < 0 ? 90 : -90, 20);
                Primitive(root, "Lamp post", PrimitiveType.Cube, new Vector3(side * 8.6f, 2.9f, -42 + i * 21), new Vector3(.16f, 5.8f, .16f), metal);
                Primitive(root, "Lamp head", PrimitiveType.Cube, new Vector3(side * 8.2f, 5.8f, -42 + i * 21), new Vector3(1.2f, .18f, .45f), marking);
            }
        }
        for (var i = 0; i < 30; i++)
            Primitive(root, "Lane marking " + i, PrimitiveType.Cube, new Vector3(0, .038f, -80 + i * 8), new Vector3(.16f, .01f, 3.3f), marking);
        for (var i = 0; i < 4; i++)
        {
            PlacePrefab(CityPrefabs + (i + 1) + ".prefab", root, new Vector3(-35 + i * 23, 0, 165), 26, 180, 20);
            Primitive(root, "Concrete seating " + i, PrimitiveType.Cube, new Vector3(-11, .3f, -12 + i * 27), new Vector3(1.5f, .6f, 3), metal);
        }
    }

    static GameObject PlacePrefab(string path, Transform parent, Vector3 position, float height, float yaw, float maxWidth = float.PositiveInfinity)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new InvalidOperationException("Missing required asset: " + path);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0, yaw, 0));
        var bounds = BoundsOf(instance);
        var scale = Mathf.Min(height / Mathf.Max(.01f, bounds.size.y), maxWidth / Mathf.Max(.01f, Mathf.Max(bounds.size.x, bounds.size.z)));
        instance.transform.localScale *= scale;
        bounds = BoundsOf(instance);
        instance.transform.position += position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        return instance;
    }

    static Bounds BoundsOf(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) throw new InvalidOperationException("No geometry in " + go.name);
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }
    static GameObject Primitive(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }
    static Material Material(string name, string hex, float smoothness = .05f)
    {
        var path = MaterialFolder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        ColorUtility.TryParseHtmlString("#" + hex, out var color);
        material = new Material(Shader.Find("Standard")) { name = name, color = color };
        material.SetFloat("_Glossiness", smoothness);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
    static void RegisterBuildScenes()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        var paths = new[] { "Assets/Scenes/SampleScene.unity", NaturePath, UrbanPath };
        foreach (var path in paths)
        {
            var existing = scenes.Find(s => s.path == path);
            if (existing != null) existing.enabled = true;
            else if (File.Exists(path)) scenes.Add(new EditorBuildSettingsScene(path, true));
        }
        if (!scenes.Select(s => s.path + s.enabled).SequenceEqual(EditorBuildSettings.scenes.Select(s => s.path + s.enabled)))
            EditorBuildSettings.scenes = scenes.ToArray();
    }
    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
