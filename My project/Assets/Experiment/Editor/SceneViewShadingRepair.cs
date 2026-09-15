using System;
using System.IO;
using UnityEditor;

/// <summary>Recovers an obsolete Scene view debug mode stored by an older editor layout.</summary>
public static class SceneViewShadingRepair
{
    [InitializeOnLoadMethod]
    static void AfterReload() { EditorApplication.delayCall += RepairUnsupportedMode; }

    [MenuItem("Experiment/Repair Scene View Shading")]
    public static void RepairUnsupportedMode()
    {
        var repaired = 0;
        var views = 0;
        var modes = "";
        foreach (SceneView view in SceneView.sceneViews)
        {
            if (view == null) continue;
            views++;
            if (view.cameraMode.name == "Shadow Cascades")
            {
                view.cameraMode = SceneView.GetBuiltinCameraMode(DrawCameraMode.Textured);
                view.Repaint();
                repaired++;
            }
            modes += view.cameraMode.name + "; ";
        }
        Directory.CreateDirectory("Temp/ExperimentPreviews");
        File.WriteAllText("Temp/ExperimentPreviews/sceneview-repair.txt",
            DateTime.Now.ToString("o") + ": inspected " + views + " Scene views; repaired " + repaired + " obsolete modes. Current modes: " + modes);
        if (repaired > 0) UnityEngine.Debug.Log("Scene view repaired: switched obsolete Shadow Cascades mode to Shaded.");
    }
}
