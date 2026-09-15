using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Desktop preview input through the same Input System used by OpenXR.</summary>
public static class ExperimentInput
{
    public static bool Held(Key key) => Keyboard.current != null && Keyboard.current[key].isPressed;
    public static bool Pressed(Key key) => Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
    public static bool RightMouse => Mouse.current != null && Mouse.current.rightButton.isPressed;
    // Match the old mouse axis scale approximately; Input System reports pixels.
    public static Vector2 MouseDelta => Mouse.current == null ? Vector2.zero : Mouse.current.delta.ReadValue() * .1f;
}
