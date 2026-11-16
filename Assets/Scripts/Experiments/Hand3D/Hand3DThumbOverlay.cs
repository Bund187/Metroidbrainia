using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Metroidbrainia.Hand3DExperiment
{
    // A moving stencil for a second sample of the ORIGINAL pixel arm texture, not a second arm renderer.
    [RequireComponent(typeof(CanvasRenderer), typeof(Mask))]
    public sealed class Hand3DThumbOverlay : MaskableGraphic
    {
        public enum VisualizationMode { ThumbOverlay, FullArm, ThumbMask }
        [SerializeField] private VisualizationMode visualization = VisualizationMode.ThumbOverlay;
        [SerializeField] private Color maskColor = new Color(1f, 0f, 1f, 1f);
        [SerializeField] private SkinnedMeshRenderer thumbRenderer;
        [SerializeField] private Camera armCamera;
        [SerializeField] private RawImage armDisplay;
        [SerializeField] private RawImage thumbImage;
        [Tooltip("Generated in the Editor from thumb skin weights; no readable FBX is required in a build.")]
        [SerializeField, HideInInspector] private int[] thumbTriangles = new int[0];
        private Mesh baked;
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> projected = new List<Vector3>();
        private readonly HashSet<int> projectedIndices = new HashSet<int>();
        public int GeneratedVertexCount { get; private set; }
        public int GeneratedTriangleCount => GeneratedVertexCount / 3;
        public string PixelDiagnostic { get; private set; } = "Not sampled. Use Sample Source Pixels during HandHold.";
        public VisualizationMode Visualization { get => visualization; set { visualization = value; RefreshMask(); } }
        public RawImage ThumbImage => thumbImage;
        public RawImage ArmDisplay => armDisplay;
        public bool HasTexture => armDisplay != null && armDisplay.texture != null;
        public bool HasValidMask => IsConfigured && GetComponent<Mask>().graphic == this;
        public int TriangleCount => thumbTriangles.Length / 3;
        public bool IsConfigured => thumbRenderer != null && armCamera != null && armDisplay != null
            && thumbImage != null && thumbTriangles.Length > 0;

        public void RefreshMask() => RefreshMask(false);

        private void RefreshMask(bool sampleThumb)
        {
            if (!isActiveAndEnabled) return;
            SynchronizeImageLayout();
            ApplyVisualization();
            projected.Clear();
            GeneratedVertexCount = 0;
            SetVerticesDirty();
            if (!IsConfigured || visualization == VisualizationMode.FullArm && !sampleThumb) return;
            if (baked == null) baked = new Mesh { name = "Temporary posed thumb mask", hideFlags = HideFlags.DontSave };
            // On this scaled rig, false leaves scale in the snapshot and TransformPoint applies it again.
            // true produces the renderer-local snapshot needed by the local-to-world projection below.
            thumbRenderer.BakeMesh(baked, true);
            baked.GetVertices(vertices);
            Rect sourceRect = armDisplay.rectTransform.rect;
            Rect uvRect = armDisplay.uvRect;
            if (Mathf.Abs(uvRect.width) < 0.00001f || Mathf.Abs(uvRect.height) < 0.00001f) return;
            // Keep mesh indices for the stencil and pixel diagnostic, but only project vertices
            // referenced by thumb triangles. Rebuild the set each refresh so edited indices stay valid.
            for (int i = 0; i < vertices.Count; i++) projected.Add(Vector3.zero);
            projectedIndices.Clear();
            foreach (int index in thumbTriangles)
            {
                if (index < 0 || index >= vertices.Count || !projectedIndices.Add(index)) continue;
                Vector3 vertex = vertices[index];
                Vector3 viewport = armCamera.WorldToViewportPoint(thumbRenderer.transform.TransformPoint(vertex));
                Vector2 uv = new Vector2((viewport.x - uvRect.x) / uvRect.width, (viewport.y - uvRect.y) / uvRect.height);
                Vector3 local = rectTransform.InverseTransformPoint(armDisplay.rectTransform.TransformPoint(sourceRect.min + Vector2.Scale(uv, sourceRect.size)));
                local.z = viewport.z;
                projected[index] = local;
            }
        }

        public void SynchronizeImageLayout()
        {
            if (armDisplay == null || thumbImage == null) return;
            var source = armDisplay.rectTransform;
            var target = thumbImage.rectTransform;
            // The stencil root stretches over the same parent as the original RawImage.
            target.anchorMin = source.anchorMin; target.anchorMax = source.anchorMax; target.pivot = source.pivot;
            target.sizeDelta = source.sizeDelta; target.anchoredPosition3D = source.anchoredPosition3D;
            target.localRotation = source.localRotation; target.localScale = source.localScale;
            thumbImage.texture = armDisplay.texture;
            thumbImage.uvRect = armDisplay.uvRect;
            thumbImage.color = armDisplay.color;
            thumbImage.material = armDisplay.material;
        }

        private void ApplyVisualization()
        {
            var stencil = GetComponent<Mask>();
            bool useMask = visualization != VisualizationMode.FullArm;
            if (stencil.enabled != useMask) stencil.enabled = useMask;
            stencil.showMaskGraphic = visualization == VisualizationMode.ThumbMask;
            if (thumbImage != null)
            {
                thumbImage.gameObject.SetActive(true);
                thumbImage.enabled = visualization != VisualizationMode.ThumbMask;
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            GeneratedVertexCount = 0;
            if (visualization == VisualizationMode.FullArm || !IsConfigured || !thumbRenderer.enabled) return;
            for (int i = 0; i + 2 < thumbTriangles.Length; i += 3)
            {
                int a = thumbTriangles[i], b = thumbTriangles[i + 1], c = thumbTriangles[i + 2];
                if (a < 0 || b < 0 || c < 0 || a >= projected.Count || b >= projected.Count || c >= projected.Count) continue;
                Vector3 pa = projected[a], pb = projected[b], pc = projected[c];
                if (pa.z <= armCamera.nearClipPlane || pb.z <= armCamera.nearClipPlane || pc.z <= armCamera.nearClipPlane) continue;
                int start = vh.currentVertCount;
                Color tint = visualization == VisualizationMode.ThumbMask ? maskColor : Color.white;
                vh.AddVert(new Vector3(pa.x, pa.y), tint, Vector2.zero);
                vh.AddVert(new Vector3(pb.x, pb.y), tint, Vector2.zero);
                vh.AddVert(new Vector3(pc.x, pc.y), tint, Vector2.zero);
                vh.AddTriangle(start, start + 1, start + 2);
            }
            GeneratedVertexCount = vh.currentVertCount;
        }

        // Explicit, synchronous diagnostic only. Never reads the GPU back in the animation loop.
        public void SampleSourcePixels()
        {
            if (!isActiveAndEnabled || !IsConfigured || !HasTexture)
            { PixelDiagnostic = "Overlay inactive, missing configuration or missing texture."; return; }
            // Full Arm skips animation-loop mask work, but explicit pixel sampling still needs it.
            RefreshMask(true);
            Canvas.ForceUpdateCanvases();
            var source = armDisplay.texture;
            if (source.width <= 0 || source.height <= 0 || armDisplay.rectTransform.rect.width <= 0f
                || armDisplay.rectTransform.rect.height <= 0f || source is RenderTexture renderTexture && !renderTexture.IsCreated())
            { PixelDiagnostic = "Texture not created or arm display has no drawable area."; return; }
            var previous = RenderTexture.active;
            var target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            Texture2D pixels = null;
            try
            {
                Graphics.Blit(source, target);
                RenderTexture.active = target;
                pixels = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                pixels.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                var colors = pixels.GetPixels32();
                int covered = 0, opaque = 0;
                var visited = new HashSet<int>();
                for (int i = 0; i + 2 < thumbTriangles.Length; i += 3)
                {
                    int ia = thumbTriangles[i], ib = thumbTriangles[i + 1], ic = thumbTriangles[i + 2];
                    if (ia < 0 || ib < 0 || ic < 0 || ia >= projected.Count || ib >= projected.Count || ic >= projected.Count) continue;
                    if (projected[ia].z <= armCamera.nearClipPlane || projected[ib].z <= armCamera.nearClipPlane || projected[ic].z <= armCamera.nearClipPlane) continue;
                    Vector2 a = TexturePixel(projected[ia], source), b = TexturePixel(projected[ib], source), c = TexturePixel(projected[ic], source);
                    float area = Cross(b - a, c - a);
                    if (Mathf.Abs(area) < 0.00001f) continue;
                    int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
                    int maxX = Mathf.Min(source.width - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
                    int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
                    int maxY = Mathf.Min(source.height - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
                    for (int y = minY; y <= maxY; y++)
                        for (int x = minX; x <= maxX; x++)
                        {
                            var p = new Vector2(x + 0.5f, y + 0.5f);
                            if (Cross(b - a, p - a) / area < 0f || Cross(c - b, p - b) / area < 0f || Cross(a - c, p - c) / area < 0f) continue;
                            int index = y * source.width + x;
                            if (!visited.Add(index)) continue;
                            covered++;
                            if (colors[index].a > 0 && thumbImage.color.a > 0f) opaque++;
                        }
                }
                PixelDiagnostic = $"Source alpha pixels inside thumb: {opaque}/{covered}. Snapshot at frame {Time.frameCount}; this does not verify final Canvas composition.";
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                if (pixels != null) { if (Application.isPlaying) Destroy(pixels); else DestroyImmediate(pixels); }
            }
        }

        private Vector2 TexturePixel(Vector3 point, Texture texture)
        {
            Vector3 local = armDisplay.rectTransform.InverseTransformPoint(rectTransform.TransformPoint(new Vector3(point.x, point.y)));
            Rect rect = armDisplay.rectTransform.rect;
            Rect uv = armDisplay.uvRect;
            return Vector2.Scale(uv.position + Vector2.Scale(new Vector2((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height), uv.size), new Vector2(texture.width, texture.height));
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        protected override void OnDestroy()
        {
            if (baked != null)
            {
                if (Application.isPlaying) Destroy(baked); else DestroyImmediate(baked);
            }
            base.OnDestroy();
        }
    }
}
