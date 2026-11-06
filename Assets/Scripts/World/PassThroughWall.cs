using UnityEngine;
using UnityEngine.Events;

namespace Metroidbrainia
{
    // Evaluate after player movement and gameplay camera updates.
    [DefaultExecutionOrder(300)]
    [DisallowMultipleComponent]
    public sealed class PassThroughWall : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private MeshRenderer wallRenderer;
        [SerializeField] private BoxCollider wallCollider;
        [SerializeField] private Material transparentMaterial;
        [SerializeField] private Transform originalSidePoint;
        [SerializeField] private Transform otherSidePoint;
        [SerializeField, Min(0f)] private float sideClearance = 0.3f;
        [Tooltip("Ignore projected triangle fragments smaller than this screen area in pixels. Zero accepts any nonzero area.")]
        [SerializeField, Min(0f)] private float minimumVisibleAreaPixels = 0.5f;
        [SerializeField] private UnityEvent onCrossedToOtherSide = new UnityEvent();

        private enum PlayerSide
        {
            Original,
            Crossing,
            Other
        }

        private readonly Plane[] frustumPlanes = new Plane[6];
        private CharacterController playerController;
        private MeshFilter wallMeshFilter;
        private Material[] originalMaterials;
        private Material[] transparentMaterials;
        private PlayerSide currentSide;
        private bool initialized;
        private bool appearanceApplied;
        private bool crossingFromOriginal;
        private bool crossingTouchedWall;
        private bool crossingEventSent;
        private Vector3[] visibilityVertices;
        private Vector3[] worldVisibilityVertices;
        private int[] visibilityTriangles;
        private readonly Vector3[] clipPolygonA = new Vector3[16];
        private readonly Vector3[] clipPolygonB = new Vector3[16];
        private static readonly int[] BoxTriangles =
        {
            0, 2, 6, 0, 6, 4, 1, 5, 7, 1, 7, 3,
            0, 4, 5, 0, 5, 1, 2, 3, 7, 2, 7, 6,
            0, 1, 3, 0, 3, 2, 4, 6, 7, 4, 7, 5
        };

        public bool IsVisible { get; private set; }
        public bool IsPassable { get; private set; }
        public bool IsOnOriginalSide => currentSide == PlayerSide.Original;
        public bool IsCrossing => currentSide == PlayerSide.Crossing;

        private void Reset()
        {
            wallRenderer = GetComponent<MeshRenderer>();
            wallCollider = GetComponent<BoxCollider>();
        }

        private void Start()
        {
            if (wallRenderer == null)
                wallRenderer = GetComponent<MeshRenderer>();
            if (wallCollider == null)
                wallCollider = GetComponent<BoxCollider>();
            if (player != null)
                playerController = player.GetComponent<CharacterController>();

            if (player == null || playerController == null || gameplayCamera == null
                || wallRenderer == null || wallCollider == null || transparentMaterial == null
                || originalSidePoint == null || otherSidePoint == null || wallCollider.isTrigger)
            {
                Debug.LogError("Assign PlayerCapsule with CharacterController, gameplay Camera, wall MeshRenderer, non-trigger BoxCollider, transparent Material and both side points.", this);
                enabled = false;
                return;
            }

            wallMeshFilter = wallRenderer.GetComponent<MeshFilter>();
            CacheVisibilityGeometry();
            GetWallGeometry(out Vector3 center, out _, out _, out _);
            Vector3 direction = otherSidePoint.position - originalSidePoint.position;
            if (direction.sqrMagnitude < 0.000001f
                || Vector3.Dot(direction, originalSidePoint.position - center) >= 0f
                || Vector3.Dot(direction, otherSidePoint.position - center) <= 0f)
            {
                Debug.LogError("Side points must be distinct and on opposite sides of the wall's center.", this);
                enabled = false;
                return;
            }

            originalMaterials = wallRenderer.sharedMaterials;
            transparentMaterials = new Material[originalMaterials.Length];
            for (int i = 0; i < transparentMaterials.Length; i++)
                transparentMaterials[i] = transparentMaterial;

            Vector3 normal = direction.normalized;
            float distance = Vector3.Dot(playerController.bounds.center - center, normal);
            currentSide = distance <= 0f ? PlayerSide.Original : PlayerSide.Other;
            initialized = true;
            EvaluateWall(true);
        }

        private void LateUpdate()
        {
            if (!initialized || player == null || playerController == null || !playerController.enabled
                || !player.gameObject.activeInHierarchy
                || gameplayCamera == null || wallRenderer == null || wallCollider == null
                || originalSidePoint == null || otherSidePoint == null)
                return;

            EvaluateWall(false);
        }

        private void EvaluateWall(bool starting)
        {
            Vector3 direction = otherSidePoint.position - originalSidePoint.position;
            if (direction.sqrMagnitude < 0.000001f)
                return;
            Vector3 normal = direction.normalized;

            GetWallGeometry(out Vector3 center, out Vector3 halfX, out Vector3 halfY, out Vector3 halfZ);
            float wallHalfThickness = Mathf.Abs(Vector3.Dot(normal, halfX))
                + Mathf.Abs(Vector3.Dot(normal, halfY)) + Mathf.Abs(Vector3.Dot(normal, halfZ));
            Bounds playerBounds = playerController.bounds;
            float playerHalfThickness = Vector3.Dot(Abs(normal), playerBounds.extents);
            float distance = Vector3.Dot(playerBounds.center - center, normal);
            float clearanceDistance = wallHalfThickness + playerHalfThickness + Mathf.Max(0f, sideClearance);
            bool clearOfWall = Mathf.Abs(distance) > clearanceDistance;

            // Reconstruct the bounds: Collider.bounds is empty when the wall is disabled.
            Bounds wallBounds = new Bounds(center, (Abs(halfX) + Abs(halfY) + Abs(halfZ)) * 2f);
            Bounds safetyBounds = wallBounds;
            safetyBounds.Expand(Mathf.Max(0f, sideClearance) * 2f);
            bool nearWall = safetyBounds.Intersects(playerBounds);
            bool touchesWall = wallBounds.Intersects(playerBounds)
                && Mathf.Abs(distance) < wallHalfThickness + playerHalfThickness;
            bool completedSpecialCrossing = false;

            if (currentSide == PlayerSide.Crossing)
            {
                crossingTouchedWall |= touchesWall;
                if (clearOfWall)
                {
                    currentSide = distance < 0f ? PlayerSide.Original : PlayerSide.Other;
                    completedSpecialCrossing = currentSide == PlayerSide.Other && crossingFromOriginal && crossingTouchedWall;
                    crossingFromOriginal = false;
                    crossingTouchedWall = false;
                }
            }
            else if (!clearOfWall && nearWall && (IsPassable || (starting
                && wallBounds.Intersects(playerBounds)
                && Mathf.Abs(distance) < wallHalfThickness + playerHalfThickness)))
            {
                crossingFromOriginal = !starting && currentSide == PlayerSide.Original && IsPassable;
                crossingTouchedWall = touchesWall;
                currentSide = PlayerSide.Crossing;
            }
            else if (clearOfWall)
            {
                currentSide = distance < 0f ? PlayerSide.Original : PlayerSide.Other;
            }

            GeometryUtility.CalculateFrustumPlanes(gameplayCamera, frustumPlanes);
            IsVisible = IntersectsFrustum();
            SetPassable(currentSide != PlayerSide.Original || !IsVisible);
            if (completedSpecialCrossing && !crossingEventSent)
            {
                crossingEventSent = true;
                onCrossedToOtherSide?.Invoke();
            }
        }

        private void CacheVisibilityGeometry()
        {
            Mesh mesh = wallMeshFilter != null ? wallMeshFilter.sharedMesh : null;
            if (mesh != null && mesh.isReadable)
            {
                visibilityVertices = mesh.vertices;
                visibilityTriangles = mesh.triangles;
            }
            else
            {
                // Unreadable meshes still get an exact frustum test of their oriented local bounds.
                Bounds bounds = mesh != null ? mesh.bounds : wallRenderer.localBounds;
                visibilityVertices = new Vector3[8];
                for (int i = 0; i < visibilityVertices.Length; i++)
                {
                    Vector3 sign = new Vector3((i & 1) == 0 ? -1f : 1f,
                        (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f);
                    visibilityVertices[i] = bounds.center + Vector3.Scale(bounds.extents, sign);
                }
                visibilityTriangles = BoxTriangles;
            }
            worldVisibilityVertices = new Vector3[visibilityVertices.Length];
        }

        private bool IntersectsFrustum()
        {
            Bounds localBounds = wallMeshFilter != null && wallMeshFilter.sharedMesh != null
                ? wallMeshFilter.sharedMesh.bounds : wallRenderer.localBounds;
            Transform meshTransform = wallRenderer.transform;
            Vector3 center = meshTransform.TransformPoint(localBounds.center);
            Vector3 extents = localBounds.extents;
            // TransformVector includes rotation, scale and the parent transform hierarchy.
            Vector3 halfX = meshTransform.TransformVector(new Vector3(extents.x, 0f, 0f));
            Vector3 halfY = meshTransform.TransformVector(new Vector3(0f, extents.y, 0f));
            Vector3 halfZ = meshTransform.TransformVector(new Vector3(0f, 0f, extents.z));

            for (int i = 0; i < frustumPlanes.Length; i++)
            {
                Plane plane = frustumPlanes[i];
                float radius = Mathf.Abs(Vector3.Dot(plane.normal, halfX))
                    + Mathf.Abs(Vector3.Dot(plane.normal, halfY))
                    + Mathf.Abs(Vector3.Dot(plane.normal, halfZ));
                if (plane.GetDistanceToPoint(center) + radius < 0f)
                    return false;
            }
            // Six OBB/plane tests alone are conservative near frustum edges. Clip the actual
            // mesh triangles to all planes and require a non-degenerate projected fragment.
            Matrix4x4 matrix = meshTransform.localToWorldMatrix;
            for (int i = 0; i < visibilityVertices.Length; i++)
                worldVisibilityVertices[i] = matrix.MultiplyPoint3x4(visibilityVertices[i]);
            float threshold = Mathf.Max(0f, minimumVisibleAreaPixels);
            for (int i = 0; i + 2 < visibilityTriangles.Length; i += 3)
            {
                if (ClippedTriangleAreaPixels(worldVisibilityVertices[visibilityTriangles[i]],
                    worldVisibilityVertices[visibilityTriangles[i + 1]],
                    worldVisibilityVertices[visibilityTriangles[i + 2]]) > threshold)
                    return true;
            }
            return false;
        }

        private float ClippedTriangleAreaPixels(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3[] input = clipPolygonA;
            Vector3[] output = clipPolygonB;
            input[0] = a;
            input[1] = b;
            input[2] = c;
            int count = 3;
            for (int planeIndex = 0; planeIndex < frustumPlanes.Length; planeIndex++)
            {
                Plane plane = frustumPlanes[planeIndex];
                int outputCount = 0;
                Vector3 previous = input[count - 1];
                float previousDistance = plane.GetDistanceToPoint(previous);
                for (int i = 0; i < count; i++)
                {
                    Vector3 current = input[i];
                    float currentDistance = plane.GetDistanceToPoint(current);
                    bool inside = currentDistance >= 0f;
                    if (inside != (previousDistance >= 0f))
                        output[outputCount++] = previous + (current - previous)
                            * (previousDistance / (previousDistance - currentDistance));
                    if (inside)
                        output[outputCount++] = current;
                    previous = current;
                    previousDistance = currentDistance;
                }
                count = outputCount;
                if (count < 3)
                    return 0f;
                Vector3[] swap = input;
                input = output;
                output = swap;
            }

            Vector3 first = gameplayCamera.WorldToViewportPoint(input[0]);
            Vector3 last = gameplayCamera.WorldToViewportPoint(input[1]) - first;
            float twiceArea = 0f;
            for (int i = 2; i < count; i++)
            {
                Vector3 next = gameplayCamera.WorldToViewportPoint(input[i]) - first;
                twiceArea += last.x * next.y - last.y * next.x;
                last = next;
            }
            return Mathf.Abs(twiceArea) * 0.5f * gameplayCamera.pixelWidth * gameplayCamera.pixelHeight;
        }

        private void GetWallGeometry(out Vector3 center, out Vector3 halfX, out Vector3 halfY, out Vector3 halfZ)
        {
            Transform colliderTransform = wallCollider.transform;
            Vector3 halfSize = wallCollider.size * 0.5f;
            center = colliderTransform.TransformPoint(wallCollider.center);
            halfX = colliderTransform.TransformVector(new Vector3(halfSize.x, 0f, 0f));
            halfY = colliderTransform.TransformVector(new Vector3(0f, halfSize.y, 0f));
            halfZ = colliderTransform.TransformVector(new Vector3(0f, 0f, halfSize.z));
        }

        private void SetPassable(bool passable)
        {
            if (appearanceApplied && IsPassable == passable)
                return;

            IsPassable = passable;
            wallCollider.enabled = !passable;
            wallRenderer.sharedMaterials = passable ? transparentMaterials : originalMaterials;
            appearanceApplied = true;
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }
    }
}
