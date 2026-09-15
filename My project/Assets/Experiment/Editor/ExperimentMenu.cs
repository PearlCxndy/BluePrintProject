using System.IO;
using UnityEditor;
using UnityEngine;

public static class ExperimentMenu
{
    const string ConfigPath = "Assets/Experiment/Resources/Experiment/ExperimentConfig.asset";

    [MenuItem("Experiment/Create Default Config")]
    public static void CreateDefaultConfig()
    {
        EnsureConfig();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<ExperimentConfig>(ConfigPath);
        EditorGUIUtility.PingObject(Selection.activeObject);
    }

    [MenuItem("Experiment/Open Saved Data Folder")]
    public static void OpenDataFolder()
    {
        Directory.CreateDirectory(ExperimentLogger.SaveDirectory);
        EditorUtility.RevealInFinder(ExperimentLogger.SaveDirectory);
    }

    [MenuItem("Experiment/Add Experiment To Open Scene")]
    public static void AddExperimentToOpenScene()
    {
        var existing = Object.FindFirstObjectByType<ExperimentController>();
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            EditorGUIUtility.PingObject(existing.gameObject);
            EditorUtility.DisplayDialog("Experiment", "The scene already has an Experiment object.", "OK");
            return;
        }

        var go = new GameObject("Experiment");
        Undo.RegisterCreatedObjectUndo(go, "Add Experiment");
        go.AddComponent<ExperimentController>();
        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
    }

    [InitializeOnLoadMethod]
    static void EnsureFoldersAndConfig()
    {
        EnsureFolder("Assets/Experiment/Resources");
        EnsureFolder("Assets/Experiment/Resources/Experiment");
        EnsureFolder("Assets/Experiment/Resources/Experiment/Nature");
        EnsureFolder("Assets/Experiment/Resources/Experiment/City");
        EnsureConfig();
    }

    static void EnsureConfig()
    {
        if (AssetDatabase.LoadAssetAtPath<ExperimentConfig>(ConfigPath) != null)
            return;

        var config = ScriptableObject.CreateInstance<ExperimentConfig>();
        AssetDatabase.CreateAsset(config, ConfigPath);
        AssetDatabase.SaveAssets();
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        var parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        var name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
            return;

        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, name);
    }
}
