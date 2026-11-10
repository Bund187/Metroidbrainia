using System;
using UnityEngine;
using UnityEngine.UI;

namespace Metroidbrainia.Hand3DExperiment
{
    // Scene-local query settings, not a component or interaction owner. Each caller owns its sweep lifetime.
    [Serializable]
    public sealed class Hand3DContactDetection
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Transform handGripPoint;
        [SerializeField] private RawImage armDisplay;
        [SerializeField] private RawImage worldDisplay;
        [SerializeField] private Vector2 gripScreenOffset;
        [SerializeField] private LayerMask interactionLayers = Physics.DefaultRaycastLayers;
        private Camera armCamera;
        private Transform owner, playerRoot;
        public Camera WorldCamera => worldCamera;
        public bool IsReady => worldCamera != null && armCamera != null && handGripPoint != null && owner != null
            && worldCamera.gameObject.scene == owner.gameObject.scene && handGripPoint.gameObject.scene == owner.gameObject.scene;
        public int HandProjections { get; private set; }
        public int TargetProjections { get; private set; }
        public int ObstacleQueries { get; private set; }
        public void ResetCounters() { HandProjections = TargetProjections = ObstacleQueries = 0; }

        public void Bind(Hand3DArmMovement movement, Hand3DInputController input, Transform owner)
        {
            armCamera = movement != null ? movement.ArmCamera : null;
            playerRoot = input != null ? input.PlayerRoot : null;
            this.owner = owner;
        }

        public void Configure(Camera camera, Transform grip, RawImage armImage, RawImage worldImage, Vector2 offset, LayerMask layers)
        {
            worldCamera = camera; handGripPoint = grip; armDisplay = armImage; worldDisplay = worldImage;
            gripScreenOffset = offset; interactionLayers = layers;
        }

        public void ResolveDisplays()
        {
            if (armDisplay == null) armDisplay = Hand3DScreenProjection.FindDisplay(armCamera);
            if (worldDisplay == null) worldDisplay = Hand3DScreenProjection.FindDisplay(worldCamera);
        }

        public struct HandSample
        {
            public Vector2 point;
            public bool visible;
            public string reason;
        }

        public struct Sweep
        {
            private Vector2 previous, previousSize;
            private bool hasPrevious;
            public Vector2 Start { get; private set; }
            public bool Available { get; private set; }
            public void Reset() { hasPrevious = Available = false; }
            public void Advance(HandSample hand, Vector2 size)
            {
                Available = hasPrevious && hand.visible && previousSize == size;
                Start = previous; previous = hand.point; previousSize = size; hasPrevious = hand.visible;
            }
        }

        public struct Contact
        {
            public Vector2 point;
            public bool visible;
            public string reason;
            public float distance, separation, sweptDistance, radius;
            public bool WithinReach(float maximum) => distance <= maximum;
            public bool WithinContact => sweptDistance <= radius;
            public bool UsedSweep => separation > radius && WithinContact;
            // Documents deliberately keep their existing strict score; doors retain their own tie-breaks.
            public float Score => separation <= radius ? separation : radius + sweptDistance;
        }

        public HandSample SampleHand()
        {
            HandProjections++;
            var hand = new HandSample();
            if (handGripPoint == null) { hand.reason = "Falta punto de contacto de mano"; return hand; }
            hand.visible = Hand3DScreenProjection.TryWorldToScreen(armCamera, armDisplay, handGripPoint.position, out hand.point, out hand.reason);
            hand.point += gripScreenOffset * ReferencePixels(1f, Screen.height);
            return hand;
        }

        public bool TryWorldToScreen(Vector3 position, out Vector2 point, out string reason)
        {
            TargetProjections++;
            return Hand3DScreenProjection.TryWorldToScreen(worldCamera, worldDisplay, position, out point, out reason);
        }

        public Contact Evaluate(HandSample hand, Sweep sweep, Vector3 visualPoint, Vector3 reachPoint, float referenceRadius, bool acquiring)
        {
            var result = new Contact { radius = ReferencePixels(referenceRadius, Screen.height), separation = float.PositiveInfinity };
            result.visible = TryWorldToScreen(visualPoint, out result.point, out result.reason);
            result.distance = worldCamera != null ? Vector3.Distance(worldCamera.transform.position, reachPoint) : float.PositiveInfinity;
            if (hand.visible && result.visible) result.separation = Vector2.Distance(hand.point, result.point);
            result.sweptDistance = acquiring && sweep.Available && hand.visible && result.visible
                ? DistanceToScreenSegment(result.point, sweep.Start, hand.point) : result.separation;
            return result;
        }

        // Synchronization is local to a query cycle; there is no cross-frame physics cache.
        public bool HasObstacle(Vector3 point, Transform targetRoot, ref bool synchronized, out Collider blocker)
        {
            if (!synchronized) { Physics.SyncTransforms(); synchronized = true; }
            ObstacleQueries++;
            blocker = null;
            Vector3 offset = point - worldCamera.transform.position;
            float nearest = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(worldCamera.transform.position, offset.normalized,
                Mathf.Max(0f, offset.magnitude - .001f), interactionLayers, QueryTriggerInteraction.Ignore))
            {
                Transform item = hit.collider.transform;
                if (item.IsChildOf(owner) || playerRoot != null && item.IsChildOf(playerRoot)) continue;
                if (targetRoot != null && item.IsChildOf(targetRoot)) continue;
                if (hit.distance < nearest) { nearest = hit.distance; blocker = hit.collider; }
            }
            return blocker != null;
        }

        public static float ReferencePixels(float value, float screenHeight) => value * screenHeight / 1080f;
        public static float DistanceToScreenSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 direction = end - start;
            float t = direction.sqrMagnitude > .0001f ? Mathf.Clamp01(Vector2.Dot(point - start, direction) / direction.sqrMagnitude) : 0f;
            return Vector2.Distance(point, start + direction * t);
        }
    }
}
