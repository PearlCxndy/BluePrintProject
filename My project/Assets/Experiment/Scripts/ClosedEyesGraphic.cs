using UnityEngine;
using UnityEngine.UI;

/// <summary>Portable closed-eye cue, independent of operating-system font glyphs.</summary>
public class ClosedEyesGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        foreach (var center in new[] { -135f, 135f })
        {
            for (var i = 0; i < 32; i++)
            {
                var a = i / 32f;
                var b = (i + 1) / 32f;
                Segment(mesh, new Vector2(center + (a - .5f) * 170, -Mathf.Sin(a * Mathf.PI) * 48),
                    new Vector2(center + (b - .5f) * 170, -Mathf.Sin(b * Mathf.PI) * 48), 8);
            }
            for (var i = 1; i <= 3; i++)
            {
                var t = i / 4f;
                var start = new Vector2(center + (t - .5f) * 170, -Mathf.Sin(t * Mathf.PI) * 48);
                Segment(mesh, start, start + new Vector2((t - .5f) * 24, -22), 6);
            }
        }
    }

    void Segment(VertexHelper mesh, Vector2 a, Vector2 b, float width)
    {
        var normal = new Vector2(-(b - a).y, (b - a).x).normalized * width * .5f;
        var index = mesh.currentVertCount;
        mesh.AddVert(a - normal, color, Vector2.zero);
        mesh.AddVert(a + normal, color, Vector2.zero);
        mesh.AddVert(b + normal, color, Vector2.zero);
        mesh.AddVert(b - normal, color, Vector2.zero);
        mesh.AddTriangle(index, index + 1, index + 2);
        mesh.AddTriangle(index, index + 2, index + 3);
    }
}
