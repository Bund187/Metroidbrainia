using System;
using UnityEngine;
using UnityEngine.Events;

namespace Metroidbrainia.Hand3DExperiment
{
    public enum PoseAction { None, PointClick, EventOnly }

    [Serializable]
    public sealed class LocalBonePose
    {
        [SerializeField] private Transform bone;
        [SerializeField] private Vector3 localEulerAngles;
        [SerializeField, HideInInspector] private bool captured;
        public Transform Bone => bone;
        public Quaternion Rotation => Quaternion.Euler(localEulerAngles);
        public bool Captured => captured;
    }

    [Serializable]
    public sealed class HandPose
    {
        [SerializeField] private string poseName;
        [SerializeField] private FlexedFingers flexedFingers;
        [SerializeField] private PoseAction action;
        [SerializeField] private LocalBonePose[] visualOverrides = new LocalBonePose[0];
        [SerializeField] private UnityEvent onAction = new UnityEvent();

        public string Name => poseName;
        public FlexedFingers Combination => flexedFingers;
        public PoseAction Action => action;
        public LocalBonePose[] VisualOverrides => visualOverrides;
        public UnityEvent OnAction => onAction;

        public HandPose(string name, FlexedFingers combination, PoseAction poseAction = PoseAction.None)
        {
            poseName = name;
            flexedFingers = combination;
            action = poseAction;
        }
    }

    [DisallowMultipleComponent]
    public sealed class Hand3DPoseRecognizer : MonoBehaviour
    {
        [SerializeField] private Hand3DFingerState fingerState;
        [SerializeField] private bool logPoseChanges;
        [SerializeField] private HandPose[] poses =
        {
            new HandPose("OK", FlexedFingers.Thumb | FlexedFingers.Index),
            new HandPose("Point", FlexedFingers.Thumb | FlexedFingers.Middle | FlexedFingers.Ring | FlexedFingers.Little, PoseAction.PointClick)
        };

        public HandPose CurrentPose { get; private set; }
        public HandPose[] Poses => poses;
        public event Action PoseChanged;

        private void OnEnable()
        {
            if (fingerState == null)
                fingerState = GetComponent<Hand3DFingerState>();
            if (fingerState != null)
                fingerState.Changed += Recognize;
            Recognize();
        }

        public HandPose Recognize(FlexedFingers combination)
        {
            if (poses != null)
            {
                foreach (HandPose pose in poses)
                    if (pose != null && pose.Combination == combination)
                        return pose;
            }
            return null;
        }

        private void Recognize()
        {
            HandPose next = fingerState != null ? Recognize(fingerState.Current) : null;
            if (logPoseChanges)
                Debug.Log($"[Hand3D Recognition] Fingers: {fingerState?.Current}; pose: {next?.Name ?? "None"}.", this);
            if (CurrentPose == next)
                return;
            CurrentPose = next;
            PoseChanged?.Invoke();
        }

        private void OnDisable()
        {
            if (fingerState != null)
                fingerState.Changed -= Recognize;
        }

        private void OnValidate()
        {
            if (poses == null)
                return;
            for (int i = 0; i < poses.Length; i++)
            {
                if (poses[i] == null)
                    continue;
                for (int j = 0; j < i; j++)
                    if (poses[j] != null && poses[i].Combination == poses[j].Combination)
                        Debug.LogWarning("Duplicate finger combination: the first matching pose wins.", this);
            }
        }
    }
}
