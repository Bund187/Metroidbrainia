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
            if (movement != null && movement.IsReady && !lookup.ContainsKey(movement.Hand))
                AddBone(movement.Hand, null, default);
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

        private void LateUpdate()
        {
            if (!initialized || Time.deltaTime <= 0f)
                return;
            // Finger and recognition events can arrive together; start once from the displayed rotations.
            if (transitionPending)
            {
                transitionPending = false;
                BeginTransition();
            }
            elapsed += Time.deltaTime;
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
                bone.transform.localRotation = armMovement != null && bone.transform == armMovement.Hand
                    ? armMovement.HandRotationCompensation * rotation : rotation;
            }
        }

        private void OnDisable()
        {
            if (!initialized)
                return;
            fingerState.Changed -= RequestTransition;
            if (poseRecognizer != null) poseRecognizer.PoseChanged -= RequestTransition;
            foreach (DrivenBone bone in driven)
                if (bone.transform != null) bone.transform.localRotation = bone.original;
            if (animatorToSuspend != null) animatorToSuspend.enabled = animatorWasEnabled;
            driven.Clear();
            lookup.Clear();
            initialized = false;
            transitionPending = false;
        }
    }
}
