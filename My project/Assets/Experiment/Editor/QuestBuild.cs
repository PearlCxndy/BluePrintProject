using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

/// <summary>Repeatable standalone Quest configuration and build entry points.</summary>
public static class QuestBuild
{
    public static readonly string[] Scenes =
    {
        "Assets/Scenes/SampleScene.unity",
        "Assets/Experiment/Scenes/ConditionA_Nature.unity",
        "Assets/Experiment/Scenes/ConditionB_Urban.unity"
    };

    [MenuItem("Experiment/Quest/Configure Android OpenXR")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before configuring Quest.");
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            throw new BuildFailedException("Install Android Build Support, SDK/NDK and OpenJDK for this editor in Unity Hub, then restart Unity.");
        PlayerSettings.productName = "Tech Porto Experiment";
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)32;
        PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)34;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "org.techporto.experiment");
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.runInBackground = true;
        var player = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        player.FindProperty("activeInputHandler").intValue = 1;
        player.ApplyModifiedPropertiesWithoutUndo();
        EditorBuildSettings.scenes = Scenes.Select(p => new EditorBuildSettingsScene(p, true)).ToArray();

        if (!EditorBuildSettings.TryGetConfigObject<XRGeneralSettingsPerBuildTarget>(XRGeneralSettings.k_SettingsKey, out var perTarget))
        {
            Directory.CreateDirectory("Assets/XR");
            AssetDatabase.Refresh();
            perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
        }
        if (!perTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
            perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
        var general = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
        general.InitManagerOnStart = true;
        general.Manager.automaticLoading = true;
        general.Manager.automaticRunning = true;
        if (!XRPackageMetadataStore.AssignLoader(general.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Android))
            throw new BuildFailedException("Unable to assign Android OpenXR loader.");

        FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
        var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        if (settings == null) throw new BuildFailedException("OpenXR Android settings were not created. Restart Unity after installing Android support.");
        settings.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
        var quest = settings.GetFeature<MetaQuestFeature>();
        if (quest == null) throw new BuildFailedException("Meta Quest Support feature is missing.");
        quest.enabled = true;
        settings.GetFeature<OculusTouchControllerProfile>().enabled = true;
        settings.GetFeature<MetaQuestTouchPlusControllerProfile>().enabled = true;
        var serialized = new SerializedObject(quest);
        var devices = serialized.FindProperty("targetDevices");
        for (var i = 0; i < devices.arraySize; i++)
        {
            var item = devices.GetArrayElementAtIndex(i);
            var id = item.FindPropertyRelative("manifestName").stringValue;
            item.FindPropertyRelative("enabled").boolValue = id == "eureka" || id == "quest3s";
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(quest);
        EditorUtility.SetDirty(settings);
        EditorUtility.SetDirty(general);
        EditorUtility.SetDirty(general.Manager);
        EditorUtility.SetDirty(perTarget);
        const string pointerPath = "Assets/Experiment/Resources/Experiment/QuestPointer.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(pointerPath) == null)
            AssetDatabase.CreateAsset(new Material(Shader.Find("Sprites/Default")) { name = "Quest Pointer" }, pointerPath);
        AssetDatabase.SaveAssets();
        Validate();
    }

    [MenuItem("Experiment/Quest/Validate Build Settings")]
    public static void Validate()
    {
        var errors = new List<string>();
        if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP) errors.Add("Android must use IL2CPP.");
        if (PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64) errors.Add("Android must use ARM64.");
        var general = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
        if (general == null || !general.InitManagerOnStart || general.Manager == null || !general.Manager.activeLoaders.Any(l => l is OpenXRLoader)) errors.Add("Android OpenXR loader must initialize on startup.");
        var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        if (settings == null || settings.GetFeature<MetaQuestFeature>()?.enabled != true) errors.Add("Enable Meta Quest Support.");
        foreach (var path in Scenes) if (!EditorBuildSettings.scenes.Any(s => s.enabled && s.path == path)) errors.Add("Missing build scene: " + path);
        var issues = new List<UnityEngine.XR.OpenXR.Features.OpenXRFeature.ValidationRule>();
        // Some Meta validation predicates consult the selected group instead of their supplied target.
        var previousGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
        try
        {
            EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Android;
            UnityEditor.XR.OpenXR.OpenXRProjectValidation.GetCurrentValidationIssues(issues, BuildTargetGroup.Android);
        }
        finally { EditorUserBuildSettings.selectedBuildTargetGroup = previousGroup; }
        errors.AddRange(issues.Where(issue => issue.error).Select(issue => issue.message));
        Directory.CreateDirectory("Temp/QuestVerification");
        File.WriteAllLines("Temp/QuestVerification/openxr-warnings.txt", issues.Where(issue => !issue.error).Select(issue => issue.message));
        File.WriteAllLines("Temp/QuestVerification/build-settings.txt", errors.Count == 0 ? new[] { "PASS: IL2CPP, ARM64, Android OpenXR initialization, Meta Quest Support and all three scenes." } : errors);
        if (errors.Count > 0) throw new BuildFailedException(string.Join("\n", errors));
        Debug.Log("Quest build settings passed. Physical headset validation remains required.");
    }

    [MenuItem("Experiment/Quest/Build Development APK")]
    public static void BuildAPK()
    {
        Configure();
        EditorUserBuildSettings.buildAppBundle = false;
        Directory.CreateDirectory("Builds/Quest");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = Scenes,
            target = BuildTarget.Android,
            locationPathName = "Builds/Quest/TechPorto-Quest.apk",
            options = BuildOptions.Development
        });
        if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Quest APK build failed: " + report.summary.result);
    }

    [MenuItem("Experiment/Quest/Preview VR Layout in Editor")]
    public static void Preview()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(Scenes[0]);
        SessionState.SetBool("Experiment.QuestPreview", true);
        EditorApplication.isPlaying = true;
    }
}

public class QuestBuildGuard : IPreprocessBuildWithReport
{
    public int callbackOrder => -100;
    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform == BuildTarget.Android) QuestBuild.Validate();
    }
}
