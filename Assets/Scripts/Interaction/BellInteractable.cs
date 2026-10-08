using UnityEngine;

namespace Metroidbrainia
{
    [DisallowMultipleComponent]
    public sealed class BellInteractable : MonoBehaviour, IHandActionInteractable
    {
        [SerializeField] private Transform bellHinge;
        [SerializeField, Min(0.01f)] private float maxInteractionDistance = 1.8f;
        [SerializeField, Range(0f, 90f)] private float swingAngle = 45f;
        [Tooltip("Oscillations per second.")]
        [SerializeField, Min(0.01f)] private float swingFrequency = 1.2f;
        [Tooltip("Exponential amplitude decay per second.")]
        [SerializeField, Min(0f)] private float swingDamping = 0.4f;
        [SerializeField, Min(0.01f)] private float swingDuration = 6f;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip audioClip;
        [SerializeField] private BellSequencePuzzle puzzle;

        private Quaternion initialRotation;
        private float swingTime;
        private float cosineAmplitude;
        private float sineAmplitude;
        private float currentAngle;
        private float previousAngle;
        private bool swinging;
        private bool initialized;

        private void Reset()
        {
            bellHinge = transform;
            audioSource = GetComponentInChildren<AudioSource>();
        }

        private void Awake()
        {
            if (bellHinge == null)
                bellHinge = transform;
            initialRotation = bellHinge.localRotation;
            initialized = true;
        }

        public bool TryPerformHandAction(ArmPose pose, Camera playerCamera, RaycastHit hit)
        {
            if (!initialized || !isActiveAndEnabled || pose != ArmPose.Point || playerCamera == null
                || hit.collider == null || !ReferenceEquals(hit.collider.GetComponentInParent<IHandActionInteractable>(), this)
                || (playerCamera.transform.position - hit.point).sqrMagnitude
                    > maxInteractionDistance * maxInteractionDistance)
                return false;

            Ring();
            return true;
        }

        public void Ring()
        {
            if (!initialized || !isActiveAndEnabled || bellHinge == null)
                return;

            // Keep the current angle on repeated strikes and reinforce the current travel direction.
            float direction = swinging && currentAngle < previousAngle ? -1f : 1f;
            cosineAmplitude = currentAngle;
            sineAmplitude = direction * Mathf.Sqrt(Mathf.Max(0f, swingAngle * swingAngle
                - currentAngle * currentAngle));
            previousAngle = currentAngle;
            swingTime = 0f;
            swinging = true;

            if (audioSource != null && audioClip != null)
                audioSource.PlayOneShot(audioClip);
            if (puzzle != null)
                puzzle.NotifyBellRung(this);
        }

        private void Update()
        {
            if (!swinging || bellHinge == null)
                return;

            swingTime += Time.deltaTime;
            if (swingTime >= swingDuration)
            {
                RestoreRotation();
                return;
            }

            float phase = swingTime * Mathf.Max(0.01f, swingFrequency) * Mathf.PI * 2f;
            float envelope = Mathf.Exp(-Mathf.Max(0f, swingDamping) * swingTime);
            // Fade to zero with zero slope during the final second, then restore the exact quaternion.
            float fadeDuration = Mathf.Min(1f, swingDuration);
            float fade = Mathf.Clamp01((swingDuration - swingTime) / fadeDuration);
            envelope *= Mathf.SmoothStep(0f, 1f, fade);
            previousAngle = currentAngle;
            currentAngle = (cosineAmplitude * Mathf.Cos(phase) + sineAmplitude * Mathf.Sin(phase)) * envelope;
            bellHinge.localRotation = initialRotation * Quaternion.AngleAxis(currentAngle, Vector3.right);
        }

        private void RestoreRotation()
        {
            swinging = false;
            currentAngle = 0f;
            previousAngle = 0f;
            if (initialized && bellHinge != null)
                bellHinge.localRotation = initialRotation;
        }

        private void OnDisable()
        {
            RestoreRotation();
        }
    }
}
