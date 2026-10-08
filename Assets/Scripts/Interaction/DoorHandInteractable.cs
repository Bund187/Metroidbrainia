using UnityEngine;

namespace Metroidbrainia
{
    public enum ScriptedDoorOpenDirection
    {
        Negative = -1,
        Positive = 1
    }

    [DisallowMultipleComponent]
    public sealed class DoorHandInteractable : MonoBehaviour, IHandInteractable
    {
        [SerializeField] private Transform doorHinge;
        [SerializeField] private Transform knob;
        [SerializeField] private Transform interactionPoint;
        [SerializeField] private bool canOpenByHand = true;
        [SerializeField, Min(0.01f)] private float maxInteractionDistance = 1.8f;
        [SerializeField, Range(0.1f, 179f)] private float manualOpenThreshold = 10f;
        [SerializeField, Range(0.1f, 179f)] private float openAngle = 125f;
        [SerializeField] private ScriptedDoorOpenDirection scriptedOpenDirection = ScriptedDoorOpenDirection.Positive;
        [SerializeField, Min(0.01f)] private float autoOpenSmoothTime = 0.3f;
        [SerializeField, Min(0.01f)] private float lockedShakeDuration = 0.18f;
        [Tooltip("Knob shake amplitude in degrees.")]
        [SerializeField, Min(0f)] private float lockedShakeAmount = 3f;

        private Collider knobCollider;
        private Quaternion closedRotation;
        private Vector3 pointInHinge;
        private Vector3 baseKnobPosition;
        private Quaternion baseKnobRotation;
        private float currentAngle;
        private float openDirection;
        private float angleVelocity;
        private float shakeTime;
        private bool captured;
        private bool autoOpening;
        private bool closing;
        private bool shaking;
        private bool lockedFeedbackShown;
        private bool lockedInteraction;
        private bool initialized;
        private const float AngleTolerance = 0.1f;
        private const float DirectionSampleAngle = 5f;

        public Transform InteractionPoint => interactionPoint;
        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (doorHinge == null || knob == null || interactionPoint == null
                || !knob.IsChildOf(doorHinge) || !interactionPoint.IsChildOf(knob))
            {
                Debug.LogError("Assign DoorHinge, its Knob and Knob's InteractionPoint.", this);
                enabled = false;
                return;
            }

            knobCollider = knob.GetComponent<Collider>();
            if (knobCollider == null || !knobCollider.isTrigger)
            {
                Debug.LogError("Knob needs an enabled trigger Collider.", this);
                enabled = false;
                return;
            }

            closedRotation = doorHinge.localRotation;
            pointInHinge = doorHinge.InverseTransformPoint(interactionPoint.position);
            baseKnobPosition = knob.localPosition;
            baseKnobRotation = knob.localRotation;
            initialized = true;
        }

        public bool TryBeginInteraction(Camera playerCamera, Collider hitCollider)
        {
            if (!initialized || !isActiveAndEnabled || playerCamera == null || hitCollider != knobCollider
                || !knobCollider.enabled || IsOpen || captured || autoOpening || closing
                || !IsCloseEnough(playerCamera))
                return false;

            if (!canOpenByHand)
            {
                if (lockedFeedbackShown)
                    return false;

                lockedFeedbackShown = true;
                lockedInteraction = true;
                captured = true;
                shakeTime = 0f;
                shaking = true;
                return true;
            }

            RestoreKnob();
            Vector3 playerPosition = playerCamera.transform.position;
            float positiveDistance = (GetPointAtAngle(DirectionSampleAngle) - playerPosition).sqrMagnitude;
            float negativeDistance = (GetPointAtAngle(-DirectionSampleAngle) - playerPosition).sqrMagnitude;
            openDirection = positiveDistance >= negativeDistance ? 1f : -1f;
            angleVelocity = 0f;
            captured = true;
            return true;
        }

        public bool UpdateInteraction(Camera playerCamera, Vector2 screenDisplacement)
        {
            if (!initialized || !isActiveAndEnabled || !captured || IsOpen
                || doorHinge == null || interactionPoint == null || knobCollider == null
                || !knobCollider.enabled || !knob.gameObject.activeInHierarchy)
                return false;
            if (lockedInteraction)
                return shaking && playerCamera != null && IsCloseEnough(playerCamera);
            if (autoOpening)
                return true;
            if (!canOpenByHand || playerCamera == null || !IsCloseEnough(playerCamera))
                return false;

            Vector3 screenPoint = playerCamera.WorldToScreenPoint(interactionPoint.position);
            Vector3 nextScreenPoint = playerCamera.WorldToScreenPoint(
                GetPointAtAngle(currentAngle + openDirection * DirectionSampleAngle));
            if (screenPoint.z <= 0f || nextScreenPoint.z <= 0f)
                return false;

            Vector2 screenTangent = ((Vector2)nextScreenPoint - (Vector2)screenPoint) / DirectionSampleAngle;
            if (screenTangent.sqrMagnitude > 0.0001f)
            {
                float degrees = Vector2.Dot(screenDisplacement, screenTangent.normalized)
                    / Mathf.Max(1f, screenTangent.magnitude);
                float threshold = Mathf.Min(manualOpenThreshold, openAngle);
                // A large mouse delta still hands off at the threshold instead of skipping auto-opening.
                float progress = Mathf.Clamp(currentAngle * openDirection + degrees, 0f, threshold);
                currentAngle = progress * openDirection;
                ApplyAngle();
                if (progress >= threshold)
                {
                    autoOpening = true;
                    angleVelocity = 0f;
                }
            }
            return true;
        }

        public void EndInteraction()
        {
            if (!captured)
            {
                lockedFeedbackShown = false;
                return;
            }

            captured = false;
            if (lockedInteraction)
            {
                lockedInteraction = false;
                RestoreKnob();
                return;
            }
            if (!autoOpening && !IsOpen)
            {
                closing = true;
                angleVelocity = 0f;
            }
        }

        public void Unlock()
        {
            canOpenByHand = true;
        }

        public void OpenAutomatically()
        {
            if (!initialized || !isActiveAndEnabled || IsOpen || autoOpening)
                return;

            Unlock();
            RestoreKnob();
            lockedInteraction = false;
            closing = false;
            openDirection = scriptedOpenDirection == ScriptedDoorOpenDirection.Negative ? -1f : 1f;
            angleVelocity = 0f;
            autoOpening = true;
        }

        private bool IsCloseEnough(Camera playerCamera)
        {
            return (playerCamera.transform.position - interactionPoint.position).sqrMagnitude
                <= maxInteractionDistance * maxInteractionDistance;
        }

        private Vector3 GetPointAtAngle(float angle)
        {
            Quaternion rotation = closedRotation * Quaternion.AngleAxis(angle, Vector3.up);
            Vector3 point = doorHinge.localPosition + rotation * Vector3.Scale(pointInHinge, doorHinge.localScale);
            return doorHinge.parent != null ? doorHinge.parent.TransformPoint(point) : point;
        }

        private void ApplyAngle()
        {
            doorHinge.localRotation = closedRotation * Quaternion.AngleAxis(currentAngle, Vector3.up);
        }

        private void Update()
        {
            if (!initialized || doorHinge == null || knob == null)
                return;

            if (autoOpening || closing)
            {
                float target = autoOpening ? openDirection * openAngle : 0f;
                currentAngle = Mathf.SmoothDampAngle(currentAngle, target, ref angleVelocity,
                    Mathf.Max(0.01f, autoOpenSmoothTime));
                if (Mathf.Abs(Mathf.DeltaAngle(currentAngle, target)) <= AngleTolerance)
                {
                    currentAngle = target;
                    IsOpen = autoOpening;
                    autoOpening = false;
                    closing = false;
                    angleVelocity = 0f;
                }
                ApplyAngle();
            }

            if (shaking)
            {
                shakeTime += Time.deltaTime;
                float progress = shakeTime / Mathf.Max(0.01f, lockedShakeDuration);
                if (progress >= 1f)
                    RestoreKnob();
                else
                    knob.localRotation = baseKnobRotation * Quaternion.AngleAxis(
                        Mathf.Sin(progress * Mathf.PI * 8f) * lockedShakeAmount * (1f - progress), Vector3.forward);
            }
        }

        private void RestoreKnob()
        {
            shaking = false;
            if (knob != null)
            {
                knob.localPosition = baseKnobPosition;
                knob.localRotation = baseKnobRotation;
            }
        }

        private void OnDisable()
        {
            if (!initialized)
                return;

            EndInteraction();
            RestoreKnob();
            if (closing && doorHinge != null)
            {
                currentAngle = 0f;
                closing = false;
                ApplyAngle();
            }
        }
    }
}
