using System.Collections.Generic;
using UnityEngine;

/// <summary>Saved scene entry point. The study keeps a fixed seated position.</summary>
public class ConditionEnvironment : MonoBehaviour
{
    public ConditionType condition;
    public Camera viewCamera;
    public ConditionLook look;
    [Tooltip("Optional approved recording. Leave empty for silent conditions.")]
    public AudioClip soundscape;
    [Range(0f, 1f)] public float soundscapeVolume = .3f;

    AudioSource _source;
    readonly List<GameObject> _hidden = new List<GameObject>();

    /// <summary>
    /// Condition scenes are usually left open in the editor next to the host scene while
    /// their skyboxes and lighting are dressed. Hiding on Awake keeps the participant on
    /// the UI until the session reaches the condition, whichever scenes happen to be open.
    /// </summary>
    void Awake() => SetVisible(false);

    /// <summary>
    /// Every Awake has run by now, including the controller AutoStart adds. With no controller
    /// the scene was opened on its own to look around in, so it shows itself straight away.
    /// </summary>
    void Start() { if (FindFirstObjectByType<ExperimentController>() == null) SetVisible(true); }

    public void Begin()
    {
        SetVisible(true);
        look.SetStill(false);
        if (soundscape != null)
        {
            if (_source == null)
            {
                _source = gameObject.AddComponent<AudioSource>();
                _source.clip = soundscape;
                _source.volume = soundscapeVolume;
                _source.loop = true;
            }
            _source.Play();
        }
    }

    /// <summary>Shows or hides the whole environment: geometry, lights, camera and listener.</summary>
    public void SetVisible(bool visible)
    {
        if (visible)
        {
            foreach (var child in _hidden) if (child != null) child.SetActive(true);
            _hidden.Clear();
            if (viewCamera != null) viewCamera.enabled = ExperimentVR.Instance == null;
            if (ExperimentVR.Instance != null)
                foreach (var listener in GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            return;
        }
        if (_source != null) _source.Stop();
        if (viewCamera != null) viewCamera.enabled = false;
        foreach (Transform child in transform)
        {
            if (!child.gameObject.activeSelf) continue;
            child.gameObject.SetActive(false);
            _hidden.Add(child.gameObject);
        }
    }
}
