using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.OpenXR.Features.Interactions;

/// <summary>Editor checks exercise real Input System actions and XRI UI raycasting with a synthetic controller.</summary>
[InitializeOnLoad]
public static class QuestVerification
{
    static int _stage;
    static double _next, _frozenStudyTime;
    static OculusTouchControllerProfile.OculusTouchController _controller;
    const string Batch = "Experiment.QuestBatch";
    static QuestVerification()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            var wasVR = SessionState.GetBool("Experiment.QuestPreview", false);
            SessionState.SetBool("Experiment.QuestPreview", false);
            if (_controller != null && _controller.added) InputSystem.RemoveDevice(_controller);
            _controller = null;
            if (!SessionState.GetBool(Batch, false)) return;
            SessionState.SetBool(Batch, false);
            var passed = File.Exists("Temp/ExperimentPreviews/flow-report.txt") && !File.Exists("Temp/ExperimentPreviews/flow-failure.txt");
            if (Application.isBatchMode)
            {
                var destination = "QuestValidationOutput/" + (wasVR ? "VR" : "Desktop");
                Directory.CreateDirectory(destination);
                foreach (var directory in new[] { "Temp/ExperimentPreviews", "Temp/QuestVerification" })
                    if (Directory.Exists(directory))
                        foreach (var path in Directory.GetFiles(directory)) File.Copy(path, Path.Combine(destination, Path.GetFileName(path)), true);
                EditorApplication.Exit(passed ? 0 : 1);
            }
        };
    }

    public static void BatchVR()
    {
        Directory.CreateDirectory("Temp/ExperimentPreviews");
        foreach (var file in new[] { "flow-report.txt", "flow-failure.txt" })
            if (File.Exists("Temp/ExperimentPreviews/" + file)) File.Delete("Temp/ExperimentPreviews/" + file);
        EditorSceneManager.OpenScene(QuestBuild.Scenes[0]);
        SessionState.SetBool("Experiment.QuestPreview", true);
        SessionState.SetBool(Batch, true);
        ExperimentVerification.StartFlowCheck();
    }

    public static void BatchDesktop()
    {
        Directory.CreateDirectory("Temp/ExperimentPreviews");
        foreach (var file in new[] { "flow-report.txt", "flow-failure.txt" })
            if (File.Exists("Temp/ExperimentPreviews/" + file)) File.Delete("Temp/ExperimentPreviews/" + file);
        EditorSceneManager.OpenScene(QuestBuild.Scenes[0]);
        SessionState.SetBool("Experiment.QuestPreview", false);
        SessionState.SetBool(Batch, true);
        ExperimentVerification.StartFlowCheck();
    }

    public static bool ExerciseVR(ExperimentUI ui)
    {
        var vr = ExperimentVR.Instance;
        if (vr == null) return false;
        Require(ui.Canvas.renderMode == RenderMode.WorldSpace, "VR canvas is not world space.");
        Require(ui.Canvas.worldCamera == vr.Head && vr.Head.enabled, "Head camera was disabled or unbound.");
        Require(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c => c.enabled) == 1, "Expected one active camera.");
        Require(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(c => c.enabled) == 1, "Expected one active audio listener.");
        Require(ui.Canvas.GetComponentsInChildren<Graphic>().All(g => g.gameObject.layer == 5), "A VR graphic is on a hidden layer.");
        Require(ui.Canvas.GetComponent<TrackedDeviceGraphicRaycaster>().enabled, "Missing tracked UI raycaster.");
        Require(EventSystem.current.currentInputModule is XRUIInputModule, "Wrong UI input module.");
        if (_stage >= 11) return false;
        if (EditorApplication.timeSinceStartup < _next) return true;
        _next = EditorApplication.timeSinceStartup + .45;
        switch (_stage++)
        {
            case 0:
                Require(!ui.GetComponentInChildren<Toggle>().isOn, "VR should start with full study durations.");
                InputSystem.RegisterLayout<OculusTouchControllerProfile.OculusTouchController>("QuestVerificationController");
                _controller = (OculusTouchControllerProfile.OculusTouchController)InputSystem.AddDevice("QuestVerificationController");
                InputSystem.SetDeviceUsage(_controller, UnityEngine.InputSystem.CommonUsages.RightHand);
                QueueState(_controller.trackingState, 3);
                QueueState(_controller.isTracked, 1f);
                Aim(ui.GetComponentsInChildren<Button>().Single(b => b.name == "Edit ID"), vr);
                break;
            case 1: QueueState(_controller.triggerPressed, 1f); break;
            case 2: QueueState(_controller.triggerPressed, 0f); break;
            case 3:
                Require(ui.GetComponentsInChildren<Button>().Any(b => b.name == "Delete"), "Controller ray/trigger did not open the ID keyboard.");
                ui.ShowVRPause(() => { });
                ui.HideVRPause();
                Require(ui.GetComponentsInChildren<Button>().Any(b => b.name == "Delete") && !ui.GetComponentsInChildren<Button>().Any(b => b.name == "Start session"), "Resuming the keyboard exposed setup controls behind it.");
                EventSystem.current.SetSelectedGameObject(null);
                QueueState(_controller.thumbstick, Vector2.right);
                break;
            case 4:
                QueueState(_controller.thumbstick, Vector2.zero);
                Require(EventSystem.current.currentSelectedGameObject != null, "Thumbstick did not select a keyboard key.");
                ui.GetComponentsInChildren<Button>().Single(b => b.name == "Done").Select();
                QueueState(_controller.primaryButton, 1f);
                break;
            case 5: QueueState(_controller.primaryButton, 0f); break;
            case 6:
                Require(ui.GetComponentsInChildren<Button>().Any(b => b.name == "Start session"), "A/X did not submit Done on the keyboard.");
                var look = new GameObject("Still-pose check").AddComponent<ConditionLook>();
                look.transform.rotation = Quaternion.Euler(0, 37, 0);
                look.SetStill(true);
                Require(Quaternion.Angle(look.transform.rotation, Quaternion.Euler(0, 37, 0)) < .01f, "Still phase reset a VR pose.");
                UnityEngine.Object.Destroy(look.gameObject);
                vr.SendMessage("SuspendStudy", "VerificationPause");
                _frozenStudyTime = vr.StudyTime;
                break;
            case 7:
                Require(!ui.GetComponentsInChildren<Button>().Any(b => b.name == "Start session"), "Setup remained interactive behind pause panel.");
                Require(vr.Suspended && Math.Abs(vr.StudyTime - _frozenStudyTime) < .0001, "Study clock advanced during pause.");
                vr.SendMessage("OnApplicationFocus", true);
                ui.SetResumeAvailable(true);
                ui.GetComponentsInChildren<Button>().Single(b => b.name == "Resume").onClick.Invoke();
                break;
            case 8:
                Require(!vr.Suspended && vr.StudyTime > _frozenStudyTime, "Study clock did not resume.");
                InputSystem.RemoveDevice(_controller);
                _controller = null;
                Directory.CreateDirectory("Temp/QuestVerification");
                File.WriteAllText("Temp/QuestVerification/interaction-report.txt", "PASS: world-space UI; one tracked camera/listener; controller ray + trigger opens ID keyboard; thumbstick selects; A/X confirms; pause panel blocks setup and freezes/resumes the study clock; still phase preserves pose. Synthetic controller, not a physical headset test.\n");
                break;
            case 9: break;
            case 10: return false;
        }
        return true;
    }

    static void QueueState<T>(InputControl<T> control, T value) where T : struct
    {
        using (DeltaStateEvent.From(control, out var state))
        {
            control.WriteValueIntoEvent(value, state);
            InputSystem.QueueEvent(state);
        }
    }

    static void Aim(Button target, ExperimentVR vr)
    {
        Canvas.ForceUpdateCanvases();
        var origin = new Vector3(.2f, -.2f, .2f);
        var trackingSpace = vr.Head.transform.parent;
        QueueState(_controller.pointerPosition, origin);
        QueueState(_controller.pointerRotation, Quaternion.LookRotation(trackingSpace.InverseTransformPoint(target.transform.position) - origin));
    }
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
