using UnityEngine;

namespace Metroidbrainia
{
    // Observe after the first-person camera has followed its target for this frame.
    [DefaultExecutionOrder(300)]
    [DisallowMultipleComponent]
    public sealed class MapObservationDiscovery : MonoBehaviour
    {
        [SerializeField] private MapJournalState journalState;
        [SerializeField] private MapDiscoveryDefinition discovery;
        [SerializeField] private Transform viewPoint;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private bool observationEnabled = true;
        [SerializeField, Min(0f)] private float maxDistance = 8f;
        [SerializeField, Min(0f)] private float requiredViewTime = 0.3f;
        [SerializeField] private Vector2 viewportMin = new Vector2(0.05f, 0.05f);
        [SerializeField] private Vector2 viewportMax = new Vector2(0.95f, 0.95f);
        [SerializeField] private LayerMask occlusionMask = Physics.DefaultRaycastLayers;

        private const float SurfaceTolerance = 0.03f;
        private float visibleTime;
        private bool discoveryComplete;

        private void Awake()
        {
            if (journalState == null || discovery == null || viewPoint == null || gameplayCamera == null)
            {
                Debug.LogError("Assign the journal, discovery, authored ViewPoint and gameplay camera for observation.", this);
                enabled = false;
            }
        }

        public void SetObservationEnabled(bool allowObservation)
        {
            observationEnabled = allowObservation;
            if (!allowObservation)
                visibleTime = 0f;
        }

        public void EnableObservation() => SetObservationEnabled(true);
        public void DisableObservation() => SetObservationEnabled(false);

        private void LateUpdate()
        {
            if (discoveryComplete)
                return;
            if (journalState == null || discovery == null)
            {
                visibleTime = 0f;
                return;
            }
            // Check here, after startup restore, rather than caching Undiscovered in Awake.
            if (journalState.GetDiscoveryState(discovery) != MapEntryState.Undiscovered)
            {
                discoveryComplete = true;
                visibleTime = 0f;
                return;
            }
            if (!observationEnabled || viewPoint == null || gameplayCamera == null || !gameplayCamera.isActiveAndEnabled)
            {
                visibleTime = 0f;
                return;
            }
            if (Time.deltaTime <= 0f)
                return;
            if (!IsViewPointVisible())
            {
                visibleTime = 0f;
                return;
            }

            visibleTime += Time.deltaTime;
            if (visibleTime >= requiredViewTime)
            {
                journalState.DiscoverDiscovery(discovery);
                discoveryComplete = journalState.GetDiscoveryState(discovery) != MapEntryState.Undiscovered;
                visibleTime = 0f;
            }
        }

        private bool IsViewPointVisible()
        {
            Vector3 origin = gameplayCamera.transform.position;
            Vector3 offset = viewPoint.position - origin;
            if (offset.sqrMagnitude > maxDistance * maxDistance)
                return false;
            Vector3 viewport = gameplayCamera.WorldToViewportPoint(viewPoint.position);
            if (viewport.z <= 0f || viewport.x < viewportMin.x || viewport.x > viewportMax.x
                || viewport.y < viewportMin.y || viewport.y > viewportMax.y)
                return false;

            float distance = offset.magnitude;
            float rayDistance = Mathf.Max(0f, distance - SurfaceTolerance);
            // Ignore the final 3 cm so the observed surface itself does not hide its ViewPoint.
            return rayDistance <= 0f || !Physics.Raycast(origin, offset / distance, rayDistance,
                occlusionMask, QueryTriggerInteraction.Ignore);
        }

        private void OnDisable() => visibleTime = 0f;

        private void OnValidate()
        {
            maxDistance = Mathf.Max(0f, maxDistance);
            requiredViewTime = Mathf.Max(0f, requiredViewTime);
            viewportMin = new Vector2(Mathf.Clamp01(viewportMin.x), Mathf.Clamp01(viewportMin.y));
            viewportMax = new Vector2(Mathf.Clamp01(viewportMax.x), Mathf.Clamp01(viewportMax.y));
            viewportMax = new Vector2(Mathf.Max(viewportMin.x, viewportMax.x), Mathf.Max(viewportMin.y, viewportMax.y));
        }
    }
}
