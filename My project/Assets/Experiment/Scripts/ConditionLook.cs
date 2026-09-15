using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Desktop preview controls. No translation or artificial camera motion.</summary>
public class ConditionLook : MonoBehaviour
{
    [Tooltip("Desktop mouse look multiplier. 2.5 is 0.5 faster than the original 2.0.")]
    public float mouseSensitivity = 2.5f;
    public float keyboardSpeed = 65f;
    public bool IsStill { get; private set; }
    Quaternion _forward;
    float _yaw, _pitch;

    void Awake() { _forward = transform.localRotation; }
    public void SetStill(bool still)
    {
        IsStill = still;
        if (still && ExperimentVR.Instance == null && !UnityEngine.XR.XRSettings.isDeviceActive)
        {
            _yaw = _pitch = 0;
            transform.localRotation = _forward;
        }
    }
    void Update()
    {
        // An XR rig, when installed, owns head tracking. Never lock its real head pose.
        if (ExperimentVR.Instance != null || IsStill || UnityEngine.XR.XRSettings.isDeviceActive) return;
        if (ExperimentInput.RightMouse)
        {
            _yaw += ExperimentInput.MouseDelta.x * mouseSensitivity;
            _pitch -= ExperimentInput.MouseDelta.y * mouseSensitivity;
        }
        _yaw += ((ExperimentInput.Held(Key.RightArrow) ? 1 : 0) - (ExperimentInput.Held(Key.LeftArrow) ? 1 : 0)) * keyboardSpeed * Time.unscaledDeltaTime;
        _pitch += ((ExperimentInput.Held(Key.DownArrow) ? 1 : 0) - (ExperimentInput.Held(Key.UpArrow) ? 1 : 0)) * keyboardSpeed * Time.unscaledDeltaTime;
        _pitch = Mathf.Clamp(_pitch, -65, 65);
        transform.localRotation = _forward * Quaternion.Euler(_pitch, _yaw, 0);
    }
}
