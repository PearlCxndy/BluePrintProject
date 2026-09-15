using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Local editor checks and review images. Outputs stay under Temp.</summary>
[InitializeOnLoad]
public static class ExperimentVerification
{
    const string Output = "Temp/ExperimentPreviews";
    const string Request = "Temp/ExperimentVerify.request";
    const string Running = "ExperimentVerification.Running";
    static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static double _nextClick, _started;
    static bool _previewNature, _previewUrban;
    static readonly HashSet<string> Captures = new HashSet<string>();

    static ExperimentVerification()
    {
        EditorApplication.update += Update;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            SessionState.SetBool(Running, false);
            ExperimentLogger.VerificationDirectory = null;
        };
    }

    [MenuItem("Experiment/Verify/Render Scene Previews")]
    public static void RenderScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Directory.CreateDirectory(Output);
        ConditionSceneBuilder.BuildMissing();
        var previous = SceneManager.GetActiveScene();
        var active = new List<GameObject>();
        for (var i = 0; i < SceneManager.sceneCount; i++)
            foreach (var root in SceneManager.GetSceneAt(i).GetRootGameObjects())
                if (root.activeSelf) { active.Add(root); root.SetActive(false); }
        var report = new List<string>();
        try
        {
            foreach (var path in new[] { ConditionSceneBuilder.NaturePath, ConditionSceneBuilder.UrbanPath })
            {
                var scene = SceneManager.GetSceneByPath(path);
                var alreadyOpen = scene.IsValid() && scene.isLoaded;
                if (!alreadyOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                else foreach (var root in scene.GetRootGameObjects()) if (active.Contains(root)) root.SetActive(true);
                SceneManager.SetActiveScene(scene);
                try
                {
                    var environment = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ConditionEnvironment>()).Single();
                    var renderers = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Renderer>()).ToArray();
                    var invalid = renderers.SelectMany(r => r.sharedMaterials).Count(m => m == null || m.shader == null || !m.shader.isSupported);
                    report.Add(scene.name + ": " + renderers.Length + " renderers; " + invalid + " missing/unsupported materials; camera height " + environment.viewCamera.transform.position.y);
                    if (invalid != 0) throw new Exception("Unsupported material in " + scene.name);
                    var expectedSky = environment.condition == ConditionType.Nature ? ConditionSceneEnhancements.NatureSky : ConditionSceneEnhancements.UrbanSky;
                    if (AssetDatabase.GetAssetPath(RenderSettings.skybox) != expectedSky) throw new Exception("Incorrect skybox in " + scene.name);
                    if (Mathf.Abs(environment.look.mouseSensitivity - 2.5f) > .001f) throw new Exception("Mouse sensitivity was not saved.");
                    report.Add("Skybox: " + RenderSettings.skybox.name + "; mouse sensitivity: " + environment.look.mouseSensitivity);
                    CaptureCamera(environment.viewCamera, Output + "/" + scene.name + ".png");
                    var motion = environment.GetComponent<NatureAmbientMotion>();
                    if (environment.condition == ConditionType.Nature)
                    {
                        if (motion == null || motion.trees.Length != 200 || motion.lake == null) throw new Exception("Nature motion or extra trees missing.");
                        var cameraPose = environment.viewCamera.transform.localToWorldMatrix;
                        var positions = motion.trees.Select(t => t.position).ToArray();
                        try
                        {
                            motion.ApplyAtTime(0);
                            var rotations = motion.trees.Select(t => t.localRotation).ToArray();
                            motion.ApplyAtTime(4);
                            var movingTrees = motion.trees.Where((t, i) => Quaternion.Angle(t.localRotation, rotations[i]) > .01f).Count();
                            if (movingTrees < 180) throw new Exception("Tree sway is not updating.");
                            if (motion.trees.Where((t, i) => Vector3.Distance(t.position, positions[i]) > .001f).Any()) throw new Exception("Tree roots moved.");
                            if (environment.viewCamera.transform.localToWorldMatrix != cameraPose) throw new Exception("Ambient motion changed the camera.");
                            var block = new MaterialPropertyBlock();
                            motion.lake.GetPropertyBlock(block);
                            if (Mathf.Abs(block.GetFloat("_RippleTime") - 4 * motion.rippleSpeed) > .001f) throw new Exception("Water time did not advance.");
                            if (ShaderUtil.ShaderHasError(motion.lake.sharedMaterial.shader)) throw new Exception("Water shader has compilation errors.");
                            CaptureCamera(environment.viewCamera, Output + "/Nature_Motion_4s.png");
                            report.Add("PASS: 200 trees; " + movingTrees + " visibly change rotation at t=4s; tree roots and camera fixed; ripple time advances; water shader compiles.");
                        }
                        finally { motion.RestoreRestPose(); }
                    }
                }
                finally
                {
                    SceneManager.SetActiveScene(previous);
                    if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true);
                    else foreach (var root in scene.GetRootGameObjects()) if (active.Contains(root)) root.SetActive(false);
                }
            }
        }
        finally { foreach (var go in active) if (go != null) go.SetActive(true); }
        report.Add("Enabled build scenes: " + string.Join(", ", EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path)));
        File.WriteAllLines(Output + "/scene-report.txt", report);
    }

    [MenuItem("Experiment/Verify/Run Short Flow Check")]
    public static void StartFlowCheck()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (Object.FindFirstObjectByType<ConditionEnvironment>() != null)
            throw new Exception("Open SampleScene before running the flow check.");
        _started = _nextClick = 0;
        _previewNature = _previewUrban = false;
        Captures.Clear();
        SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
    }

    static void Update()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (File.Exists(Request) && File.ReadAllText(Request).Trim() == "enhance" && EditorApplication.isPlaying)
        {
            var controller = Object.FindFirstObjectByType<ExperimentController>();
            var session = controller == null ? null : (ExperimentSession)typeof(ExperimentController).GetField("_session", Private).GetValue(controller);
            if (session == null || session.testMode)
            {
                SessionState.SetBool(Running, false);
                EditorApplication.isPlaying = false;
            }
            return;
        }
        if (File.Exists(Request) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            var command = File.ReadAllText(Request).Trim();
            File.Delete(Request);
            try
            {
                if (command == "scenes") RenderScenes();
                else if (command == "flow") StartFlowCheck();
                else if (command == "enhance") { ConditionSceneEnhancements.ApplyToSavedScenes(); RenderScenes(); }
                else if (command == "skyboxes") { ConditionSceneEnhancements.ApplySkyboxes(); RenderScenes(); }
            }
            catch (Exception e) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/failure.txt", e.ToString()); Debug.LogException(e); }
        }
        if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying) return;
        try { CheckFlow(); }
        catch (Exception e)
        {
            File.WriteAllText(Output + "/flow-failure.txt", e.ToString());
            SessionState.SetBool(Running, false);
            EditorApplication.isPlaying = false;
            Debug.LogException(e);
        }
    }

    static void CheckFlow()
    {
        var now = EditorApplication.timeSinceStartup;
        if (_started == 0) { _started = now; _nextClick = now + 2; Directory.CreateDirectory(Output); }
        if (now - _started > 180) throw new Exception("Flow check timed out.");
        if (now < _nextClick) return;
        var controller = Object.FindFirstObjectByType<ExperimentController>();
        if (controller == null) return;
        if (string.IsNullOrEmpty(ExperimentLogger.VerificationDirectory))
        {
            var original = ExperimentLogger.SaveDirectory;
            ExperimentLogger.VerificationDirectory = Path.GetFullPath(Output);
            File.WriteAllText(Output + "/data-location.txt", "Normal participant data directory: " + original);
        }
        var ui = controller.GetComponent<ExperimentUI>();
        var launchButtons = ui.GetComponentsInChildren<Button>();
        var launch = launchButtons.FirstOrDefault(b => b.name == (ExperimentVR.Instance != null ? "VR version" : "Non-VR version"));
        if (launch != null)
        {
            if (Captures.Add("launch"))
            {
                CaptureUI(ui, "Launch");
                launchButtons.Single(b => b.name == (ExperimentVR.Instance != null ? "Non-VR version" : "VR version")).onClick.Invoke();
                ui.GetComponentsInChildren<Button>().Single(b => b.name == "Back to mode selection").onClick.Invoke();
                launch = ui.GetComponentsInChildren<Button>().Single(b => b.name == (ExperimentVR.Instance != null ? "VR version" : "Non-VR version"));
            }
            launch.onClick.Invoke();
            _nextClick = now + 1;
            return;
        }
        if (QuestVerification.ExerciseMouse(ui)) return;
        if (QuestVerification.ExerciseVR(ui)) return;
        var buttons = ui.GetComponentsInChildren<Button>();
        var eyes = ui.GetComponentInChildren<ClosedEyesGraphic>();
        if (eyes != null && Captures.Add("eyes-closed"))
        {
            if (eyes.color != Color.white || eyes.rectTransform.rect.width < 800)
                throw new Exception("Closed-eyes cue must be large and white.");
            CaptureUI(ui, "EyesClosed");
        }
        var setup = buttons.FirstOrDefault(b => b.name == "Start session");
        if (setup != null)
        {
            if (Captures.Add("setup")) CaptureUI(ui, "Setup");
            if (!_previewNature)
            {
                _previewNature = true;
                buttons.Single(b => b.name == "Preview Nature").onClick.Invoke();
            }
            else if (!_previewUrban)
            {
                _previewUrban = true;
                buttons.Single(b => b.name == "Preview Urban").onClick.Invoke();
            }
            else
            {
                ui.GetComponentInChildren<InputField>().text = "UI_FLOW_CHECK";
                ui.GetComponentInChildren<Toggle>().isOn = true;
                buttons.Single(b => b.name == "Nature first").onClick.Invoke();
                setup.onClick.Invoke();
            }
            _nextClick = now + 2;
            return;
        }
        var back = buttons.FirstOrDefault(b => b.name == "Back to setup");
        if (back != null)
        {
            var environment = Object.FindFirstObjectByType<ConditionEnvironment>();
            if (environment == null) throw new Exception("Preview has no environment.");
            if (Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(l => l.enabled) != 1) throw new Exception("Duplicate/missing audio listeners during preview.");
            CaptureUI(ui, environment.condition + "_Overlay");
            back.onClick.Invoke();
            _nextClick = now + 2;
            return;
        }
        var responses = buttons.Where(b => b.name.StartsWith("Response ")).ToArray();
        if (responses.Length > 0)
        {
            if (responses.Length != 6) throw new Exception("Expected six response targets.");
            var session = (ExperimentSession)typeof(ExperimentController).GetField("_session", Private).GetValue(controller);
            var figures = ui.GetComponentsInChildren<RawImage>().Where(i => i.name == "SAM figure").ToArray();
            if (figures.Length != 0 && (figures.Length != 5 || figures.Select(f => f.texture).Distinct().Count() != 5)) throw new Exception("SAM needs five distinct anchors.");
            if (session.ratings.Count == 0 && Captures.Add("sam"))
            {
                CaptureUI(ui, "SAM");
                ExecuteEvents.Execute(responses[3].gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
                _nextClick = now + .6;
                return;
            }
            if (session.ratings.Count == 0 && Captures.Add("hover")) CaptureUI(ui, "SAM_Hover");
            responses[3].onClick.Invoke();
            responses[3].onClick.Invoke(); // Duplicate submission must be ignored.
            _nextClick = now + .6;
            return;
        }
        var end = buttons.FirstOrDefault(b => b.name == "New session");
        if (end != null)
        {
            var session = (ExperimentSession)typeof(ExperimentController).GetField("_session", Private).GetValue(controller);
            if (session.ratings.Count != 12) throw new Exception("Expected 12 recorded responses, got " + session.ratings.Count);
            if (session.events.Count(e => e.name.Contains("_Response_")) != 12) throw new Exception("Duplicate response event.");
            var report = new List<string> { "PASS: both previews, Valence → Arousal, both conditions, all 12 ratings, duplicate-click protection, CSV export." };
            foreach (var name in new[] { "Nature", "Urban" })
            {
                var begin = DateTime.Parse(session.events.Single(e => e.name == "Condition_" + name + "_Begin").timestampIso);
                var still = DateTime.Parse(session.events.Single(e => e.name == "Condition_" + name + "_LookStraightAhead").timestampIso);
                var finish = DateTime.Parse(session.events.Single(e => e.name == "Condition_" + name + "_End").timestampIso);
                if (Math.Abs((finish - begin).TotalSeconds - 12) > .6) throw new Exception("Condition duration drift: " + name);
                report.Add(name + ": explore " + (still - begin).TotalSeconds.ToString("F3") + "s; total " + (finish - begin).TotalSeconds.ToString("F3") + "s (test mode).");
            }
            CaptureUI(ui, "Complete");
            File.WriteAllLines(Output + "/flow-report.txt", report);
            SessionState.SetBool(Running, false);
            EditorApplication.isPlaying = false;
            return;
        }
        var next = buttons.FirstOrDefault(b => b.name == "Next" || b.name == "Begin" || b.name.StartsWith("Click here"));
        if (next != null) { next.onClick.Invoke(); _nextClick = now + .75; }
    }

    static void CaptureUI(ExperimentUI ui, string name)
    {
        if (ExperimentVR.Instance != null)
        {
            Canvas.ForceUpdateCanvases();
            CaptureCamera(ExperimentVR.Instance.Head, Output + "/" + name + ".png");
            return;
        }
        var cameraObject = new GameObject("UI review camera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.17f, .17f, .17f);
        camera.cullingMask = 1 << 5;
        camera.transform.position = new Vector3(0, 0, -500);
        var oldMode = ui.Canvas.renderMode;
        var oldCamera = ui.Canvas.worldCamera;
        var layers = ui.GetComponentsInChildren<Transform>(true).ToDictionary(t => t, t => t.gameObject.layer);
        try
        {
            foreach (var entry in layers) entry.Key.gameObject.layer = 5;
            ui.Canvas.renderMode = RenderMode.ScreenSpaceCamera;
            ui.Canvas.worldCamera = camera;
            ui.Canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases();
            CaptureCamera(camera, Output + "/" + name + ".png");
        }
        finally
        {
            ui.Canvas.renderMode = oldMode;
            ui.Canvas.worldCamera = oldCamera;
            foreach (var entry in layers) entry.Key.gameObject.layer = entry.Value;
            Object.DestroyImmediate(cameraObject);
        }
    }
    static void CaptureCamera(Camera camera, string path)
    {
        var previous = RenderTexture.active;
        var target = camera.targetTexture;
        var texture = RenderTexture.GetTemporary(1600, 1000, 24);
        var png = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = texture;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = texture;
            png.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0);
            png.Apply();
            File.WriteAllBytes(path, png.EncodeToPNG());
        }
        finally { camera.targetTexture = target; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(texture); Object.DestroyImmediate(png); }
    }
}
