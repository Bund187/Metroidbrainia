using System;
using System.Collections.Generic;
using UnityEngine;

namespace Metroidbrainia.Hand3DExperiment
{
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class Hand3DRig : MonoBehaviour
    {
        [Serializable]
        public sealed class Joint
        {
            [SerializeField] private Transform bone;
            [SerializeField] private Vector3 extendedLocalEulerAngles;
            [SerializeField] private Vector3 flexedLocalEulerAngles;
            [SerializeField, HideInInspector] private bool extendedCaptured;
            [SerializeField, HideInInspector] private bool flexedCaptured;
            public Transform Bone => bone;
            public Quaternion Extended => Quaternion.Euler(extendedLocalEulerAngles);
            public Quaternion Flexed => Quaternion.Euler(flexedLocalEulerAngles);
            public bool Captured => extendedCaptured && flexedCaptured;
        }

        [Serializable]
        public sealed class FingerBinding
        {
            [SerializeField] private Finger finger;
            [SerializeField] private Joint[] joints = new Joint[0];
            public Finger Finger => finger;
            public Joint[] Joints => joints;
            public FingerBinding(Finger value) => finger = value;
        }

        [SerializeField] private Hand3DFingerState fingerState;
        [SerializeField] private Hand3DPoseRecognizer poseRecognizer;
        [SerializeField] private Hand3DPoseActions poseActions;
        [Tooltip("Optional Animator on this scene instance. Suspended while the procedural rig owns the bones.")]
        [SerializeField] private Animator animatorToSuspend;
        [SerializeField, Min(0f)] private float transitionDuration = 0.2f;
        [SerializeField] private bool logVisualTransitions;
        [SerializeField] private FingerBinding[] fingers =
        {
            new FingerBinding(Finger.Thumb), new FingerBinding(Finger.Index), new FingerBinding(Finger.Middle),
            new FingerBinding(Finger.Ring), new FingerBinding(Finger.Little)
        };

        private sealed class DrivenBone
        {
            public Transform transform;
            public Joint joint;
            public Finger finger;
            public Quaternion original;
            public Quaternion start;
            public Quaternion target;
            public Quaternion visualRotation;
        }

        private readonly List<DrivenBone> driven = new List<DrivenBone>();
        private readonly Dictionary<Transform, DrivenBone> lookup = new Dictionary<Transform, DrivenBone>();
        private bool initialized;
        private bool animatorWasEnabled;
        private float elapsed;
        private bool transitionPending;
        private Hand3DArmMovement armMovement;
        private Quaternion wristOffset = Quaternion.identity;
        private Quaternion wristFrom = Quaternion.identity;
        private Quaternion wristTarget = Quaternion.identity;
        private HandPose wristPose;
        private bool wristEditing;
        private float wristElapsed;
        private float wristDuration = 0.2f;
        private AnimationCurve wristEasing;
        private Hand3DInteractionPose interactionPose;

        public bool IsReady => initialized;
        public FingerBinding[] Fingers => fingers;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (!Initialize())
            {
                enabled = false;
                return;
            }
            fingerState.Changed += RequestTransition;
            if (poseRecognizer != null)
                poseRecognizer.PoseChanged += RequestTransition;
        }

        public bool Initialize()
        {
            if (initialized)
                return true;
            if (fingerState == null) fingerState = GetComponent<Hand3DFingerState>();
            if (poseRecognizer == null) poseRecognizer = GetComponent<Hand3DPoseRecognizer>();
            if (poseActions == null) poseActions = GetComponent<Hand3DPoseActions>();
            if (!ExperimentScope.IsAllowed(this) || fingerState == null || fingers == null)
                return Fail("Use this rig only in TestArmScene and assign its finger state and five finger bindings.");

            driven.Clear();
            lookup.Clear();
            var configured = new HashSet<Finger>();
            foreach (FingerBinding binding in fingers)
            {
                if (binding == null || binding.Finger < Finger.Thumb || binding.Finger > Finger.Little
                    || !configured.Add(binding.Finger) || binding.Joints == null || binding.Joints.Length == 0)
                    return Fail("Configure each finger once, with at least one joint.");
                foreach (Joint joint in binding.Joints)
                {
                    if (joint == null || !ValidBone(joint.Bone) || !joint.Captured || lookup.ContainsKey(joint.Bone))
                        return Fail("Assign unique scene bones and capture both Extended and Flexed for every joint before Play.");
                    AddBone(joint.Bone, joint, binding.Finger);
                }
            }
            if (configured.Count != 5)
                return Fail("All five fingers need a binding.");

            Hand3DArmMovement movement = GetComponent<Hand3DArmMovement>();
            armMovement = movement;
            interactionPose = GetComponent<Hand3DInteractionPose>();
            foreach (DrivenBone bone in driven)
                if (movement != null && movement.ControlsRotationOf(bone.transform))
                    return Fail("Finger joints cannot own arm bones controlled by movement.");
            if (poseRecognizer != null && poseRecognizer.Poses != null)
            {
                foreach (HandPose pose in poseRecognizer.Poses)
                {
                    if (pose == null || pose.VisualOverrides == null)
                        continue;
                    var poseBones = new HashSet<Transform>();
                    foreach (LocalBonePose adjustment in pose.VisualOverrides)
                    {
                        if (adjustment == null || !adjustment.Captured || !ValidBone(adjustment.Bone)
                            || !poseBones.Add(adjustment.Bone)
                            || movement != null && movement.ControlsRotationOf(adjustment.Bone))
                            return Fail("Pose overrides must reference this rig's scene bones, excluding the upper arm and forearm controlled by movement.");
                        if (!lookup.ContainsKey(adjustment.Bone))
                            AddBone(adjustment.Bone, null, default);
                    }
                }
            }
            if (animatorToSuspend != null)
            {
                if (animatorToSuspend.gameObject.scene != gameObject.scene)
                    return Fail("The Animator must belong to the experimental scene.");
                animatorWasEnabled = animatorToSuspend.enabled;
                animatorToSuspend.enabled = false;
            }
            if (interactionPose != null)
            foreach (var profile in new[] { interactionPose.Overrides, interactionPose.HandHoldOverrides })
            {
                var grabBones = new HashSet<Transform>();
                foreach (LocalBonePose entry in profile)
                {
                    if (entry == null || !entry.Captured || !ValidBone(entry.Bone) || !grabBones.Add(entry.Bone)
                        || movement != null && movement.ControlsRotationOf(entry.Bone))
                        return Fail("Grab overrides need captured, unique scene bones, excluding arm joints controlled by movement.");
                    if (!lookup.ContainsKey(entry.Bone)) AddBone(entry.Bone, null, default);
                }
            }
            if (movement != null && movement.IsReady && !lookup.ContainsKey(movement.Hand))
                AddBone(movement.Hand, null, default);
            if (interactionPose != null)
            foreach (var profile in new[] { interactionPose.AdditionalBones, interactionPose.HandHoldBones })
            {
                var additionalBones = new HashSet<Transform>();
                foreach (Hand3DInteractionPose.AdditionalBone entry in profile)
                {
                    if (entry == null || !entry.Captured || !ValidBone(entry.Bone) || !additionalBones.Add(entry.Bone)
                        || movement != null && (movement.ControlsRotationOf(entry.Bone) || !entry.Bone.IsChildOf(movement.Forearm)))
                        return Fail("Capture Neutral and Grab for unique wrist/forearm Twist bones. Main forearm/upper arm are reserved for IK.");
                    if (lookup.TryGetValue(entry.Bone, out DrivenBone existing) && existing.joint != null)
                        return Fail("Additional Grab bones must not duplicate configured finger joints.");
                    if (!lookup.ContainsKey(entry.Bone)) AddBone(entry.Bone, null, default);
                    // Wrist uses the movement's reference basis; other extra bones restore their captured neutral.
                    if (movement == null || entry.Bone != movement.Hand)
                        lookup[entry.Bone].original = lookup[entry.Bone].visualRotation = entry.Neutral;
                }
            }
            if (interactionPose != null && interactionPose.UseHoldPositions)
                foreach (var entry in interactionPose.HandHoldPositions)
                {
                    if (entry == null || !lookup.TryGetValue(entry.Bone, out var bone) || bone.joint == null)
                        return Fail("HandHold translations must reference configured finger joints only.");
                    entry.Bone.localPosition = entry.Neutral;
                }
            initialized = true;
            BeginTransition();
            foreach (DrivenBone bone in driven)
            {
                bone.visualRotation = bone.target;
                bone.transform.localRotation = bone.target;
            }
            elapsed = transitionDuration;
            return true;
        }

        private bool ValidBone(Transform bone)
        {
            return bone != null && bone != transform && bone.gameObject.scene == gameObject.scene && bone.IsChildOf(transform);
        }

        private void AddBone(Transform transform, Joint joint, Finger finger)
        {
            var bone = new DrivenBone { transform = transform, joint = joint, finger = finger,
                original = transform.localRotation, visualRotation = transform.localRotation };
            driven.Add(bone);
            lookup.Add(transform, bone);
        }

        private bool Fail(string message)
        {
            Debug.LogError(message, this);
            driven.Clear();
            lookup.Clear();
            return false;
        }

        private void BeginTransition()
        {
            if (!initialized)
                return;
            elapsed = 0f;
            foreach (DrivenBone bone in driven)
            {
                // Keep the pose blend independent of the wrist compensation applied after IK.
                bone.start = bone.visualRotation;
                bone.target = bone.joint == null ? bone.original
                    : fingerState.IsFlexed(bone.finger) ? bone.joint.Flexed : bone.joint.Extended;
            }
            // Resolve from the current mask so event subscription order cannot select a stale pose.
            HandPose pose = poseRecognizer != null ? poseRecognizer.Recognize(fingerState.Current) : null;
            if (logVisualTransitions)
                Debug.Log($"[Hand3D Application] Fingers: {fingerState.Current}; pose: {pose?.Name ?? "None"}; overrides: {pose?.VisualOverrides?.Length ?? 0}; duration: {transitionDuration}s.", this);
            if (pose == null || pose.VisualOverrides == null)
                return;
            foreach (LocalBonePose adjustment in pose.VisualOverrides)
                if (adjustment != null && adjustment.Bone != null && lookup.TryGetValue(adjustment.Bone, out DrivenBone bone))
                    bone.target = adjustment.Rotation;
        }

        private void RequestTransition() => transitionPending = true;

        private void LateUpdate() => UpdateRig(Time.deltaTime);

        private void UpdateRig(float deltaTime)
        {
            if (!initialized || deltaTime <= 0f)
                return;
            // Finger and recognition events can arrive together; start once from the displayed rotations.
            if (transitionPending)
            {
                transitionPending = false;
                BeginTransition();
            }
            elapsed += deltaTime;
            UpdateWristOrientation(deltaTime);
            if (interactionPose != null) interactionPose.Tick(deltaTime);
            float grabWeight = interactionPose != null && interactionPose.isActiveAndEnabled ? interactionPose.Weight : 0f;
            float t = transitionDuration > 0f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / transitionDuration)) : 1f;
            float clickWeight = poseActions != null ? poseActions.PointClickWeight : 0f;
            foreach (DrivenBone bone in driven)
            {
                if (bone.transform == null)
                    continue;
                Quaternion rotation = Quaternion.Slerp(bone.start, bone.target, t);
                if (bone.joint != null && bone.finger == Finger.Index && clickWeight > 0f)
                    rotation = Quaternion.Slerp(rotation, bone.joint.Flexed, clickWeight);
                bone.visualRotation = rotation;
                Quaternion grabRotation = bone.joint != null ? bone.joint.Flexed : rotation;
                if (interactionPose != null && interactionPose.TryGetRotation(bone.transform, out Quaternion capturedGrab))
                    grabRotation = capturedGrab;
                if (interactionPose != null && interactionPose.TryGetAdditionalBone(bone.transform, out var additional))
                    grabRotation = armMovement != null && bone.transform == armMovement.Hand
                        ? bone.original * Quaternion.Inverse(additional.Neutral) * additional.Grab : additional.Grab;
                rotation = Quaternion.Slerp(rotation, grabRotation, grabWeight);
                bone.transform.localRotation = armMovement != null && bone.transform == armMovement.Hand
                    ? armMovement.ComposeHandRotation(rotation, Quaternion.Slerp(wristOffset, Quaternion.identity, grabWeight))
                        * (interactionPose != null && interactionPose.isActiveAndEnabled ? interactionPose.WristOffset : Quaternion.identity) : rotation;
            }
            if (interactionPose != null && interactionPose.UseHoldPositions)
                foreach (var entry in interactionPose.HandHoldPositions)
                    if (entry != null && entry.Bone != null)
                        entry.Bone.localPosition = Vector3.Lerp(entry.Neutral, entry.Hold, interactionPose.HandHoldWeight);
            if (armMovement != null && armMovement.HasContact)
            {
                Quaternion worldRotation = armMovement.Hand.rotation;
                armMovement.ResolveContactAfterPose();
                // IK changes the wrist parent; the rig restores its final orientation without a competing writer.
                armMovement.Hand.rotation = worldRotation;
            }
        }

        private void UpdateWristOrientation(float deltaTime)
        {
            HandPose pose = poseRecognizer != null ? poseRecognizer.CurrentPose : null;
            bool editing = armMovement != null && armMovement.Modes != null && armMovement.Modes.IsEditing;
            Quaternion target = !editing && pose != null && pose.UseExplorationWrist ? pose.ExplorationWrist : Quaternion.identity;
            if (pose != wristPose || editing != wristEditing || Quaternion.Angle(target, wristTarget) > 0.01f)
            {
                if (pose != null)
                {
                    wristDuration = pose.WristTransitionDuration;
                    wristEasing = pose.WristTransitionEasing;
                }
                wristPose = pose;
                wristEditing = editing;
                wristFrom = wristOffset;
                wristTarget = target;
                wristElapsed = 0f;
            }
            wristElapsed += deltaTime;
            float t = wristDuration > 0f ? Mathf.Clamp01(wristElapsed / wristDuration) : 1f;
            float weight = t >= 1f ? 1f : wristEasing != null ? Mathf.Clamp01(wristEasing.Evaluate(t)) : Mathf.SmoothStep(0f, 1f, t);
            wristOffset = Quaternion.Slerp(wristFrom, wristTarget, weight);
        }

        private void OnDisable()
        {
            if (!initialized)
                return;
            fingerState.Changed -= RequestTransition;
            if (poseRecognizer != null) poseRecognizer.PoseChanged -= RequestTransition;
            foreach (DrivenBone bone in driven)
                if (bone.transform != null) bone.transform.localRotation = bone.original;
            if (interactionPose != null && interactionPose.UseHoldPositions)
                foreach (var entry in interactionPose.HandHoldPositions)
                    if (entry != null && entry.Bone != null) entry.Bone.localPosition = entry.Neutral;
            if (animatorToSuspend != null) animatorToSuspend.enabled = animatorWasEnabled;
            driven.Clear();
            lookup.Clear();
            initialized = false;
            transitionPending = false;
            wristOffset = wristFrom = wristTarget = Quaternion.identity;
            wristPose = null;
        }
    }
}

