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
            [Tooltip("Legacy selection metadata. Automatic Point always uses the actual fingertip hit.")]
            public Transform interactionPoint;
            [Min(.01f)] public float maximumDistance = 1.8f;
            [Tooltip("Legacy metadata, retained for serialization. Cannot expand the actual fingertip contact.")]
            [Min(0f)] public float tolerancePixels;
        }
        [SerializeField] private Hand3DContactDetection detection = new Hand3DContactDetection();
        [Tooltip("Index distal bone or a PointFingertip child. Detection uses this transform plus the local offset.")]
        [SerializeField] private Transform pointFingertip;
        [SerializeField] private Vector3 fingertipOffset;
        [SerializeField] private FlexedFingers pointCombination = (FlexedFingers)29;
        [SerializeField, Min(.01f)] private float maximumDistance = 2f;
        [Tooltip("Optional per-receiver/collider reach overrides. Unlisted objects are discovered automatically.")]
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
        private bool Select(Hand3DActionContext context, out MonoBehaviour selected, out RaycastHit selectedHit)
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
            bool synchronized = false;
            if (!detection.TryHitWorld(hand.point, maximumDistance, ref synchronized, out var hit))
            { Diagnostic = "No visible, reachable Point contact."; return false; }
            var receiver = hit.collider.GetComponentInParent<IHandActionInteractable>() as MonoBehaviour;
            if (receiver == null || !receiver.isActiveAndEnabled || receiver.gameObject.scene != gameObject.scene)
            { Diagnostic = "First Point surface has no active receiver in this scene."; return false; }
            float reach = maximumDistance;
            if (targets != null) foreach (var target in targets)
            {
                if (target == null || !ReferenceEquals(target.receiver, receiver) || target.collider != hit.collider) continue;
                // Preserve the first serialized override; never execute the list as a second action route.
                reach = Mathf.Min(reach, target.maximumDistance);
                break;
            }
            var contact = detection.Evaluate(hand, default, hit.point, hit.point, .01f, false);
            if (!contact.visible || !contact.WithinReach(reach) || !contact.WithinContact
                || detection.HasObstacle(hit.point, receiver.transform, ref synchronized, out _, QueryTriggerInteraction.Collide))
            { Diagnostic = "Point contact is blocked or outside its configured reach."; return false; }
            selected = receiver; selectedHit = hit;
            Diagnostic = "Point candidate: " + receiver.name;
            return true;
        }
        public bool TryPerform(Hand3DActionContext context)
        {
            if (ReferenceEquals(lastRequest, context)) return false;
            // Set before invoking gameplay: callbacks and duplicate action definitions cannot reenter this request.
            lastRequest = context; LastTarget = null; LastAccepted = false;
            if (!Select(context, out var target, out var hit)) return false;
            LastTarget = target;
            string targetName = target.name;
            LastAccepted = ((IHandActionInteractable)target).TryPerformHandAction(ArmPose.Point, detection.WorldCamera, hit);
            Diagnostic = LastAccepted ? "Point accepted: " + targetName : "Point rejected by original target: " + targetName;
            return LastAccepted;
        }
        // Keep the request token through a disable/re-enable inside a gameplay callback.
        private void OnDisable() { LastTarget = null; LastAccepted = false; }
    }
}
