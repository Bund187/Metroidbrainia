using System;
using UnityEngine;

namespace Metroidbrainia.Hand3DExperiment
{
    public enum Hand3DMode { Exploration, HandEdit }

    [Serializable]
    public sealed class ArmConfiguration
    {
        [SerializeField] private bool captured;
        [SerializeField] private Vector3 modelPositionInCamera;
        [SerializeField] private Vector3 modelEulerInCamera;
        [SerializeField] private Vector3 upperLocalEuler;
        [SerializeField] private Vector3 forearmLocalEuler;
        [SerializeField] private Vector3 handEulerInCamera;
        public bool Captured => captured;
        public ArmFrame Frame => new ArmFrame
        {
            position = modelPositionInCamera, rotation = Quaternion.Euler(modelEulerInCamera),
            upper = Quaternion.Euler(upperLocalEuler), forearm = Quaternion.Euler(forearmLocalEuler),
            hand = Quaternion.Euler(handEulerInCamera)
        };
        public static ArmConfiguration Capture(Hand3DArmMovement movement)
        {
            ArmFrame frame = movement.CaptureFrame();
            return new ArmConfiguration { captured = true, modelPositionInCamera = frame.position,
                modelEulerInCamera = frame.rotation.eulerAngles, upperLocalEuler = frame.upper.eulerAngles,
                forearmLocalEuler = frame.forearm.eulerAngles, handEulerInCamera = frame.hand.eulerAngles };
        }
    }

    public struct ArmFrame
    {
        public Vector3 position;
        public Quaternion rotation, upper, forearm, hand;
        public static ArmFrame Lerp(ArmFrame a, ArmFrame b, float t) => new ArmFrame
        {
            position = Vector3.Lerp(a.position, b.position, t), rotation = Quaternion.Slerp(a.rotation, b.rotation, t),
            upper = Quaternion.Slerp(a.upper, b.upper, t), forearm = Quaternion.Slerp(a.forearm, b.forearm, t),
            hand = Quaternion.Slerp(a.hand, b.hand, t)
        };
    }

    // Stores mode state only. Movement and rig retain exclusive ownership of their transforms.
    [DisallowMultipleComponent]
    public sealed class Hand3DModeController : MonoBehaviour
    {
        [SerializeField] private ArmConfiguration exploration = new ArmConfiguration();
        [SerializeField] private ArmConfiguration handEdit = new ArmConfiguration();
        [SerializeField, Min(0f)] private float transitionDuration = 0.35f;
        [Tooltip("Zero is the captured palm-facing right endpoint. Negative angles roll towards the back of the hand.")]
        [SerializeField] private float minimumInspectionAngle = -180f;
        [SerializeField] private float maximumInspectionAngle;
        [SerializeField, Min(0f)] private float inspectionSpeed = 120f;
        [SerializeField, Min(0f)] private float inspectionSmoothing = 0.08f;
        [Tooltip("Capturing the left limit sets this sign from the actual forearm axis.")]
        [SerializeField] private float inspectionAxisSign = 1f;
        private ArmFrame fallback, from;
        private float elapsed, targetAngle, angle;
        public Hand3DMode Mode { get; private set; }
        public bool IsEditing => Mode == Hand3DMode.HandEdit;
        public bool IsTransitioning => elapsed < transitionDuration;
        public ArmConfiguration Exploration => exploration;
        public ArmConfiguration HandEdit => handEdit;
        public float InspectionAngle => angle * inspectionAxisSign;
        public ArmFrame TargetFrame => (IsEditing ? handEdit : exploration).Captured
            ? (IsEditing ? handEdit : exploration).Frame : fallback;
        public event Action ModeChanged;

        public void Initialize(ArmFrame initial)
        {
            fallback = initial;
            Mode = Hand3DMode.Exploration;
            elapsed = transitionDuration;
            targetAngle = angle = maximumInspectionAngle;
            if (!handEdit.Captured)
                Debug.LogWarning("[Hand3D Modes] Capture HandEdit and its left roll limit before inspecting the hand. Uncaptured configurations use the original scene posture.", this);
        }

        public void Toggle(ArmFrame displayed)
        {
            if (!ExperimentScope.IsAllowed(this)) return;
            from = displayed;
            Mode = IsEditing ? Hand3DMode.Exploration : Hand3DMode.HandEdit;
            elapsed = 0f;
            targetAngle = angle = maximumInspectionAngle;
            ModeChanged?.Invoke();
        }

        public void Inspect(float integratedHorizontalInput)
        {
            if (IsEditing)
                targetAngle = Mathf.Clamp(targetAngle + integratedHorizontalInput * inspectionSpeed,
                    minimumInspectionAngle, maximumInspectionAngle);
        }

        public ArmFrame Evaluate(float deltaTime, Vector3 longitudinalAxis, Quaternion forearmInCamera)
        {
            elapsed += deltaTime;
            float weight = inspectionSmoothing > 0f ? 1f - Mathf.Exp(-deltaTime / inspectionSmoothing) : 1f;
            angle = Mathf.Lerp(angle, targetAngle, weight);
            ArmFrame target = TargetFrame;
            if (IsEditing)
            {
                Quaternion roll = Quaternion.AngleAxis(InspectionAngle, longitudinalAxis);
                // Transform the bone's longitudinal axis; no assumed FBX X/Y/Z axis.
                Quaternion cameraRoll = forearmInCamera * roll * Quaternion.Inverse(forearmInCamera);
                target.forearm *= roll;
                target.hand = cameraRoll * target.hand;
            }
            float t = transitionDuration > 0f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / transitionDuration)) : 1f;
            return t < 1f ? ArmFrame.Lerp(from, target, t) : target;
        }

        private void OnValidate()
        {
            // The captured initial edit frame is always the right endpoint, never the middle of the range.
            maximumInspectionAngle = 0f;
            minimumInspectionAngle = Mathf.Clamp(minimumInspectionAngle, -180f, 0f);
            inspectionAxisSign = inspectionAxisSign < 0f ? -1f : 1f;
        }
    }
}
