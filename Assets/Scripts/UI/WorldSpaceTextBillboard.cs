using UnityEngine;

namespace Metroidbrainia
{
    [DefaultExecutionOrder(300)]
    [DisallowMultipleComponent]
    public sealed class WorldSpaceTextBillboard : MonoBehaviour
    {
        [SerializeField] private Camera gameplayCamera;
        [Tooltip("UI text faces local -Z. Use 180 only if your canvas faces the opposite way.")]
        [SerializeField] private float yawOffset;
        [SerializeField] private bool useDistanceScaling;
        [SerializeField, Min(0f)] private float nearDistance = 1f;
        [SerializeField, Min(0.001f)] private float farDistance = 8f;
        [SerializeField, Min(0.001f)] private float minimumScaleMultiplier = 0.85f;
        [SerializeField, Min(0.001f)] private float maximumScaleMultiplier = 1.1f;
        [SerializeField, Min(0.001f)] private float scaleSmoothTime = 0.25f;

        private Vector3 baseScale;
        private Quaternion initialRotation;
        private float scaleMultiplier = 1f;
        private float scaleVelocity;

        private void OnEnable()
        {
            baseScale = transform.localScale;
            initialRotation = transform.localRotation;
            scaleMultiplier = 1f;
            scaleVelocity = 0f;
            if (gameplayCamera == null)
                Debug.LogWarning("Assign the gameplay camera for the world-space text billboard.", this);
        }

        private void LateUpdate()
        {
            if (gameplayCamera == null)
                return;
            // A world-space UI canvas is read from its -Z side. Point its +Z away from the viewer.
            Vector3 awayFromCamera = transform.position - gameplayCamera.transform.position;
            Vector3 horizontal = new Vector3(awayFromCamera.x, 0f, awayFromCamera.z);
            if (horizontal.sqrMagnitude > 0.000001f)
                transform.rotation = Quaternion.LookRotation(horizontal, Vector3.up) * Quaternion.Euler(0f, yawOffset, 0f);
            if (!useDistanceScaling && Mathf.Approximately(scaleMultiplier, 1f))
                return;
            float distanceT = Mathf.InverseLerp(nearDistance, farDistance, awayFromCamera.magnitude);
            float target = useDistanceScaling ? Mathf.Lerp(maximumScaleMultiplier, minimumScaleMultiplier, distanceT) : 1f;
            if (Time.deltaTime > 0f)
                scaleMultiplier = Mathf.SmoothDamp(scaleMultiplier, target, ref scaleVelocity, scaleSmoothTime);
            float min = Mathf.Min(1f, minimumScaleMultiplier);
            float max = Mathf.Max(1f, maximumScaleMultiplier);
            scaleMultiplier = Mathf.Clamp(scaleMultiplier, min, max);
            transform.localScale = baseScale * scaleMultiplier;
        }

        private void OnDisable()
        {
            transform.localScale = baseScale;
            transform.localRotation = initialRotation;
        }

        private void OnValidate()
        {
            nearDistance = Mathf.Max(0f, nearDistance);
            farDistance = Mathf.Max(nearDistance + 0.001f, farDistance);
            minimumScaleMultiplier = Mathf.Max(0.001f, minimumScaleMultiplier);
            maximumScaleMultiplier = Mathf.Max(minimumScaleMultiplier, maximumScaleMultiplier);
            scaleSmoothTime = Mathf.Max(0.001f, scaleSmoothTime);
        }
    }
}
