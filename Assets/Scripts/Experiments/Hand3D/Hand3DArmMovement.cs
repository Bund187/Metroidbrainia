using UnityEngine;

namespace Metroidbrainia.Hand3DExperiment
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class Hand3DArmMovement : MonoBehaviour
    {
        [SerializeField] private Camera armCamera;
        [SerializeField] private Transform upperArm;
        [SerializeField] private Transform forearm;
        [SerializeField] private Transform hand;
        [SerializeField] private Transform elbowHint;
        [Tooltip("Stick speed in reference-screen units per second. Mouse delta already measures displacement.")]
        [SerializeField, Min(0f)] private float movementSpeed = 900f;
        [SerializeField, Min(0f)] private float movementSmoothing = 0.08f;
        [Tooltip("Input reference size, independent of the pixelated RenderTexture resolution.")]
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
        [Tooltip("Normalized viewport bounds for the wrist. Leave room for the fingers.")]
        [SerializeField] private Vector2 horizontalScreenLimits = new Vector2(0.06f, 0.94f);
        [SerializeField] private Vector2 verticalScreenLimits = new Vector2(0.06f, 0.94f);
        [SerializeField, Min(0.01f)] private float returnSmoothTime = 0.18f;

        private Quaternion upperRotation;
        private Quaternion forearmRotation;
        private Quaternion handRotation;
        private Quaternion restHandRotationInCamera;
        private Vector3 forearmPosition;
        private Vector3 handPosition;
        private Vector3 bendDirectionInCamera;
        private Vector2 restScreenPosition;
        private Vector2 targetScreenPosition;
        private Vector2 screenPosition;
        private Vector2 returnVelocity;
        private float handDepth;
        private float upperLength;
        private float forearmLength;
        private bool initialized;
        private bool controlling;
        private bool returning;

        public bool IsReady => initialized;
        public Transform Hand => hand;
        public bool ControlsRotationOf(Transform bone) => bone == upperArm || bone == forearm;

        // The rig composes this with its wrist pose and remains the sole writer of wrist/finger rotations.
        public Quaternion HandRotationCompensation => initialized && isActiveAndEnabled
            ? Quaternion.Inverse(hand.parent.rotation) * armCamera.transform.rotation * restHandRotationInCamera * Quaternion.Inverse(handRotation)
            : Quaternion.identity;

        private void OnEnable()
        {
            if (Application.isPlaying && !Initialize()) enabled = false;
        }

        public bool Initialize()
        {
            if (initialized) return true;
            if (!ExperimentScope.IsAllowed(this) || armCamera == null || upperArm == null || forearm == null || hand == null
                || upperArm == forearm || forearm == hand || !forearm.IsChildOf(upperArm) || !hand.IsChildOf(forearm)
                || !upperArm.IsChildOf(transform) || upperArm.gameObject.scene != gameObject.scene
                || hand.gameObject.scene != gameObject.scene || armCamera.gameObject.scene != gameObject.scene)
            {
                Debug.LogError("In TestArmScene assign the pixel arm camera and UpperArm > Forearm > Hand scene chain.", this);
                return false;
            }
            Vector3 viewport = armCamera.WorldToViewportPoint(hand.position);
            upperLength = Vector3.Distance(upperArm.position, forearm.position);
            forearmLength = Vector3.Distance(forearm.position, hand.position);
            if (viewport.z <= armCamera.nearClipPlane || viewport.z >= armCamera.farClipPlane
                || upperLength < 0.0001f || forearmLength < 0.0001f)
            {
                Debug.LogError("The hand must be inside camera depth bounds, with two nonzero arm segments.", this);
                return false;
            }
            upperRotation = upperArm.localRotation;
            forearmRotation = forearm.localRotation;
            handRotation = hand.localRotation;
            forearmPosition = forearm.localPosition;
            handPosition = hand.localPosition;
            restHandRotationInCamera = Quaternion.Inverse(armCamera.transform.rotation) * hand.rotation;
            handDepth = viewport.z;
            restScreenPosition = new Vector2(viewport.x, viewport.y);
            screenPosition = targetScreenPosition = restScreenPosition;
            Vector3 direction = (hand.position - upperArm.position).normalized;
            Vector3 bend = forearm.position - upperArm.position;
            bend -= direction * Vector3.Dot(bend, direction);
            if (bend.sqrMagnitude < 0.000001f) bend = armCamera.transform.right;
            bendDirectionInCamera = armCamera.transform.InverseTransformDirection(bend.normalized);
            initialized = true;
            return true;
        }

        public void BeginControl()
        {
            if (!initialized) return;
            controlling = true;
            returning = false;
            targetScreenPosition = screenPosition;
            returnVelocity = Vector2.zero;
        }

        public void EndControl()
        {
            if (!initialized || !controlling) return;
            controlling = false;
            returning = true;
            targetScreenPosition = restScreenPosition;
            returnVelocity = Vector2.zero;
        }

        // Stick input arrives integrated with deltaTime; relative mouse delta must not be integrated again.
        public void MoveHand(Vector2 displacement, bool usingMouse = false)
        {
            if (!initialized || !controlling || displacement.sqrMagnitude == 0f) return;
            Vector2 pixels = displacement * (usingMouse ? 1f : movementSpeed);
            targetScreenPosition = ClampToScreen(targetScreenPosition + new Vector2(
                pixels.x / referenceResolution.x, pixels.y / referenceResolution.y));
        }

        private Vector2 ClampToScreen(Vector2 position) => new Vector2(
            Mathf.Clamp(position.x, horizontalScreenLimits.x, horizontalScreenLimits.y),
            Mathf.Clamp(position.y, verticalScreenLimits.x, verticalScreenLimits.y));

        private void LateUpdate()
        {
            if (!initialized || Time.deltaTime <= 0f || !controlling && !returning) return;
            if (returning)
            {
                screenPosition = Vector2.SmoothDamp(screenPosition, restScreenPosition, ref returnVelocity,
                    returnSmoothTime, Mathf.Infinity, Time.deltaTime);
                // The authored rest pose can be outside the interactive screen limits, just like ArmView.
                if (Vector2.Scale(screenPosition - restScreenPosition, referenceResolution).sqrMagnitude < 0.01f)
                {
                    screenPosition = targetScreenPosition = restScreenPosition;
                    returning = false;
                    RestoreRestPose();
                    return;
                }
            }
            else
            {
                float weight = movementSmoothing > 0f ? 1f - Mathf.Exp(-Time.deltaTime / movementSmoothing) : 1f;
                screenPosition = Vector2.Lerp(screenPosition, targetScreenPosition, weight);
            }
            RestoreRestPose();
            Vector3 target = armCamera.ViewportToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, handDepth));
            bool limited = SolveArm(target);
            if (limited && controlling)
            {
                // Do not accumulate an invisible target beyond the physical reach of the skeleton.
                Vector3 reached = armCamera.WorldToViewportPoint(hand.position);
                screenPosition = targetScreenPosition = new Vector2(reached.x, reached.y);
            }
        }

        private bool SolveArm(Vector3 target)
        {
            Vector3 shoulder = upperArm.position;
            Vector3 toTarget = target - shoulder;
            float rawDistance = toTarget.magnitude;
            Vector3 direction = rawDistance > 0.000001f ? toTarget / rawDistance : armCamera.transform.forward;
            float minimumReach = Mathf.Abs(upperLength - forearmLength) + 0.000001f;
            float maximumReach = upperLength + forearmLength - 0.000001f;
            float distance = Mathf.Clamp(rawDistance, minimumReach, maximumReach);
            target = shoulder + direction * distance;
            Vector3 pole = elbowHint != null ? elbowHint.position - shoulder
                : armCamera.transform.TransformDirection(bendDirectionInCamera);
            pole -= direction * Vector3.Dot(pole, direction);
            if (pole.sqrMagnitude < 0.000001f) pole = Vector3.Cross(direction, armCamera.transform.forward);
            if (pole.sqrMagnitude < 0.000001f) pole = Vector3.Cross(direction, armCamera.transform.up);
            pole.Normalize();
            float along = Mathf.Clamp((upperLength * upperLength + distance * distance - forearmLength * forearmLength)
                / (2f * distance), -upperLength, upperLength);
            Vector3 elbow = shoulder + direction * along + pole * Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
            // Only rotate the joints. Segment offsets and scales always keep their authored values.
            upperArm.rotation = Quaternion.FromToRotation(forearm.position - shoulder, elbow - shoulder) * upperArm.rotation;
            forearm.rotation = Quaternion.FromToRotation(hand.position - forearm.position, target - forearm.position) * forearm.rotation;
            return Mathf.Abs(distance - rawDistance) > 0.00001f;
        }

        private void RestoreRestPose()
        {
            upperArm.localRotation = upperRotation;
            forearm.localRotation = forearmRotation;
            forearm.localPosition = forearmPosition;
            hand.localPosition = handPosition;
        }

        private void OnDisable()
        {
            if (initialized && upperArm != null && forearm != null && hand != null) RestoreRestPose();
            initialized = controlling = returning = false;
        }

        private void OnValidate()
        {
            referenceResolution = new Vector2(Mathf.Max(1f, referenceResolution.x), Mathf.Max(1f, referenceResolution.y));
            horizontalScreenLimits = new Vector2(Mathf.Clamp01(horizontalScreenLimits.x), Mathf.Clamp01(horizontalScreenLimits.y));
            verticalScreenLimits = new Vector2(Mathf.Clamp01(verticalScreenLimits.x), Mathf.Clamp01(verticalScreenLimits.y));
            horizontalScreenLimits.y = Mathf.Max(horizontalScreenLimits.x, horizontalScreenLimits.y);
            verticalScreenLimits.y = Mathf.Max(verticalScreenLimits.x, verticalScreenLimits.y);
        }
    }
}
