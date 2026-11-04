using System.Collections.Generic;
using UnityEngine;

namespace Metroidbrainia
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class CurvedScreenMesh : MonoBehaviour
    {
        [SerializeField, Min(0.001f)] private float width = 1f;
        [SerializeField, Min(0.001f)] private float height = 0.75f;
        [Tooltip("Central bulge in local units, towards local -Z, like the front of a Unity Quad.")]
        [SerializeField, Min(0f)] private float curvature = 0.015f;
        [SerializeField, Range(1, 128)] private int horizontalSubdivisions = 24;
        [SerializeField, Range(1, 128)] private int verticalSubdivisions = 18;

        private MeshFilter meshFilter;
        private Mesh originalMesh;
        private Mesh generatedMesh;
        private Vector2[] surfaceUVs;
        private bool needsRebuild;

        public Mesh GeneratedMesh => generatedMesh;
        public IReadOnlyList<Vector2> SurfaceUVs => surfaceUVs;
        public int Revision { get; private set; }

        private void OnEnable()
        {
            meshFilter = GetComponent<MeshFilter>();
            originalMesh = meshFilter.sharedMesh;
            needsRebuild = true;
        }

        private void Update()
        {
            if (needsRebuild || generatedMesh == null)
                Rebuild();
        }

        [ContextMenu("Rebuild Surface")]
        public void Rebuild()
        {
            if (!isActiveAndEnabled)
                return;
            if (meshFilter == null)
                meshFilter = GetComponent<MeshFilter>();
            int columns = Mathf.Clamp(horizontalSubdivisions, 1, 128);
            int rows = Mathf.Clamp(verticalSubdivisions, 1, 128);
            float meshWidth = Mathf.Max(0.001f, width);
            float meshHeight = Mathf.Max(0.001f, height);
            float bulge = Mathf.Max(0f, curvature);
            int stride = columns + 1;
            var vertices = new Vector3[stride * (rows + 1)];
            var normals = new Vector3[vertices.Length];
            surfaceUVs = new Vector2[vertices.Length];
            var triangles = new int[columns * rows * 6];
            for (int y = 0; y <= rows; y++)
            {
                float v = (float)y / rows;
                float ny = 2f * v - 1f;
                for (int x = 0; x <= columns; x++)
                {
                    float u = (float)x / columns;
                    float nx = 2f * u - 1f;
                    int index = y * stride + x;
                    // An elliptic paraboloid stays convex across the entire rectangle.
                    float z = -bulge * (1f - 0.5f * nx * nx - 0.5f * ny * ny);
                    vertices[index] = new Vector3((u - 0.5f) * meshWidth, (v - 0.5f) * meshHeight, z);
                    normals[index] = new Vector3(2f * bulge * nx / meshWidth,
                        2f * bulge * ny / meshHeight, -1f).normalized;
                    surfaceUVs[index] = new Vector2(u, v);
                    if (x == columns || y == rows)
                        continue;
                    int triangle = (y * columns + x) * 6;
                    triangles[triangle] = index;
                    triangles[triangle + 1] = index + stride;
                    triangles[triangle + 2] = index + 1;
                    triangles[triangle + 3] = index + 1;
                    triangles[triangle + 4] = index + stride;
                    triangles[triangle + 5] = index + stride + 1;
                }
            }
            if (generatedMesh == null)
            {
                generatedMesh = new Mesh { name = "Curved Screen (Generated)", hideFlags = HideFlags.DontSave };
                generatedMesh.MarkDynamic();
            }
            else
                generatedMesh.Clear();
            generatedMesh.vertices = vertices;
            generatedMesh.normals = normals;
            generatedMesh.uv = surfaceUVs;
            generatedMesh.triangles = triangles;
            generatedMesh.RecalculateBounds();
            meshFilter.sharedMesh = generatedMesh;
            Revision++;
            needsRebuild = false;
        }

        private void OnValidate()
        {
            width = Mathf.Max(0.001f, width);
            height = Mathf.Max(0.001f, height);
            curvature = Mathf.Max(0f, curvature);
            horizontalSubdivisions = Mathf.Clamp(horizontalSubdivisions, 1, 128);
            verticalSubdivisions = Mathf.Clamp(verticalSubdivisions, 1, 128);
            // OnValidate may run outside the main thread. Mesh work stays in Update.
            needsRebuild = true;
        }

        private void OnDisable()
        {
            if (generatedMesh == null)
                return;
            if (meshFilter != null && meshFilter.sharedMesh == generatedMesh)
                meshFilter.sharedMesh = originalMesh;
            if (Application.isPlaying)
                Destroy(generatedMesh);
            else
                DestroyImmediate(generatedMesh);
            generatedMesh = null;
            surfaceUVs = null;
        }
    }
}
