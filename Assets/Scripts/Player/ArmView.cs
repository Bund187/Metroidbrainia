using UnityEngine;
using UnityEngine.UI;

namespace Metroidbrainia
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(Image))]
    public sealed class ArmView : MonoBehaviour
    {
        [SerializeField] private RectTransform viewModel;
        [Tooltip("A direct child of Arm, positioned at the contact point in the Rest sprite.")]
        [SerializeField] private RectTransform handPoint;
        [SerializeField] private Animator armAnimator;
        [SerializeField, Min(0.01f)] private float minimumDistance = 10f;
        [Header("Animator state paths (layer 0)")]
        [SerializeField] private string restState = "Base Layer.Arm_Rest";
        [SerializeField] private string pointState = "Base Layer.Arm_Point";
        [SerializeField] private string okState = "Base Layer.Arm_OK";
        [SerializeField] private string grabState = "Base Layer.Arm_Grab";

        private RectTransform arm;
        private Vector3 basePosition;
        private Quaternion baseRotation;
        private Vector3 baseScale;
        private Vector2 referenceVector;
        private Vector2 handPosition;
        private bool initialized;
        private bool controlling;

        public bool Initialize()
        {
            if (initialized)
                return true;

            arm = (RectTransform)transform;
            Canvas canvas = GetComponentInParent<Canvas>();
            if (viewModel == null || handPoint == null || arm.parent != viewModel || handPoint.parent != arm
                || canvas == null || canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                Debug.LogError("Arm requires an Overlay Canvas, its ViewModel parent and a direct HandPoint child.", this);
                return false;
            }

            Canvas.ForceUpdateCanvases();
            basePosition = arm.anchoredPosition3D;
            baseRotation = arm.localRotation;
            baseScale = arm.localScale;
            referenceVector = viewModel.InverseTransformPoint(handPoint.position) - arm.localPosition;
            if (referenceVector.sqrMagnitude < 0.0001f)
            {
                Debug.LogError("Move HandPoint away from the shoulder in the Rest pose.", this);
                return false;
            }

            if (armAnimator == null || armAnimator.gameObject != gameObject
                || armAnimator.runtimeAnimatorController == null)
            {
                Debug.LogError("Assign Arm's Animator with the four arm pose states.", this);
                return false;
            }

            armAnimator.enabled = true;
            foreach (ArmPose pose in System.Enum.GetValues(typeof(ArmPose)))
            {
                string state = GetStatePath(pose);
                if (string.IsNullOrEmpty(state) || !armAnimator.HasState(0, Animator.StringToHash(state)))
                {
                    Debug.LogError($"Arm Animator is missing the layer 0 state '{state}' for {pose}.", this);
                    return false;
                }
            }

            initialized = true;
            RestoreBaseTransform();
            return true;
        }

        public void BeginControl()
        {
            RestoreBaseTransform();
            handPosition = (Vector2)arm.localPosition + referenceVector;
            controlling = true;
            MoveHand(Vector2.zero);
        }

        public void MoveHand(Vector2 displacement)
        {
            if (!controlling)
                return;

            handPosition += displacement;
            ApplyHandPosition();
        }

        private void LateUpdate()
        {
            // Re-clamp after layout/resolution changes, and keep animation off the shoulder transform.
            if (controlling)
                ApplyHandPosition();
        }

        private void ApplyHandPosition()
        {
            arm.anchoredPosition3D = basePosition;
            Vector2 shoulder = arm.localPosition;
            Rect bounds = viewModel.rect;
            handPosition = ClampToRect(handPosition, bounds);
            Vector2 direction = handPosition - shoulder;
            float minDistance = Mathf.Max(0.01f, minimumDistance);

            if (direction.sqrMagnitude < minDistance * minDistance)
            {
                Vector2 outward = direction.sqrMagnitude > 0.000001f ? direction.normalized : referenceVector.normalized;
                handPosition = ClampToRect(shoulder + outward * minDistance, bounds);

                // At a screen edge the chosen direction can point outside the rectangle.
                // A segment to the farthest corner stays inside when the shoulder is on-screen.
                if ((handPosition - shoulder).sqrMagnitude < minDistance * minDistance - 0.0001f)
                {
                    Vector2 corner = new Vector2(
                        shoulder.x < bounds.center.x ? bounds.xMax : bounds.xMin,
                        shoulder.y < bounds.center.y ? bounds.yMax : bounds.yMin);
                    handPosition = Vector2.MoveTowards(shoulder, corner, minDistance);
                    handPosition = ClampToRect(handPosition, bounds);
                }
                direction = handPosition - shoulder;
            }

            if (direction.sqrMagnitude < 0.000001f)
                return;

            float angle = (Mathf.Atan2(direction.y, direction.x)
                - Mathf.Atan2(referenceVector.y, referenceVector.x)) * Mathf.Rad2Deg;
            float factor = direction.magnitude / referenceVector.magnitude;
            arm.localRotation = Quaternion.AngleAxis(angle, Vector3.forward) * baseRotation;
            arm.localScale = new Vector3(baseScale.x * factor, baseScale.y * factor, baseScale.z);
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
            // The non-looping clip holds its last frame; no transition or sprite assignment is needed.
            armAnimator.Play(Animator.StringToHash(state), 0, 0f);
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
            arm.anchoredPosition3D = basePosition;
            arm.localRotation = baseRotation;
            arm.localScale = baseScale;
        }

        public Vector2 GetHandScreenPosition()
        {
            // Overlay coordinates must be projected without the world camera.
            return RectTransformUtility.WorldToScreenPoint(null, handPoint.position);
        }

        private void OnDisable()
        {
            RestoreBaseTransform();
        }
    }
}
