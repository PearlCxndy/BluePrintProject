using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>Runs the Tech Porto resting-state and Nature/Urban experiment.</summary>
public class ExperimentController : MonoBehaviour
{
    double StudyTime => ExperimentVR.Instance != null ? ExperimentVR.Instance.StudyTime : Time.realtimeSinceStartupAsDouble;
    float StudyDelta => ExperimentVR.Instance != null ? ExperimentVR.Instance.StudyDelta : Time.unscaledDeltaTime;
    ExperimentConfig _config; ExperimentUI _ui; ExperimentSession _session;
    AudioSource _audio; AudioClip _tone; bool _skipRequested;
    ConditionEnvironment _environment;
    Scene _hostScene, _conditionScene;
    readonly List<Behaviour> _suspended = new List<Behaviour>();
    int _conditionIndex;
    bool _abortSession;
    bool _ownsConditionScene;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void AutoStart()
    {
        if (FindFirstObjectByType<ExperimentController>() == null && FindFirstObjectByType<ConditionEnvironment>() == null)
            new GameObject("Tech Porto Experiment").AddComponent<ExperimentController>();
    }

    void Awake()
    {
        _hostScene = gameObject.scene;
        _config = Instantiate(ExperimentConfig.LoadOrCreateRuntime()); _ui = gameObject.AddComponent<ExperimentUI>(); _ui.Build();
        if (ExperimentVR.Requested) { _config.testMode = false; gameObject.AddComponent<ExperimentVR>().Initialize(_ui); }
        _audio = gameObject.AddComponent<AudioSource>(); _audio.playOnAwake = false; _tone = ExperimentAudio.CreateTone();
    }
    void Start()
    {
        // Not in Awake: a scene is only marked loaded once every Awake in it has run.
        // The host owns lighting while the UI is up, even with a condition scene open beside it.
        if (_hostScene.isLoaded) SceneManager.SetActiveScene(_hostScene);
        ShowLaunchMenu();
    }
    void Update() { if (ExperimentInput.Held(Key.RightShift) && ExperimentInput.Pressed(Key.N)) _skipRequested = true; }

    void ShowLaunchMenu()
    {
        _ui.SetProgress("CHOOSE HOW TO PLAY");
        _ui.ShowLaunchMenu(ExperimentVR.Instance != null, ShowSetup, ShowLaunchMenu);
    }

    void ShowSetup()
    {
        var suggestedId = "P" + DateTime.Now.ToString("HHmmss");
        _ui.SetProgress("RESEARCHER SETUP");
        _ui.ShowSetup(suggestedId, _config.testMode, ConditionOrder.Random, (id, testMode, order) =>
        { _config.testMode = testMode; _conditionIndex = 0; _abortSession = false; _session = ExperimentSession.Create(id, testMode, ResolveOrder(order)); StartCoroutine(RunExperiment()); },
        condition => StartCoroutine(Preview(condition)));
    }
    IEnumerator RunExperiment()
    {
        _ui.SetProgress("WELCOME");
        yield return WaitForClick(_config.welcomeTitle, _config.welcomeBody, "Click here to move on");
        _ui.SetProgress("RESTING STATE  /  1 OF 2");
        yield return WaitForClick("Resting state — eyes open", "Please sit still, relax, and fixate your gaze on the red cross.", "Begin");
        yield return RunCountdown("Resting state — eyes open", "Fixate on the red cross", () => { Mark("RestingState_EyesOpen_Begin"); return _ui.ShowRedCrossBaseline(); }, _config.GetBaselineSeconds());
        _ui.SetProgress("RESTING STATE  /  2 OF 2");
        yield return WaitForClick("Resting state — eyes closed", "Please sit still, relax, and close your eyes for 3 minutes.", "Begin");
        yield return RunCountdown("Resting state — eyes closed", "Please close your eyes", () => { Mark("RestingState_EyesClosed_Begin"); return _ui.ShowEyesClosedBaseline(); }, _config.GetBaselineSeconds());
        Mark("RestingState_EyesClosed_End");
        yield return AskRating("Valence_Baseline", "How unhappy or happy are you feeling now?", "Unhappy", "Happy");
        yield return AskRating("Arousal_Baseline", "How calm or stimulated are you feeling now?", "Calm", "Aroused");
        yield return WaitForClick("The experiment", "Thank you for completing the resting state recording and the survey.\n\nNow we begin the real experiment. You will be shown two different scenarios, and after each scenario you will complete a simple survey before moving onto the next.\n\nPlease tell the researcher when you're ready to begin.", "Click here to begin");
        foreach (var condition in _session.ConditionSequence())
        {
            yield return RunCondition(condition);
            if (_abortSession) yield break;
        }
        _ui.SetProgress("SESSION COMPLETE");
        _session.finishedAtIso = ExperimentSession.NowIso(); Mark("SessionCompleted"); FinishSession();
    }
    IEnumerator RunCondition(ConditionType condition)
    {
        _conditionIndex++;
        var name = condition == ConditionType.Nature ? "Nature" : "Urban";
        yield return LoadEnvironment(condition);
        yield return WaitUntilAvailable();
        if (_environment == null) yield break;
        _environment.Begin();
        if (ExperimentVR.Instance != null) ExperimentVR.Instance.EnterEnvironment(_environment);
        Mark("Condition_" + name + "_Begin");
        yield return RunEnvironmentTiming(name);
        PlayTone();
        Mark("Condition_" + name + "_End");
        _ui.ShowLoading("Experience complete", "Please take a moment to describe how you feel.");
        yield return UnloadEnvironment();
        yield return AskRating("Valence_" + name, "How unhappy or happy are you feeling now?", "Unhappy", "Happy"); yield return AskRating("Arousal_" + name, "How calm or stimulated are you feeling now?", "Calm", "Aroused");
        yield return AskRating("Liking_" + name, "How much did you like the experience?", "Dislike", "Like"); yield return AskRating("Beauty_" + name, "How beautiful did you find the experience?", "Ugly", "Beautiful"); yield return AskRating("Wanting_" + name, "How much would you want to experience this again?", "Not at all", "Very much");
    }
    IEnumerator AskRating(string eventName, string question, string low, string high)
    {
        var baseline = eventName.EndsWith("Baseline");
        var kind = eventName.Split('_')[0];
        var number = Array.IndexOf(new[] { "Valence", "Arousal", "Liking", "Beauty", "Wanting" }, kind) + 1;
        _ui.SetProgress((baseline ? "BASELINE" : "EXPERIENCE " + _conditionIndex + " OF 2") + "  /  QUESTION " + number + " OF " + (baseline ? 2 : 5));
        var complete = false; _ui.ShowSixPointRating(kind == "Valence" ? "How you feel" : kind == "Arousal" ? "Your energy level" : "Your experience", question, low, high, value => { if (complete) return; _session.RecordRating(eventName, value); Mark(eventName + "_Response_" + value); complete = true; }, kind); while (!complete) yield return null;
    }

    IEnumerator Preview(ConditionType condition)
    {
        yield return LoadEnvironment(condition);
        yield return WaitUntilAvailable();
        if (_environment == null) yield break;
        _environment.Begin();
        if (ExperimentVR.Instance != null) ExperimentVR.Instance.EnterEnvironment(_environment);
        var back = false;
        _ui.ShowEnvironmentOverlay("Environment preview", ExperimentVR.Requested ? "Look around naturally. Point and press the trigger to return to setup." : "Hold the right mouse button and drag, or use the arrow keys to look around.", "", () => back = true);
        while (!back) yield return null;
        _ui.ShowLoading("Returning to setup", "");
        yield return UnloadEnvironment();
        ShowSetup();
    }

    IEnumerator LoadEnvironment(ConditionType condition)
    {
        var sceneName = condition == ConditionType.Nature ? _config.natureScene : _config.urbanScene;
        _ui.ShowLoading("Preparing your experience", "Please wait a moment.");
        // Left open in the editor beside the host scene, the environment is already in memory
        // (hidden by ConditionEnvironment.Awake). Loading it again would duplicate the scene.
        var open = SceneManager.GetSceneByName(sceneName);
        _ownsConditionScene = !(open.IsValid() && open.isLoaded);
        if (_ownsConditionScene && !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("Condition scene is not in Build Settings: " + sceneName);
            var back = false;
            _ui.ShowWelcome("Experience unavailable", "Please let the researcher know this scene could not be loaded.", () => back = true, "Back to setup");
            while (!back) yield return null;
            _abortSession = true;
            ShowSetup();
            yield break;
        }
        foreach (var root in _hostScene.GetRootGameObjects())
        {
            foreach (var camera in root.GetComponentsInChildren<Camera>())
                if (ExperimentVR.Instance == null || camera != ExperimentVR.Instance.Head) Suspend(camera);
            foreach (var listener in root.GetComponentsInChildren<AudioListener>())
                if (ExperimentVR.Instance == null || listener.gameObject != ExperimentVR.Instance.Head.gameObject) Suspend(listener);
            foreach (var light in root.GetComponentsInChildren<Light>()) Suspend(light);
        }
        if (_ownsConditionScene) yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        _conditionScene = SceneManager.GetSceneByName(sceneName);
        SceneManager.SetActiveScene(_conditionScene);
        foreach (var root in _conditionScene.GetRootGameObjects())
            if ((_environment = root.GetComponentInChildren<ConditionEnvironment>()) != null) break;
        if (_environment == null) throw new InvalidOperationException("Missing ConditionEnvironment in " + sceneName);
    }

    void Suspend(Behaviour component) { if (component.enabled) { _suspended.Add(component); component.enabled = false; } }

    IEnumerator UnloadEnvironment()
    {
        if (_environment != null) _environment.SetVisible(false);
        _environment = null;
        if (ExperimentVR.Instance != null) ExperimentVR.Instance.ShowRoom();
        SceneManager.SetActiveScene(_hostScene);
        if (_ownsConditionScene && _conditionScene.IsValid() && _conditionScene.isLoaded) yield return SceneManager.UnloadSceneAsync(_conditionScene);
        _ownsConditionScene = false;
        foreach (var component in _suspended) if (component != null) component.enabled = true;
        _suspended.Clear();
    }

    IEnumerator RunEnvironmentTiming(string name)
    {
        // All prompts and the countdown occur INSIDE the first minute, keeping the total at 300 seconds.
        var total = _config.GetConditionSeconds();
        var explore = _config.GetExploreSeconds();
        var guidance = Mathf.Min(_config.GetGuidanceSeconds(), explore / 4);
        var countdownStep = Mathf.Min(_config.GetCountdownSeconds(), explore / 8);
        var start = StudyTime;
        var still = false;
        var previousPrompt = "__initial";
        _skipRequested = false;
        while (true)
        {
            var elapsed = (float)(StudyTime - start);
            if (_skipRequested) { if (still) break; start -= Math.Max(0, explore - elapsed); elapsed = explore; _skipRequested = false; }
            if (elapsed >= total) break;
            if (!still && elapsed >= explore)
            {
                still = true;
                _environment.look.SetStill(true);
                PlayTone();
                Mark("Condition_" + name + "_LookStraightAhead");
            }
            var prompt = "";
            var detail = "";
            var count = "";
            if (still && elapsed < explore + guidance) prompt = "Please sit still, relax and look straight ahead.";
            else if (!still && elapsed >= explore - 3 * countdownStep)
            {
                prompt = "At the beep, face straight ahead and sit still.";
                count = Mathf.Clamp(Mathf.CeilToInt((explore - elapsed) / countdownStep), 1, 3).ToString();
            }
            else if (!still && elapsed >= explore - Mathf.Min(10, explore * .4f)) prompt = "When you hear the beep, please stop moving your head and look straight ahead.";
            else if (elapsed < guidance)
            {
                prompt = "Feel free to look around this environment for 1 minute.";
                detail = ExperimentVR.Requested ? "Remain seated and look around naturally." : "Desktop: hold the right mouse button and drag, or use the arrow keys.";
            }
            else if (elapsed < guidance * 2) prompt = "After that, face straight ahead and sit still for 4 minutes.";
            if (_config.testMode) detail = "TEST MODE  ·  " + ExperimentUI.FormatTime(total - elapsed) + (ExperimentVR.Requested ? " remaining" : " remaining  ·  Right Shift + N to skip");
            var signature = prompt + detail + count;
            if (signature != previousPrompt)
            {
                _ui.ShowEnvironmentOverlay(prompt, detail, count);
                previousPrompt = signature;
            }
            yield return null;
        }
    }
    IEnumerator WaitUntilAvailable() { while (ExperimentVR.Instance != null && ExperimentVR.Instance.Suspended) yield return null; }
    IEnumerator WaitForClick(string title, string body, string button) { var clicked = false; _ui.ShowWelcome(title, body, () => clicked = true, button); while (!clicked) yield return null; }
    IEnumerator RunCountdown(string heading, string instruction, Func<Text> beginScreen, float duration) { yield return CountdownOnly(heading, instruction); yield return WaitUntilAvailable(); PlayTone(); yield return RunTimer(beginScreen(), duration); PlayTone(); }
    IEnumerator CountdownOnly(string heading, string instruction = "") { for (var count = 3; count >= 1; count--) { _ui.ShowCountdown(heading, instruction, count.ToString()); yield return WaitSeconds(_config.GetCountdownSeconds()); } _ui.ShowCountdown(heading, instruction, "Begin"); yield return WaitSeconds(Mathf.Min(.6f, _config.GetCountdownSeconds())); }
    IEnumerator RunTimer(Text timer, float seconds) { _skipRequested = false; for (var elapsed = 0f; elapsed < seconds && !_skipRequested; elapsed += StudyDelta) { if (timer != null) timer.text = _config.testMode ? ExperimentUI.FormatTime(seconds - elapsed) : ""; yield return null; } if (timer != null) timer.text = "00:00"; }
    IEnumerator WaitSeconds(float seconds) { _skipRequested = false; for (var elapsed = 0f; elapsed < seconds && !_skipRequested; elapsed += StudyDelta) yield return null; }
    void Mark(string name) { _session.RecordEvent(name); ExperimentMarker.Emit(name); if (name.Contains("_Response_") || name == "SessionResumed" || name == "ApplicationFocusLost" || name == "ApplicationPaused" || name == "HeadsetUnavailable") Checkpoint(); }
    public void RecordInterruption(string reason)
    {
        if (_session == null || !string.IsNullOrEmpty(_session.finishedAtIso)) return;
        Mark(reason);
    }
    void Checkpoint()
    {
        if (_session == null) return;
        try { ExperimentLogger.Save(_session); }
        catch (Exception e) { Debug.LogError("Could not checkpoint experiment data: " + e.Message); }
    }
    void FinishSession()
    {
        try
        {
            var path = ExperimentLogger.Save(_session);
            _ui.ShowEnd(_session.participantId, path, ShowSetup);
        }
        catch (Exception e)
        {
            Debug.LogError("Could not save experiment data: " + e.Message);
            _ui.ShowWelcome("Responses need saving", "Please let the researcher know. Keep this session open and retry saving.", FinishSession, "Retry save");
        }
    }
    void OnApplicationPause(bool paused) { if (paused) Checkpoint(); }
    void OnApplicationQuit() { Checkpoint(); }
    void PlayTone() => ExperimentAudio.PlayTone(_audio, _tone);
    static ConditionType ResolveOrder(ConditionOrder order) => order == ConditionOrder.NatureThenCity ? ConditionType.Nature : order == ConditionOrder.CityThenNature ? ConditionType.City : UnityEngine.Random.value < .5f ? ConditionType.Nature : ConditionType.City;
}
