using System;
using UnityEngine;

namespace Metroidbrainia.Hand3DExperiment
{
    // A visual layer only: never changes Hand3DFingerState or the recognized logical pose.
    [DisallowMultipleComponent]
    public sealed class Hand3DInteractionPose : MonoBehaviour
    {
        [Serializable]
        public sealed class FingerPosition
        {
            [SerializeField] private Transform bone;
            [SerializeField] private Vector3 neutral, hold;
            public Transform Bone => bone;
            public Vector3 Neutral => neutral;
            public Vector3 Hold => hold;
            public void CaptureHold() { if (bone != null) hold = bone.localPosition; }
        }
        [Serializable]
        public sealed class AdditionalBone
        {
            [SerializeField] private Transform bone;
            [SerializeField] private Vector3 neutralLocalEulerAngles;
            [SerializeField] private Vector3 grabLocalEulerAngles;
            [SerializeField, HideInInspector] private Transform neutralBone;
            [SerializeField, HideInInspector] private bool neutralCaptured;
            [SerializeField, HideInInspector] private bool grabCaptured;
            public Transform Bone => bone;
            public Quaternion Neutral => Quaternion.Euler(neutralLocalEulerAngles);
            public Quaternion Grab => Quaternion.Euler(grabLocalEulerAngles);
            public bool HasNeutral => neutralCaptured && neutralBone == bone;
            public bool Captured => HasNeutral && grabCaptured;
        }

        [SerializeField, Min(0f)] private float transitionDuration = 0.12f;
        [SerializeField] private LocalBonePose[] grabOverrides = new LocalBonePose[0];
        [Tooltip("Optional wrist/forearm Twist bones. Capture Neutral before posing. The main forearm and upper arm remain owned by IK.")]
        [SerializeField] private AdditionalBone[] additionalGrabBones = new AdditionalBone[0];
        [Header("HandHold (independent of Grab)")]
        [SerializeField] private LocalBonePose[] handHoldOverrides = new LocalBonePose[0];
        [SerializeField] private AdditionalBone[] additionalHandHoldBones = new AdditionalBone[0];
        [SerializeField, Min(0f)] private float handHoldTransitionDuration = 0.12f;
        [Tooltip("Optional small local finger translations. Capture the base before editing; never translate arm joints.")]
        [SerializeField] private bool captureHandHoldFingerPositions;
        [SerializeField, HideInInspector] private FingerPosition[] handHoldPositions = new FingerPosition[0];
        private bool handHolding;
        private bool grabbing;
        private float elapsed;
        private float from;
        private Quaternion wristOffset = Quaternion.identity;
        private Quaternion wristFrom = Quaternion.identity;
        private Quaternion wristTarget = Quaternion.identity;
        public float Weight { get; private set; }
        public bool IsGrabbing => grabbing;
        public LocalBonePose[] Overrides => grabOverrides;
        public AdditionalBone[] AdditionalBones => additionalGrabBones;
        public Quaternion WristOffset => wristOffset;
        public bool IsHandHolding => handHolding && grabbing;
        public LocalBonePose[] HandHoldOverrides => handHoldOverrides;
        public AdditionalBone[] HandHoldBones => additionalHandHoldBones;
        public FingerPosition[] HandHoldPositions => handHoldPositions;
        public bool UseHoldPositions => captureHandHoldFingerPositions;
        public float HandHoldWeight => handHolding ? Weight : 0f;

        public void SetHandHold(bool active)
        {
            if (active)
            {
                if (grabbing && !handHolding) return;
                handHolding = true;
                SetInteraction(true, Quaternion.identity);
            }
            else if (handHolding) SetInteraction(false, Quaternion.identity);
        }

        public void SetGrab(bool active) => SetGrab(active, Quaternion.identity);

        public void SetGrab(bool active, Quaternion doorWristOffset)
        {
            if (active)
            {
                if (grabbing && handHolding) return;
                handHolding = false;
            }
            else if (handHolding) return;
            SetInteraction(active, doorWristOffset);
        }

        private void SetInteraction(bool active, Quaternion doorWristOffset)
        {
            active &= ExperimentScope.IsAllowed(this) && isActiveAndEnabled;
            Quaternion target = active ? doorWristOffset : Quaternion.identity;
            if (grabbing == active && Quaternion.Angle(target, wristTarget) < 0.01f) return;
            grabbing = active;
            from = Weight;
            wristFrom = wristOffset;
            wristTarget = target;
            elapsed = 0f;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            elapsed += deltaTime;
            float duration = handHolding ? handHoldTransitionDuration : transitionDuration;
            float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            Weight = Mathf.Lerp(from, grabbing ? 1f : 0f, Mathf.SmoothStep(0f, 1f, t));
            wristOffset = Quaternion.Slerp(wristFrom, wristTarget, Mathf.SmoothStep(0f, 1f, t));
        }

        public bool TryGetRotation(Transform bone, out Quaternion rotation)
        {
            var overrides = handHolding ? handHoldOverrides : grabOverrides;
            if (overrides != null)
                foreach (LocalBonePose entry in overrides)
                    if (entry != null && entry.Captured && entry.Bone == bone)
                    {
                        rotation = entry.Rotation;
                        return true;
                    }
            rotation = Quaternion.identity;
            return false;
        }

        public bool TryGetAdditionalBone(Transform bone, out AdditionalBone result)
        {
            var bones = handHolding ? additionalHandHoldBones : additionalGrabBones;
            if (bones != null)
                foreach (AdditionalBone entry in bones)
                    if (entry != null && entry.Captured && entry.Bone == bone)
                    { result = entry; return true; }
            result = null;
            return false;
        }

        private void OnDisable()
        {
            grabbing = false;
            Weight = elapsed = from = 0f;
            wristOffset = wristFrom = wristTarget = Quaternion.identity;
        }
    }
}
