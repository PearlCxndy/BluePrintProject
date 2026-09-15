using UnityEngine;

/// <summary>Quiet, repeatable environmental motion with fixed tree roots and viewpoint.</summary>
public class NatureAmbientMotion : MonoBehaviour
{
    public Transform[] trees = new Transform[0];
    public Renderer lake;
    [Range(0f, 2f)] public float swayDegrees = .65f;
    [Range(.05f, 1f)] public float swayCyclesPerSecond = .18f;
    [Range(0f, 2f)] public float rippleSpeed = .7f;

    Quaternion[] _restRotations;
    MaterialPropertyBlock _waterProperties;
    float _elapsed;
    static readonly int RippleTime = Shader.PropertyToID("_RippleTime");

    void OnEnable() { _elapsed = 0; CacheRestPose(); }
    void Update() { _elapsed += Time.deltaTime; ApplyAtTime(_elapsed); }
    void OnDisable() { RestoreRestPose(); }

    void CacheRestPose()
    {
        _restRotations = new Quaternion[trees.Length];
        for (var i = 0; i < trees.Length; i++)
            if (trees[i] != null) _restRotations[i] = trees[i].localRotation;
        if (_waterProperties == null) _waterProperties = new MaterialPropertyBlock();
    }

    // Explicit time also allows editor verification without running the full study.
    public void ApplyAtTime(float seconds)
    {
        if (_restRotations == null || _restRotations.Length != trees.Length) CacheRestPose();
        var windTime = seconds * swayCyclesPerSecond * Mathf.PI * 2;
        for (var i = 0; i < trees.Length; i++)
        {
            var tree = trees[i];
            if (tree == null) continue;
            var phase = tree.position.x * .19f + tree.position.z * .13f;
            var lean = Mathf.Sin(windTime + phase) * swayDegrees;
            var cross = Mathf.Sin(windTime * .73f + phase * 1.3f) * swayDegrees * .35f;
            tree.localRotation = _restRotations[i] * Quaternion.Euler(cross, 0, lean);
        }
        if (lake != null)
        {
            lake.GetPropertyBlock(_waterProperties);
            _waterProperties.SetFloat(RippleTime, seconds * rippleSpeed);
            lake.SetPropertyBlock(_waterProperties);
        }
    }

    public void RestoreRestPose()
    {
        if (_restRotations != null)
            for (var i = 0; i < Mathf.Min(trees.Length, _restRotations.Length); i++)
                if (trees[i] != null) trees[i].localRotation = _restRotations[i];
        if (lake != null && _waterProperties != null)
        {
            lake.GetPropertyBlock(_waterProperties);
            _waterProperties.SetFloat(RippleTime, 0);
            lake.SetPropertyBlock(_waterProperties);
        }
    }
}
