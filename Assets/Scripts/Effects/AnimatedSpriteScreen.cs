using System.Collections.Generic;
using UnityEngine;

namespace Metroidbrainia
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CurvedScreenMesh), typeof(MeshRenderer))]
    public sealed class AnimatedSpriteScreen : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer sourceSpriteRenderer;
        [SerializeField] private bool hideSourceRenderer = true;

        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");
        private readonly Dictionary<Sprite, SpriteMapping> mappings = new Dictionary<Sprite, SpriteMapping>();
        private CurvedScreenMesh surface;
        private MeshRenderer screenRenderer;
        private MaterialPropertyBlock originalProperties;
        private MaterialPropertyBlock properties;
        private SpriteRenderer boundSource;
        private Animator sourceAnimator;
        private AnimatorCullingMode originalCullingMode;
        private bool originalSourceRenderingOff;
        private bool originalScreenRenderingOff;
        private bool sourceOverridden;
        private bool ready;
        private Sprite lastSprite;
        private Texture lastTexture;
        private Color lastColor;
        private bool lastFlipX;
        private bool lastFlipY;
        private int lastRevision = -1;
        private Material lastMaterial;
        private Vector2[] mappedUVs;

        private struct SpriteMapping
        {
            public bool valid;
            public Vector2 origin;
            public Vector2 horizontal;
            public Vector2 vertical;
        }

        private void OnEnable()
        {
            surface = GetComponent<CurvedScreenMesh>();
            screenRenderer = GetComponent<MeshRenderer>();
            originalProperties = new MaterialPropertyBlock();
            properties = new MaterialPropertyBlock();
            screenRenderer.GetPropertyBlock(originalProperties);
            screenRenderer.GetPropertyBlock(properties);
            originalScreenRenderingOff = screenRenderer.forceRenderingOff;
            boundSource = sourceSpriteRenderer;
            if (Application.isPlaying && boundSource != null)
            {
                sourceAnimator = boundSource.GetComponent<Animator>();
                if (sourceAnimator != null)
                {
                    originalCullingMode = sourceAnimator.cullingMode;
                    sourceAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }
                originalSourceRenderingOff = boundSource.forceRenderingOff;
                boundSource.forceRenderingOff = hideSourceRenderer || originalSourceRenderingOff;
                sourceOverridden = true;
            }
            ready = false;
            lastRevision = -1;
            lastMaterial = null;
            lastTexture = null;
        }

        private void LateUpdate()
        {
            if (screenRenderer == null || surface == null || properties == null)
                return;
            Sprite sprite = sourceSpriteRenderer != null ? sourceSpriteRenderer.sprite : null;
            Mesh mesh = surface.GeneratedMesh;
            Material material = screenRenderer.sharedMaterial;
            if (sprite == null || mesh == null || material == null)
            {
                screenRenderer.forceRenderingOff = true;
                ready = false;
                return;
            }
            bool materialChanged = material != lastMaterial;
            if (materialChanged)
            {
                lastMaterial = material;
                if (!material.HasProperty(BaseMap))
                {
                    Debug.LogWarning("Assign a URP/Unlit material with a Base Map to the curved screen.", this);
                    screenRenderer.forceRenderingOff = true;
                    ready = false;
                    return;
                }
            }
            if (!material.HasProperty(BaseMap))
                return;
            bool spriteChanged = !ready || sprite != lastSprite;
            bool uvChanged = spriteChanged || lastRevision != surface.Revision
                || lastFlipX != sourceSpriteRenderer.flipX || lastFlipY != sourceSpriteRenderer.flipY;
            if (uvChanged)
            {
                if (!mappings.TryGetValue(sprite, out SpriteMapping mapping))
                {
                    mapping = BuildMapping(sprite);
                    mappings.Add(sprite, mapping);
                }
                if (!mapping.valid)
                {
                    screenRenderer.forceRenderingOff = true;
                    return;
                }
                IReadOnlyList<Vector2> coordinates = surface.SurfaceUVs;
                if (mappedUVs == null || mappedUVs.Length != coordinates.Count)
                    mappedUVs = new Vector2[coordinates.Count];
                for (int i = 0; i < mappedUVs.Length; i++)
                {
                    Vector2 point = coordinates[i];
                    if (sourceSpriteRenderer.flipX) point.x = 1f - point.x;
                    if (sourceSpriteRenderer.flipY) point.y = 1f - point.y;
                    mappedUVs[i] = mapping.origin + mapping.horizontal * point.x + mapping.vertical * point.y;
                }
                mesh.SetUVs(0, mappedUVs);
                lastRevision = surface.Revision;
                lastFlipX = sourceSpriteRenderer.flipX;
                lastFlipY = sourceSpriteRenderer.flipY;
            }
            Texture texture = sprite.texture;
            Color color = sourceSpriteRenderer.color;
            bool propertiesChanged = !ready || materialChanged || texture != lastTexture || color != lastColor;
            if (propertiesChanged)
            {
                if (!ready || materialChanged || texture != lastTexture)
                    properties.SetTexture(BaseMap, texture);
                properties.SetColor(BaseColor, color);
                properties.SetVector(BaseMapST, new Vector4(1f, 1f, 0f, 0f));
                screenRenderer.SetPropertyBlock(properties);
            }
            screenRenderer.forceRenderingOff = originalScreenRenderingOff;
            lastTexture = texture;
            lastColor = color;
            lastSprite = sprite;
            ready = true;
        }

        private SpriteMapping BuildMapping(Sprite sprite)
        {
            // Tight atlas packing does not reserve a rectangular area for the missing corners.
            if (sprite.packed && sprite.packingMode == SpritePackingMode.Tight)
            {
                Debug.LogWarning("A rectangular screen requires an unpacked spritesheet or rectangle-packed sprites. Tight atlas packing is unsupported.", this);
                return default;
            }
            Vector2[] vertices = sprite.vertices;
            Vector2[] uvs = sprite.uv;
            Rect rect = sprite.rect;
            if (vertices.Length < 3 || vertices.Length != uvs.Length || rect.width <= 0f || rect.height <= 0f)
                return default;
            Vector2 first = NormalizeVertex(vertices[0], sprite, rect);
            for (int i = 1; i < vertices.Length - 1; i++)
            {
                Vector2 a = NormalizeVertex(vertices[i], sprite, rect) - first;
                for (int j = i + 1; j < vertices.Length; j++)
                {
                    Vector2 b = NormalizeVertex(vertices[j], sprite, rect) - first;
                    float determinant = a.x * b.y - a.y * b.x;
                    if (Mathf.Abs(determinant) < 0.000001f)
                        continue;
                    Vector2 uvA = uvs[i] - uvs[0];
                    Vector2 uvB = uvs[j] - uvs[0];
                    Vector2 horizontal = (uvA * b.y - uvB * a.y) / determinant;
                    Vector2 vertical = (uvB * a.x - uvA * b.x) / determinant;
                    return new SpriteMapping
                    {
                        valid = true, horizontal = horizontal, vertical = vertical,
                        origin = uvs[0] - horizontal * first.x - vertical * first.y
                    };
                }
            }
            Debug.LogWarning("The source sprite has no usable UV geometry.", this);
            return default;
        }

        private static Vector2 NormalizeVertex(Vector2 vertex, Sprite sprite, Rect rect)
        {
            Vector2 pixels = vertex * sprite.pixelsPerUnit + sprite.pivot;
            return new Vector2(pixels.x / rect.width, pixels.y / rect.height);
        }

        private void OnDisable()
        {
            if (screenRenderer != null)
            {
                screenRenderer.SetPropertyBlock(originalProperties);
                screenRenderer.forceRenderingOff = originalScreenRenderingOff;
            }
            if (sourceOverridden)
            {
                if (boundSource != null)
                    boundSource.forceRenderingOff = originalSourceRenderingOff;
                if (sourceAnimator != null)
                    sourceAnimator.cullingMode = originalCullingMode;
            }
            sourceOverridden = false;
            mappings.Clear();
            mappedUVs = null;
            ready = false;
        }
    }
}
