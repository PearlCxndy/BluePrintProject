using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>One seated XR rig for the complete experiment; scene cameras are placement anchors only.</summary>
public class ExperimentVR : MonoBehaviour
{
    public static ExperimentVR Instance { get; private set; }
    public static bool Requested => Application.platform == RuntimePlatform.Android || XRSettings.isDeviceActive
#if UNITY_EDITOR
        || UnityEditor.SessionState.GetBool("Experiment.QuestPreview", false)
#endif
        ;
    public Camera Head { get; private set; }
    public bool InEnvironment { get; private set; }
    public bool Suspended { get; private set; }
    public double StudyTime { get; private set; }
    public float StudyDelta => Suspended ? 0 : Time.unscaledDeltaTime;
    readonly List<InputAction> _actions = new List<InputAction>();
    readonly List<XRRayInteractor> _rays = new List<XRRayInteractor>();
    InputAction _navigate, _submit;
    readonly List<Selectable> _selectables = new List<Selectable>();
    Vector3 _panelBeforePause;
    Quaternion _panelRotationBeforePause;
    Transform _origin;
    Canvas _canvas;
    ExperimentUI _ui;
    float _nextMove, _screenInputReadyAt;
    bool _focused = true, _paused, _hadTracking;
    bool _wasPresent = true;
    bool _alignPending;
    Vector3 _anchorPosition;
    Quaternion _anchorRotation;

    public void Initialize(ExperimentUI ui)
    {
        Instance = this;
        _ui = ui;
        _canvas = ui.Canvas;
        var rig = new GameObject("Seated XR Origin");
        rig.SetActive(false);
        rig.transform.SetParent(transform, false);
        _origin = rig.transform;
        _origin.position = new Vector3(0, 1.6f, 0);
        var offset = new GameObject("Tracking Space");
        offset.transform.SetParent(_origin, false);
        var head = new GameObject("Quest Head", typeof(Camera), typeof(AudioListener));
        head.tag = "MainCamera";
        head.transform.SetParent(offset.transform, false);
        Head = head.GetComponent<Camera>();
        Head.nearClipPlane = .05f;
        Head.farClipPlane = 500;
        Head.allowHDR = false;
        Head.allowMSAA = true;
        Track(head, "<XRHMD>/centerEyePosition", "<XRHMD>/centerEyeRotation", "<XRHMD>/trackingState");
        var xr = rig.AddComponent<XROrigin>();
        xr.Camera = Head;
        xr.CameraFloorOffsetObject = offset;
        xr.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
        xr.CameraYOffset = 0;
        rig.AddComponent<XRInteractionManager>();
        CreateController(offset.transform, "LeftHand");
        CreateController(offset.transform, "RightHand");
        _navigate = Action("Survey navigation", InputActionType.Value, "<XRController>{RightHand}/primary2DAxis");
        _navigate.AddBinding("<XRController>{LeftHand}/primary2DAxis");
        _submit = Action("Confirm selection", InputActionType.Button, "<XRController>{RightHand}/primaryButton");
        _submit.AddBinding("<XRController>{LeftHand}/primaryButton");
        foreach (var action in _actions) action.Enable();
        rig.SetActive(true);

        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.worldCamera = Head;
        var scaler = _canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.dynamicPixelsPerUnit = 2;
        var rect = (RectTransform)_canvas.transform;
        rect.sizeDelta = new Vector2(1600, 1000);
        rect.localScale = Vector3.one * .0015f;
        // Keep the editor's world-space preview usable with a mouse or trackpad.
        _canvas.GetComponent<GraphicRaycaster>().enabled = Application.isEditor;
        _canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        SetUILayer(_canvas.transform);
        foreach (var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            if (camera != Head) camera.enabled = false;
        foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            if (listener.gameObject != head) listener.enabled = false;
        ShowRoom();
        QualitySettings.vSyncCount = 0;
        QualitySettings.antiAliasing = 4;
        QualitySettings.shadowDistance = 35;
        QualitySettings.shadowCascades = 1;
        QualitySettings.pixelLightCount = 1;
        Application.targetFrameRate = 72;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }

    static void SetUILayer(Transform root)
    {
        root.gameObject.layer = 5;
        foreach (Transform child in root) SetUILayer(child);
    }

    InputAction Action(string name, InputActionType type, string binding)
    {
        var action = new InputAction(name, type, binding);
        _actions.Add(action);
        return action;
    }

    void Track(GameObject go, string position, string rotation, string tracking)
    {
        var driver = go.AddComponent<TrackedPoseDriver>();
        driver.positionInput = new InputActionProperty(Action(go.name + " position", InputActionType.Value, position));
        driver.rotationInput = new InputActionProperty(Action(go.name + " rotation", InputActionType.Value, rotation));
        driver.trackingStateInput = new InputActionProperty(Action(go.name + " tracking", InputActionType.Value, tracking));
        driver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
    }

    void CreateController(Transform parent, string hand)
    {
        var go = new GameObject(hand + " UI Pointer");
        go.layer = 5;
        go.SetActive(false);
        go.transform.SetParent(parent, false);
        var path = "<XRController>{" + hand + "}";
        Track(go, path + "/pointerPosition", path + "/pointerRotation", path + "/trackingState");
        var ray = go.AddComponent<XRRayInteractor>();
        ray.enableUIInteraction = true;
        ray.maxRaycastDistance = 6;
        ray.raycastMask = 1 << 5; // XRI shares this mask between UI and physics raycasts.
        ray.uiPressInput = new XRInputButtonReader
        {
            inputSourceMode = XRInputButtonReader.InputSourceMode.InputAction,
            inputActionPerformed = Action(hand + " UI press", InputActionType.Button, path + "/triggerPressed"),
            inputActionValue = Action(hand + " trigger", InputActionType.Value, path + "/trigger")
        };
        go.AddComponent<LineRenderer>().sharedMaterial = Resources.Load<Material>("Experiment/QuestPointer");
        var line = go.AddComponent<XRInteractorLineVisual>();
        line.lineWidth = .003f;
        _rays.Add(ray);
        go.SetActive(true);
    }

    public void EnterEnvironment(ConditionEnvironment environment)
    {
        InEnvironment = true;
        _anchorPosition = environment.viewCamera.transform.position;
        _anchorRotation = Quaternion.Euler(0, environment.viewCamera.transform.eulerAngles.y, 0);
        AlignToAnchor();
        _alignPending = !_hadTracking;
        Head.cullingMask = ~0;
        Head.clearFlags = CameraClearFlags.Skybox;
        PlacePanel(_anchorPosition, _anchorRotation);
    }

    void AlignToAnchor()
    {
        var delta = Mathf.DeltaAngle(Head.transform.eulerAngles.y, _anchorRotation.eulerAngles.y);
        _origin.rotation = Quaternion.Euler(0, delta, 0) * _origin.rotation;
        _origin.position += _anchorPosition - Head.transform.position;
    }

    public void ShowRoom()
    {
        InEnvironment = false;
        _alignPending = false;
        Head.cullingMask = 1 << 5;
        Head.clearFlags = CameraClearFlags.SolidColor;
        Head.backgroundColor = new Color(.169f, .169f, .169f);
        PlacePanel(Head.transform.position, Quaternion.Euler(0, Head.transform.eulerAngles.y, 0));
    }

    void PlacePanel(Vector3 eye, Quaternion facing)
    {
        _canvas.transform.SetPositionAndRotation(eye + facing * Vector3.forward * 2, facing);
    }

    public void ScreenChanged()
    {
        // Let go before selecting on a new page, and don't show rays during recordings.
        _screenInputReadyAt = _nextMove = Time.unscaledTime + .25f;
    }

    void Update()
    {
        if (Head == null) return;
        var device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(XRNode.Head);
        var tracked = device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out var isTracked) && isTracked;
        var present = !device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.userPresence, out var presence) || presence;
        if (_hadTracking && (!tracked || !present) && _wasPresent) SuspendStudy("HeadsetUnavailable");
        _wasPresent = tracked && present;
        if (tracked && !_hadTracking)
        {
            _hadTracking = true;
            if (!InEnvironment) ShowRoom();
        }
        if (_alignPending && tracked) { AlignToAnchor(); _alignPending = false; }
        if (!Suspended) StudyTime += Time.unscaledDeltaTime;
        _canvas.GetComponentsInChildren(false, _selectables);
        var buttons = _selectables;
        bool interactive = false;
        foreach (var button in buttons) if (button.IsInteractable()) { interactive = true; break; }
        foreach (var ray in _rays) ray.gameObject.SetActive(interactive);
        if (Suspended) _ui.SetResumeAvailable(_focused && !_paused && (!_hadTracking || (tracked && present)));
        if (!interactive || Time.unscaledTime < _screenInputReadyAt || EventSystem.current == null) return;
        var axis = _navigate.ReadValue<Vector2>();
        if (axis.sqrMagnitude > .36f && Time.unscaledTime >= _nextMove)
        {
            Navigate(axis, buttons);
            _nextMove = Time.unscaledTime + .25f;
        }
        if (_submit.WasPressedThisFrame())
        {
            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null) ExecuteEvents.Execute(selected, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        }
    }

    public static void Navigate(Vector2 axis, IList<Selectable> buttons)
    {
        var system = EventSystem.current;
        if (system == null) return;
        var selected = system.currentSelectedGameObject;
        if (selected == null || !selected.activeInHierarchy)
        {
            foreach (var button in buttons) if (button.IsInteractable()) { button.Select(); return; }
            return;
        }
        var direction = Mathf.Abs(axis.x) > Mathf.Abs(axis.y)
            ? (axis.x > 0 ? MoveDirection.Right : MoveDirection.Left)
            : (axis.y > 0 ? MoveDirection.Up : MoveDirection.Down);
        ExecuteEvents.Execute(selected, new AxisEventData(system) { moveVector = axis, moveDir = direction }, ExecuteEvents.moveHandler);
    }

    void OnApplicationFocus(bool focused) { _focused = focused; if (!focused && (Application.platform == RuntimePlatform.Android || _hadTracking)) SuspendStudy("ApplicationFocusLost"); }
    void OnApplicationPause(bool paused) { _paused = paused; if (paused && (Application.platform == RuntimePlatform.Android || _hadTracking)) SuspendStudy("ApplicationPaused"); }

    void SuspendStudy(string reason)
    {
        if (_ui == null || Suspended) return;
        Suspended = true;
        GetComponent<ExperimentController>().RecordInterruption(reason);
        AudioListener.pause = true;
        _panelBeforePause = _canvas.transform.position;
        _panelRotationBeforePause = _canvas.transform.rotation;
        PlacePanel(Head.transform.position, Quaternion.Euler(0, Head.transform.eulerAngles.y, 0));
        _ui.ShowVRPause(() =>
        {
            if (!_focused || _paused || (_hadTracking && !_wasPresent)) return;
            GetComponent<ExperimentController>().RecordInterruption("SessionResumed");
            Suspended = false;
            AudioListener.pause = false;
            if (InEnvironment) _canvas.transform.SetPositionAndRotation(_panelBeforePause, _panelRotationBeforePause);
            else ShowRoom();
            _ui.HideVRPause();
        });
    }

    void OnDestroy()
    {
        foreach (var action in _actions) action.Dispose();
        if (Instance == this) Instance = null;
        AudioListener.pause = false;
    }
}
