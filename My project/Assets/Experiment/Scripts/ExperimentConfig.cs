using UnityEngine;

[CreateAssetMenu(fileName = "ExperimentConfig", menuName = "Experiment/Config")]
public class ExperimentConfig : ScriptableObject
{
    [Header("3D condition scenes (included in Build Settings)")]
    public string natureScene = "ConditionA_Nature";
    public string urbanScene = "ConditionB_Urban";

    [Header("Durations (seconds)")]
    public float baselineSeconds = 180f;
    [Tooltip("Total condition duration: 1 minute explore + 4 minutes still.")]
    public float conditionSeconds = 300f;
    public float slideIntervalSeconds = 15f;

    [Header("Test mode")]
    [Tooltip("Use short timers so you can walk through the whole flow quickly.")]
    public bool testMode = true;
    public float testBaselineSeconds = 8f;
    public float testConditionSeconds = 12f;
    public float testSlideIntervalSeconds = 3f;
    public float countdownSeconds = 1f;
    [Tooltip("How long each condition instruction sentence is shown before the experience continues.")]
    public float guidanceSeconds = 5f;

    [Header("Welcome copy")]
    [TextArea(3, 8)]
    public string welcomeTitle = "Welcome";
    [TextArea(4, 12)]
    public string welcomeBody =
        "This is a sample Unity project for the in-person training/tech.\n\n" +
        "We will begin with two resting state recordings. For the first 3 minutes, " +
        "you will be asked to look at a cross; for the second 3 minutes, you will be asked to close your eyes.\n\n" +
        "Please let the researcher know once you're ready to begin.";

    [Header("Condition copy")]
    [TextArea(2, 6)]
    public string natureInstruction = "Please look at the images. Sit comfortably and keep your attention on the screen.";
    [TextArea(2, 6)]
    public string cityInstruction = "Please look at the images. Sit comfortably and keep your attention on the screen.";

    public float GetBaselineSeconds() => testMode ? testBaselineSeconds : baselineSeconds;
    public float GetConditionSeconds() => testMode ? testConditionSeconds : conditionSeconds;
    public float GetExploreSeconds() => GetConditionSeconds() * 0.2f;
    public float GetStillSeconds() => GetConditionSeconds() * 0.8f;
    public float GetSlideInterval() => testMode ? testSlideIntervalSeconds : slideIntervalSeconds;
    public float GetCountdownSeconds() => testMode ? 0.35f : countdownSeconds;
    public float GetGuidanceSeconds() => testMode ? 1f : guidanceSeconds;

    public static ExperimentConfig LoadOrCreateRuntime()
    {
        var loaded = Resources.Load<ExperimentConfig>("Experiment/ExperimentConfig");
        if (loaded != null)
            return loaded;

        var runtime = CreateInstance<ExperimentConfig>();
        runtime.name = "ExperimentConfig (runtime)";
        return runtime;
    }
}
