using UnityEngine;

/// <summary>
/// Builds a flat ring (a disc with a hole) for Saturn's rings.
/// The ring texture (2k_saturn_ring_alpha.png) is a thin strip: its left edge is the
/// inside of the ring and its right edge the outside, so U runs from inner to outer radius.
/// Put this on a child of Saturn with a MeshFilter + MeshRenderer and the "Saturn Rings" material.
/// Radii are in the child's local units (Saturn's sphere has radius 0.5 there).
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SaturnRing : MonoBehaviour
{
    [Tooltip("Inner edge of the ring (Saturn's surface is at 0.5)")]
    public float innerRadius = 0.6f;
    [Tooltip("Outer edge of the ring")]
    public float outerRadius = 1.15f;
    [Range(16, 256)] public int segments = 128;

    Mesh mesh;

    void OnEnable() => Build();

#if UNITY_EDITOR
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () => { if (this != null && isActiveAndEnabled) Build(); };
    }
#endif

    void Build()
    {
        if (mesh == null)
        {
            mesh = new Mesh { name = "Saturn Ring (generated)", hideFlags = HideFlags.DontSave };
        }
        mesh.Clear();

        int n = Mathf.Max(16, segments);
        var vertices = new Vector3[(n + 1) * 2];
        var normals = new Vector3[vertices.Length];
        var uvs = new Vector2[vertices.Length];
        var triangles = new int[n * 6];

        for (int i = 0; i <= n; i++)
        {
            float a = (float)i / n * Mathf.PI * 2f;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            int v = i * 2;
            vertices[v] = dir * innerRadius;
            vertices[v + 1] = dir * outerRadius;
            normals[v] = normals[v + 1] = Vector3.up;
            float along = (float)i / n;
            uvs[v] = new Vector2(0f, along);      // inner edge = left of the texture
            uvs[v + 1] = new Vector2(1f, along);  // outer edge = right of the texture
        }

        for (int i = 0; i < n; i++)
        {
            int v = i * 2, t = i * 6;
            triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
            triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        GetComponent<MeshFilter>().sharedMesh = mesh;
    }
}
