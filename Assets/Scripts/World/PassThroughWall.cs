using UnityEngine;

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

            if (currentSide == PlayerSide.Crossing)
            {
                if (clearOfWall)
                    currentSide = distance < 0f ? PlayerSide.Original : PlayerSide.Other;
            }
            else if (!clearOfWall && nearWall && (IsPassable || (starting
                && wallBounds.Intersects(playerBounds)
                && Mathf.Abs(distance) < wallHalfThickness + playerHalfThickness)))
            {
                currentSide = PlayerSide.Crossing;
            }
            else if (clearOfWall)
            {
                currentSide = distance < 0f ? PlayerSide.Original : PlayerSide.Other;
            }

            GeometryUtility.CalculateFrustumPlanes(gameplayCamera, frustumPlanes);
            IsVisible = IntersectsFrustum();
            SetPassable(currentSide != PlayerSide.Original || !IsVisible);
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
            return true;
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
