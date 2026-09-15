using System;
/// <summary>Subscribe this hook from an LSL bridge to forward task events to EEG.</summary>
public static class ExperimentMarker { public static event Action<string> OnMarker; public static void Emit(string marker) => OnMarker?.Invoke(marker); }
