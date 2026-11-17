using System;
using UnityEngine;

namespace Metroidbrainia.Hand3DExperiment
{
    // Scene adapter only. Input and execution timing belong to PoseActions, gameplay to the target.
    [DisallowMultipleComponent]
    public sealed class Hand3DPointInteractor : MonoBehaviour
    {
        [Serializable] public sealed class Target
        {
            public MonoBehaviour receiver;
            public Collider collider;
            [Tooltip("Optional aim/selection point; otherwise the collider bounds center is used.")]
            public Transform interactionPoint;
            [Min(.01f)] public float maximumDistance = 1.8f;
            [Tooltip("Extra tolerance at 1080p. Zero requires an actual hit through the visible fingertip.")]
            [Min(0f)] public float tolerancePixels;
        }
        [SerializeField] private Hand3DContactDetection detection = new Hand3DContactDetection();
        [Tooltip("Index distal bone or a PointFingertip child. Detection uses this transform plus the local offset.")]
        [SerializeField] private Transform pointFingertip;
        [SerializeField] private Vector3 fingertipOffset;
        [SerializeField] private FlexedFingers pointCombination = (FlexedFingers)29;
        [SerializeField, Min(.01f)] private float maximumDistance = 2f;
        [SerializeField] private Target[] targets = Array.Empty<Target>();
        private Hand3DActionContext lastRequest;
        private Hand3DRig rig;
        private Hand3DArmMovement movement;
        private Hand3DInputController input;
        public MonoBehaviour LastTarget { get; private set; }
        public bool LastAccepted { get; private set; }
        public string Diagnostic { get; private set; } = "No Point request.";
        public Transform PointFingertip => pointFingertip;
        public Vector3 FingertipPosition => pointFingertip != null ? pointFingertip.TransformPoint(fingertipOffset) : Vector3.zero;

        private bool Allowed(Hand3DActionContext context)
        {
            return isActiveAndEnabled && ExperimentScope.IsAllowed(this) && context != null && rig != null
                && rig.gameObject == gameObject && rig.IsReady && rig.isActiveAndEnabled
                && movement != null && movement.IsReady && movement.isActiveAndEnabled
                && input != null && input.isActiveAndEnabled
                && context.Mode == Hand3DMode.Exploration && !movement.Modes.IsTransitioning
                && context.ArmControlHeld && context.Gesture != null && context.Gesture.Combination == pointCombination
                && context.Fingers == pointCombination && context.Interactions != null && context.Interactions.gameObject == gameObject && context.Interactions.CanExecuteGestureAction
                && pointFingertip != null && pointFingertip.IsChildOf(movement.Hand) && Time.timeScale > 0f;
        }
        public bool CanPerform(Hand3DActionContext context)
        {
            if (!ReferenceEquals(lastRequest, context)) { LastTarget = null; LastAccepted = false; }
            return Select(context, out _, out _);
        }
        private bool Select(Hand3DActionContext context, out Target selected, out RaycastHit selectedHit)
        {
            selected = null; selectedHit = default;
            if (rig == null) rig = GetComponent<Hand3DRig>();
            if (movement == null) movement = GetComponent<Hand3DArmMovement>();
            if (input == null) input = GetComponent<Hand3DInputController>();
            if (!Allowed(context)) { Diagnostic = "Point requires Exploration, active arm control and a free hand."; return false; }
            detection.Bind(movement, input, transform);
            detection.HandPoint = pointFingertip;
            if (!detection.IsReady) { Diagnostic = "Missing Point camera/contact configuration."; return false; }
            detection.ResolveDisplays();
            var hand = detection.SampleHand(fingertipOffset);
            if (!hand.visible) { Diagnostic = hand.reason; return false; }
            bool synchronized = false; float best = float.PositiveInfinity;
            foreach (var target in targets)
            {
                if (target == null || target.receiver == null || !target.receiver.isActiveAndEnabled || target.receiver.gameObject.scene != gameObject.scene
                    || !(target.receiver is IHandActionInteractable) || target.collider == null
                    || !ReferenceEquals(target.collider.GetComponentInParent<IHandActionInteractable>(), target.receiver)) continue;
                float reach = Mathf.Min(maximumDistance, target.maximumDistance);
                bool hitTarget = detection.TryHitTarget(target.collider, hand.point, reach, ref synchronized, out RaycastHit hit);
                Vector3 aim = target.interactionPoint != null ? target.interactionPoint.position : target.collider.bounds.center;
                bool projected = detection.TryWorldToScreen(aim, out Vector2 targetScreen, out _);
                if (!hitTarget && target.tolerancePixels > 0f && projected
                    && Vector2.Distance(targetScreen, hand.point) <= Hand3DContactDetection.ReferencePixels(target.tolerancePixels, Screen.height))
                    hitTarget = detection.TryHitTarget(target.collider, targetScreen, reach, ref synchronized, out hit);
                if (!hitTarget) continue;
                var contact = detection.Evaluate(hand, default, hit.point, hit.point, Mathf.Max(.01f, target.tolerancePixels), false);
                if (!contact.visible || !contact.WithinReach(reach) || !contact.WithinContact
                    || detection.HasObstacle(hit.point, target.receiver.transform, ref synchronized, out _, QueryTriggerInteraction.Collide)) continue;
                float score = projected ? Vector2.Distance(hand.point, targetScreen) : contact.separation;
                // Serialized order is the stable tie-break, independent of object instance IDs.
                if (selected != null && score >= best - .001f) continue;
                best = score; selected = target; selectedHit = hit;
            }
            Diagnostic = selected != null ? "Point candidate: " + selected.receiver.name : "No visible, reachable Point contact.";
            return selected != null;
        }
        public bool TryPerform(Hand3DActionContext context)
        {
            if (ReferenceEquals(lastRequest, context)) return false;
            // Set before invoking gameplay: callbacks and duplicate action definitions cannot reenter this request.
            lastRequest = context; LastTarget = null; LastAccepted = false;
            if (!Select(context, out var target, out var hit)) return false;
            LastTarget = target.receiver;
            string targetName = target.receiver.name;
            LastAccepted = ((IHandActionInteractable)target.receiver).TryPerformHandAction(ArmPose.Point, detection.WorldCamera, hit);
            Diagnostic = LastAccepted ? "Point accepted: " + targetName : "Point rejected by original target: " + targetName;
            return LastAccepted;
        }
        // Keep the request token through a disable/re-enable inside a gameplay callback.
        private void OnDisable() { LastTarget = null; LastAccepted = false; }
    }
}
