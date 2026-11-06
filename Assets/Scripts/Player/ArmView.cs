using UnityEngine;
using UnityEngine.Serialization;

namespace Metroidbrainia
{
    public enum HandInteractionVisual
    {
        Grab,
        Hold
    }

    // Follow after world objects and the player camera have completed their updates.
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class ArmView : MonoBehaviour
    {
        [SerializeField] private RectTransform viewModel;
        [SerializeField] private RectTransform armRoot;
        [SerializeField] private RectTransform armBody;
        [SerializeField] private RectTransform handAnchor;
        [SerializeField] private RectTransform hand;
        [SerializeField] private RectTransform handPoint;
        [FormerlySerializedAs("armAnimator")]
        [SerializeField] private Animator handAnimator;
        [SerializeField, Min(0.01f)] private float minimumDistance = 10f;
        [Tooltip("Extra length behind the shoulder, in ArmRoot local units.")]
        [SerializeField, Min(0f)] private float shoulderOverscan = 80f;
        [SerializeField, Min(0.01f)] private float returnSmoothTime = 0.18f;
        [Tooltip("Contact point margin inside ViewModel, in its local UI units.")]
        [SerializeField, Min(0f)] private float screenPadding = 20f;
        [Header("Animator state paths (layer 0)")]
        [SerializeField] private string restState = "Base Layer.Arm_Rest";
        [SerializeField] private string pointState = "Base Layer.Arm_Point";
        [SerializeField] private string okState = "Base Layer.Arm_OK";
        [SerializeField] private string grabState = "Base Layer.Arm_Grab";
        [SerializeField] private string holdState = "Base Layer.Arm_Hold";
        private bool missingInteractionVisualReported;

        private Vector3 basePosition;
        private Quaternion baseRotation;
        private Vector3 baseBodyPosition;
        private Vector2 baseBodySize;
        private Vector3 baseAnchorPosition;
        private Vector2 localAxis;
        private Vector2 extensionVector;
        private Vector2 contactOffset;
        private Vector2 baseHandPosition;
        private Vector2 handPosition;
        private Vector2 returnVelocity;
        private float baseLength;
        private float baseBodyHeight;
        private bool initialized;
        private bool controlling;
        private bool returning;
        private Transform followedPoint;
        private Camera followCamera;
        private const float ReturnTolerance = 0.1f;

        public float InitialHandAngle { get; private set; }

        public bool Initialize()
        {
            if (initialized)
                return true;

            Canvas canvas = GetComponentInParent<Canvas>();
            if (viewModel == null || armRoot == null || armBody == null || handAnchor == null
                || hand == null || handPoint == null || armRoot.parent != viewModel
                || armBody.parent != armRoot || handAnchor.parent != armRoot
                || hand.parent != handAnchor || handPoint.parent != hand
                || canvas == null || canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                Debug.LogError("Assign ViewModel > ArmRoot > (ArmBody, HandAnchor > Hand > HandPoint) on an Overlay Canvas.", this);
                return false;
            }

            if (handAnimator == null || handAnimator.gameObject != hand.gameObject
                || handAnimator.runtimeAnimatorController == null)
            {
                Debug.LogError("Assign Hand's Animator with the four existing arm pose states.", this);
                return false;
            }

            handAnimator.enabled = true;
            foreach (ArmPose pose in System.Enum.GetValues(typeof(ArmPose)))
            {
                string state = GetStatePath(pose);
                if (string.IsNullOrEmpty(state) || !handAnimator.HasState(0, Animator.StringToHash(state)))
                {
                    Debug.LogError($"Hand Animator is missing the layer 0 state '{state}' for {pose}.", this);
                    return false;
                }
            }

            Canvas.ForceUpdateCanvases();
            Vector3 initialHandRight = viewModel.InverseTransformVector(hand.TransformVector(Vector3.right));
            InitialHandAngle = Mathf.Atan2(initialHandRight.y, initialHandRight.x) * Mathf.Rad2Deg;
            basePosition = armRoot.anchoredPosition3D;
            baseRotation = armRoot.localRotation;
            baseBodyPosition = armBody.anchoredPosition3D;
            baseBodySize = armBody.sizeDelta;
            baseBodyHeight = armBody.rect.height;
            baseAnchorPosition = handAnchor.anchoredPosition3D;
            localAxis = armBody.localRotation * Vector3.up;
            baseLength = baseBodyHeight * armBody.localScale.y;
            extensionVector = viewModel.InverseTransformVector(armRoot.TransformVector(localAxis));
            baseHandPosition = viewModel.InverseTransformPoint(handPoint.position);
            contactOffset = baseHandPosition - (Vector2)armRoot.localPosition - extensionVector * baseLength;
            if (baseLength <= 0f || extensionVector.sqrMagnitude < 0.000001f)
            {
                Debug.LogError("ArmBody needs a positive height and Y scale, with its local Y axis in the UI plane.", this);
                return false;
            }

            initialized = true;
            RestoreBaseTransform();
            return true;
        }

        public void BeginControl()
        {
            if (!initialized)
                return;

            // Retain the current target when interrupting a return.
            returning = false;
            returnVelocity = Vector2.zero;
            controlling = true;
        }

        public bool FollowWorldPoint(Transform point, Camera worldCamera)
        {
            if (!initialized || point == null || worldCamera == null)
                return false;

            BeginControl();
            followedPoint = point;
            followCamera = worldCamera;
            ApplyFollowedPoint();
            return true;
        }

        public void EndWorldFollow()
        {
            followedPoint = null;
            followCamera = null;
            EndControl();
        }

        public Vector2 GetScreenDisplacement(Vector2 uiDisplacement)
        {
            Vector2 origin = RectTransformUtility.WorldToScreenPoint(null, viewModel.TransformPoint(Vector3.zero));
            Vector2 end = RectTransformUtility.WorldToScreenPoint(null, viewModel.TransformPoint(uiDisplacement));
            return end - origin;
        }

        private void ApplyFollowedPoint()
        {
            Vector3 screenPoint = followCamera.WorldToScreenPoint(followedPoint.position);
            if (screenPoint.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                viewModel, screenPoint, null, out Vector2 localPoint))
            {
                handPosition = localPoint;
                // Clamping a captured contact would detach it from the world point.
                ApplyHandPosition(false);
            }
        }

        public void EndControl()
        {
            if (!initialized || !controlling)
                return;

            followedPoint = null;
            followCamera = null;
            controlling = false;
            returning = true;
            returnVelocity = Vector2.zero;
        }

        public void MoveHand(Vector2 displacement)
        {
            if (!controlling || followedPoint != null)
                return;

            handPosition += displacement;
            ApplyHandPosition(true);
        }

        private void LateUpdate()
        {
            // SmoothDamp must not update its return velocity with a zero time step during pause.
            if (!initialized || Time.deltaTime <= 0f)
                return;

            if (followedPoint != null && followCamera != null)
                ApplyFollowedPoint();
            else if (controlling)
                ApplyHandPosition(true);
            else if (returning)
            {
                handPosition = Vector2.SmoothDamp(handPosition, baseHandPosition, ref returnVelocity,
                    Mathf.Max(0.01f, returnSmoothTime), Mathf.Infinity, Time.deltaTime);
                // The Inspector rest pose may lie outside the interactive screen bounds.
                ApplyHandPosition(false);
                if ((handPosition - baseHandPosition).sqrMagnitude <= ReturnTolerance * ReturnTolerance)
                    RestoreBaseTransform();
            }
        }

        private void ApplyHandPosition(bool clampToScreen)
        {
            armRoot.anchoredPosition3D = basePosition;
            Vector2 shoulder = armRoot.localPosition;
            Rect bounds = viewModel.rect;
            float paddingX = Mathf.Min(Mathf.Max(0f, screenPadding), bounds.width * 0.5f);
            float paddingY = Mathf.Min(Mathf.Max(0f, screenPadding), bounds.height * 0.5f);
            bounds = Rect.MinMaxRect(bounds.xMin + paddingX, bounds.yMin + paddingY,
                bounds.xMax - paddingX, bounds.yMax - paddingY);
            if (clampToScreen)
                handPosition = ClampToRect(handPosition, bounds);

            Vector2 axis = extensionVector.normalized;
            Vector2 perpendicular = new Vector2(-axis.y, axis.x);
            float parallelOffset = Vector2.Dot(contactOffset, axis);
            float sidewaysOffset = Vector2.Dot(contactOffset, perpendicular);
            float axisScale = extensionVector.magnitude;
            // A fixed sideways contact offset creates a small unreachable circle near the shoulder.
            float minimumLength = Mathf.Max(0f, -parallelOffset / axisScale);
            float minimumReach = Mathf.Max(minimumDistance,
                (contactOffset + extensionVector * minimumLength).magnitude);
            Vector2 direction = handPosition - shoulder;
            if (clampToScreen && direction.magnitude < minimumReach)
            {
                Vector2 outward = direction.sqrMagnitude > 0.000001f
                    ? direction.normalized : (baseHandPosition - shoulder).normalized;
                handPosition = ClampToRect(shoulder + outward * minimumReach, bounds);
                if ((handPosition - shoulder).magnitude < minimumReach - 0.0001f)
                {
                    Vector2 corner = new Vector2(shoulder.x < bounds.center.x ? bounds.xMax : bounds.xMin,
                        shoulder.y < bounds.center.y ? bounds.yMax : bounds.yMin);
                    handPosition = ClampToRect(Vector2.MoveTowards(shoulder, corner, minimumReach), bounds);
                }
                direction = handPosition - shoulder;
            }

            float longitudinalReach = Mathf.Sqrt(Mathf.Max(0f,
                direction.sqrMagnitude - sidewaysOffset * sidewaysOffset));
            float length = Mathf.Max(0f, (longitudinalReach - parallelOffset) / axisScale);
            Vector2 unrotatedContact = contactOffset + extensionVector * length;
            float angle = (Mathf.Atan2(direction.y, direction.x)
                - Mathf.Atan2(unrotatedContact.y, unrotatedContact.x)) * Mathf.Rad2Deg;
            armRoot.localRotation = Quaternion.AngleAxis(angle, Vector3.forward) * baseRotation;
            ApplyLength(length);
        }

        private void ApplyLength(float length)
        {
            float overscan = Mathf.Max(0f, shoulderOverscan);
            float height = (length + overscan) / armBody.localScale.y;
            // Offset the pivot so the added length grows behind the shoulder, even for a nonzero pivot.
            armBody.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            armBody.anchoredPosition3D = baseBodyPosition + (Vector3)localAxis
                * (armBody.pivot.y * (height - baseBodyHeight) * armBody.localScale.y - overscan);
            handAnchor.anchoredPosition3D = baseAnchorPosition + (Vector3)localAxis * (length - baseLength);
        }

        private static Vector2 ClampToRect(Vector2 point, Rect bounds)
        {
            return new Vector2(Mathf.Clamp(point.x, bounds.xMin, bounds.xMax),
                Mathf.Clamp(point.y, bounds.yMin, bounds.yMax));
        }

        public void PlayPose(ArmPose pose)
        {
            if (!initialized)
                return;

            string state = GetStatePath(pose);
            if (string.IsNullOrEmpty(state))
                return;

            // Explicit zero restarts even the state that is already active.
            handAnimator.Play(Animator.StringToHash(state), 0, 0f);
        }

        public bool TryPlayInteractionVisual(HandInteractionVisual visual)
        {
            string state = visual == HandInteractionVisual.Hold ? holdState : grabState;
            if (!initialized || handAnimator == null || string.IsNullOrEmpty(state)
                || !handAnimator.HasState(0, Animator.StringToHash(state)))
            {
                if (!missingInteractionVisualReported)
                {
                    Debug.LogError($"Hand Animator is missing the interaction state '{state}'.", this);
                    missingInteractionVisualReported = true;
                }
                return false;
            }

            handAnimator.Play(Animator.StringToHash(state), 0, 0f);
            return true;
        }

        private string GetStatePath(ArmPose pose)
        {
            switch (pose)
            {
                case ArmPose.Rest: return restState;
                case ArmPose.Point: return pointState;
                case ArmPose.OK: return okState;
                case ArmPose.Grab: return grabState;
                default: return null;
            }
        }

        public void RestoreBaseTransform()
        {
            if (!initialized)
                return;

            controlling = false;
            returning = false;
            followedPoint = null;
            followCamera = null;
            returnVelocity = Vector2.zero;
            handPosition = baseHandPosition;
            armRoot.anchoredPosition3D = basePosition;
            armRoot.localRotation = baseRotation;
            armBody.anchoredPosition3D = baseBodyPosition;
            armBody.sizeDelta = baseBodySize;
            handAnchor.anchoredPosition3D = baseAnchorPosition;
        }

        public Vector2 GetHandScreenPosition()
        {
            return RectTransformUtility.WorldToScreenPoint(null, handPoint.position);
        }

        private void OnDisable()
        {
            RestoreBaseTransform();
        }
    }
}
