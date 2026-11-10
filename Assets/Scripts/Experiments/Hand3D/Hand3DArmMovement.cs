using UnityEngine;

namespace Metroidbrainia.Hand3DExperiment
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class Hand3DArmMovement : MonoBehaviour
    {
        [SerializeField] private Camera armCamera;
        [SerializeField] private Transform upperArm;
        [SerializeField] private Transform forearm;
        [SerializeField] private Transform hand;
        [SerializeField] private Transform elbowHint;
        [Tooltip("Stick speed in reference-screen units per second. Mouse delta already measures displacement.")]
        [SerializeField, Min(0f)] private float movementSpeed = 900f;
        [SerializeField, Min(0f)] private float movementSmoothing = 0.08f;
        [Tooltip("Input reference size, independent of the pixelated RenderTexture resolution.")]
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
        [Tooltip("Normalized viewport bounds for the wrist. Leave room for the fingers.")]
        [SerializeField] private Vector2 horizontalScreenLimits = new Vector2(0.06f, 0.94f);
        [SerializeField] private Vector2 verticalScreenLimits = new Vector2(0.06f, 0.94f);
        [SerializeField, Min(0.01f)] private float returnSmoothTime = 0.18f;

        private Quaternion upperRotation;
        private Quaternion forearmRotation;
        private Quaternion handRotation;
        private Quaternion restHandRotationInCamera;
        private Vector3 forearmPosition;
        private Vector3 handPosition;
        private Vector3 bendDirectionInCamera;
        private Vector2 restScreenPosition;
        private Vector2 targetScreenPosition;
        private Vector2 screenPosition;
        private Vector2 returnVelocity;
        private float handDepth;
        private float upperLength;
        private float forearmLength;
        private bool initialized;
        private bool controlling;
        private bool returning;
        private Hand3DModeController modes;
        private Vector3 longitudinalAxis;
        private Vector3 originalModelPosition;
        private Quaternion originalModelRotation;
        private Quaternion desiredHandInCamera;
        private Transform contactPoint;
        private Vector3 contactTarget;
        private bool returningFromContact;
        private float releaseDepth;
        private Hand3DRig rig;

        public bool IsReady => initialized;
        public Transform Hand => hand;
        public Transform UpperArm => upperArm;
        public Transform Forearm => forearm;
        public Camera ArmCamera => armCamera;
        public Hand3DModeController Modes => modes;
        public Vector3 ReferenceLongitudinalAxis => longitudinalAxis;
        public Vector3 LastUniversalRequestedTarget { get; private set; }
        public Vector3 LastUniversalFeasibleTarget { get; private set; }
        public float LastUniversalReachError { get; private set; }
        public bool LastUniversalTargetLimited { get; private set; }
        public void SetUniversalReferenceOrientation(Quaternion inCamera) => desiredHandInCamera=inCamera;
        public bool HasContact => contactPoint != null;
        public Vector3 LongitudinalAxis => forearm.InverseTransformDirection(hand.position - forearm.position).normalized;
        public bool ControlsRotationOf(Transform bone) => bone == upperArm || bone == forearm;

        // The rig composes this with its wrist pose and remains the sole writer of wrist/finger rotations.
        public Quaternion HandRotationCompensation => initialized && isActiveAndEnabled
            ? Quaternion.Inverse(hand.parent.rotation) * armCamera.transform.rotation * desiredHandInCamera * Quaternion.Inverse(handRotation)
            : Quaternion.identity;

        public Quaternion ComposeHandRotation(Quaternion visualRotation, Quaternion poseOffset) => initialized && isActiveAndEnabled
            ? Quaternion.Inverse(hand.parent.rotation) * armCamera.transform.rotation * desiredHandInCamera
                * poseOffset * Quaternion.Inverse(handRotation) * visualRotation : visualRotation;

        private void OnEnable()
        {
            if (Application.isPlaying && !Initialize()) enabled = false;
        }

        public bool Initialize()
        {
            if (initialized) return true;
            rig=GetComponent<Hand3DRig>();
            if (!ExperimentScope.IsAllowed(this) || armCamera == null || upperArm == null || forearm == null || hand == null
                || upperArm == forearm || forearm == hand || !forearm.IsChildOf(upperArm) || !hand.IsChildOf(forearm)
                || !upperArm.IsChildOf(transform) || upperArm.gameObject.scene != gameObject.scene
                || armCamera.transform.IsChildOf(transform)
                || hand.gameObject.scene != gameObject.scene || armCamera.gameObject.scene != gameObject.scene)
            {
                Debug.LogError("In TestArmScene assign the pixel arm camera and UpperArm > Forearm > Hand scene chain.", this);
                return false;
            }
            Vector3 viewport = armCamera.WorldToViewportPoint(hand.position);
            upperLength = Vector3.Distance(upperArm.position, forearm.position);
            forearmLength = Vector3.Distance(forearm.position, hand.position);
            if (viewport.z <= armCamera.nearClipPlane || viewport.z >= armCamera.farClipPlane
                || upperLength < 0.0001f || forearmLength < 0.0001f)
            {
                Debug.LogError("The hand must be inside camera depth bounds, with two nonzero arm segments.", this);
                return false;
            }
            upperRotation = upperArm.localRotation;
            forearmRotation = forearm.localRotation;
            handRotation = hand.localRotation;
            forearmPosition = forearm.localPosition;
            handPosition = hand.localPosition;
            originalModelPosition = transform.localPosition;
            originalModelRotation = transform.localRotation;
            restHandRotationInCamera = Quaternion.Inverse(armCamera.transform.rotation) * hand.rotation;
            desiredHandInCamera = restHandRotationInCamera;
            longitudinalAxis = LongitudinalAxis;
            handDepth = viewport.z;
            restScreenPosition = new Vector2(viewport.x, viewport.y);
            screenPosition = targetScreenPosition = restScreenPosition;
            Vector3 direction = (hand.position - upperArm.position).normalized;
            Vector3 bend = forearm.position - upperArm.position;
            bend -= direction * Vector3.Dot(bend, direction);
            if (bend.sqrMagnitude < 0.000001f) bend = armCamera.transform.right;
            bendDirectionInCamera = armCamera.transform.InverseTransformDirection(bend.normalized);
            initialized = true;
            modes = GetComponent<Hand3DModeController>();
            if (modes == null) modes = gameObject.AddComponent<Hand3DModeController>();
            modes.Initialize(CaptureFrame());
            return true;
        }

        public ArmFrame CaptureFrame() => new ArmFrame
        {
            position = armCamera.transform.InverseTransformPoint(transform.position),
            rotation = Quaternion.Inverse(armCamera.transform.rotation) * transform.rotation,
            upper = upperArm.localRotation, forearm = forearm.localRotation,
            hand = Quaternion.Inverse(armCamera.transform.rotation) * hand.rotation
        };

        public void ApplyFrame(ArmFrame frame)
        {
            if(rig!=null && rig.UniversalActive)return;
            transform.SetPositionAndRotation(armCamera.transform.TransformPoint(frame.position), armCamera.transform.rotation * frame.rotation);
            upperArm.localRotation = frame.upper;
            forearm.localRotation = frame.forearm;
            if (initialized)
            {
                forearm.localPosition = forearmPosition;
                hand.localPosition = handPosition;
            }
            desiredHandInCamera = frame.hand;
        }

        public void ToggleMode()
        {
            if (!initialized) return;
            ReleaseContact();
            ArmFrame displayed = CaptureFrame();
            displayed.hand = desiredHandInCamera;
            modes.Toggle(displayed);
            returningFromContact = false;
            controlling = returning = false;
            returnVelocity = Vector2.zero;
        }

        public void BeginControl()
        {
            if (!initialized || modes.IsEditing || modes.IsTransitioning) return;
            controlling = true;
            returning = false;
            targetScreenPosition = screenPosition;
            returnVelocity = Vector2.zero;
        }

        public void EndControl()
        {
            if (!initialized || !controlling) return;
            ReleaseContact();
            controlling = false;
            returning = true;
            targetScreenPosition = restScreenPosition;
            returnVelocity = Vector2.zero;
        }

        // Stick input arrives integrated with deltaTime; relative mouse delta must not be integrated again.
        public void MoveHand(Vector2 displacement, bool usingMouse = false)
        {
            if (!initialized || !controlling || modes.IsEditing || displacement.sqrMagnitude == 0f) return;
            Vector2 pixels = displacement * (usingMouse ? 1f : movementSpeed);
            targetScreenPosition = ClampToScreen(targetScreenPosition + new Vector2(
                pixels.x / referenceResolution.x, pixels.y / referenceResolution.y));
        }

        public Vector2 GetScreenDisplacement(Vector2 integratedInput, bool usingMouse, Camera worldCamera)
        {
            Vector2 pixels = integratedInput * (usingMouse ? 1f : movementSpeed);
            return new Vector2(pixels.x / referenceResolution.x * worldCamera.pixelWidth,
                pixels.y / referenceResolution.y * worldCamera.pixelHeight);
        }

        public void FollowContact(Transform gripPoint, Vector3 target)
        {
            if (!initialized || modes.IsEditing || !controlling) return;
            contactPoint = gripPoint;
            contactTarget = target;
            returningFromContact = false;
        }

        public void ReleaseContact()
        {
            if (contactPoint == null) return;
            contactPoint = null;
            Vector3 viewport = armCamera.WorldToViewportPoint(hand.position);
            screenPosition = targetScreenPosition = new Vector2(viewport.x, viewport.y);
            releaseDepth = viewport.z;
            returningFromContact = true;
        }

        // Called by the rig after its final wrist orientation is known. Only this component rotates arm joints.
        public void ResolveContactAfterPose()
        {
            if(rig!=null && rig.UniversalActive)return;
            if (contactPoint == null) return;
            Vector3 gripOffset = contactPoint.position - hand.position;
            SolveArm(contactTarget - gripOffset);
        }

        private Vector2 ClampToScreen(Vector2 position) => new Vector2(
            Mathf.Clamp(position.x, horizontalScreenLimits.x, horizontalScreenLimits.y),
            Mathf.Clamp(position.y, verticalScreenLimits.x, verticalScreenLimits.y));

        private void LateUpdate() => UpdateMovement(Time.deltaTime);

        private void UpdateMovement(float deltaTime)
        {
            if (!initialized || deltaTime <= 0f) return;
            if(rig!=null){rig.PreparePoseSystem();if(rig.UniversalActive)return;}
            // Follow the dedicated camera after Starter Assets/Cinemachine. Never smooth camera attachment.
            ApplyFrame(modes.TargetFrame);
            Vector3 neutral=hand.position;
            Quaternion forearmInCamera = Quaternion.Inverse(armCamera.transform.rotation) * forearm.rotation;
            ApplyFrame(modes.Evaluate(deltaTime, longitudinalAxis, forearmInCamera));
            if(!AdvanceTarget(deltaTime,neutral,out Vector3 target))return;
            bool limited=SolveArm(target);AcceptReachedTarget(limited,hand.position);
        }

        private bool AdvanceTarget(float deltaTime,Vector3 neutral,out Vector3 target)
        {
            target=default;
            Vector3 neutralViewport=armCamera.WorldToViewportPoint(neutral);restScreenPosition=new Vector2(neutralViewport.x,neutralViewport.y);handDepth=neutralViewport.z;
            if (modes.IsEditing || modes.IsTransitioning)
            {
                screenPosition = targetScreenPosition = restScreenPosition;
                return false;
            }
            if (!controlling && !returning)
            {
                screenPosition = targetScreenPosition = restScreenPosition;
                return false;
            }
            if (HasContact) return false;
            float targetDepth = handDepth;
            if (returningFromContact)
            {
                float weight = movementSmoothing > 0f ? 1f - Mathf.Exp(-deltaTime / movementSmoothing) : 1f;
                releaseDepth = Mathf.Lerp(releaseDepth, handDepth, weight);
                if (Mathf.Abs(releaseDepth - handDepth) < 0.0001f) returningFromContact = false;
                else targetDepth = releaseDepth;
            }
            if (returning)
            {
                screenPosition = Vector2.SmoothDamp(screenPosition, restScreenPosition, ref returnVelocity,
                    returnSmoothTime, Mathf.Infinity, deltaTime);
                // The authored rest pose can be outside the interactive screen limits, just like ArmView.
                if (!returningFromContact && Vector2.Scale(screenPosition - restScreenPosition, referenceResolution).sqrMagnitude < 0.01f)
                {
                    screenPosition = targetScreenPosition = restScreenPosition;
                    returning = false;
                    return false;
                }
            }
            else
            {
                float weight = movementSmoothing > 0f ? 1f - Mathf.Exp(-deltaTime / movementSmoothing) : 1f;
                screenPosition = Vector2.Lerp(screenPosition, targetScreenPosition, weight);
            }
            target = armCamera.ViewportToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, targetDepth));
            return true;
        }
        private void AcceptReachedTarget(bool limited,Vector3 position)
        {
            if (limited && controlling)
            {
                // Do not accumulate an invisible target beyond the physical reach of the skeleton.
                Vector3 reached = armCamera.WorldToViewportPoint(position);
                screenPosition = targetScreenPosition = new Vector2(reached.x, reached.y);
            }
        }

        private bool SolveArm(Vector3 target)
        {
            Vector3 shoulder = upperArm.position;
            Vector3 pole=elbowHint!=null?elbowHint.position-shoulder:armCamera.transform.TransformDirection(bendDirectionInCamera);
            bool limited=CalculateElbow(ref target,shoulder,pole,out Vector3 elbow);
            upperArm.rotation = Quaternion.FromToRotation(forearm.position - shoulder, elbow - shoulder) * upperArm.rotation;
            forearm.rotation = Quaternion.FromToRotation(hand.position - forearm.position, target - forearm.position) * forearm.rotation;
            return limited;
        }

        private bool CalculateElbow(ref Vector3 target,Vector3 shoulder,Vector3 pole,out Vector3 elbow)
        {
            Vector3 toTarget = target - shoulder;
            float rawDistance = toTarget.magnitude;
            Vector3 direction = rawDistance > 0.000001f ? toTarget / rawDistance : armCamera.transform.forward;
            float minimumReach = Mathf.Abs(upperLength - forearmLength) + 0.000001f;
            float maximumReach = upperLength + forearmLength - 0.000001f;
            float distance = Mathf.Clamp(rawDistance, minimumReach, maximumReach);
            target = shoulder + direction * distance;
            pole -= direction * Vector3.Dot(pole, direction);
            if (pole.sqrMagnitude < 0.000001f) pole = Vector3.Cross(direction, armCamera.transform.forward);
            if (pole.sqrMagnitude < 0.000001f) pole = Vector3.Cross(direction, armCamera.transform.up);
            pole.Normalize();
            float along = Mathf.Clamp((upperLength * upperLength + distance * distance - forearmLength * forearmLength)
                / (2f * distance), -upperLength, upperLength);
            elbow = shoulder + direction * along + pole * Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
            return Mathf.Abs(distance - rawDistance) > 0.00001f;
        }

        public bool AdvanceUniversalTarget(float deltaTime,Vector3 referenceHand,Hand3DPoseComposition.Frame pose,out Vector3 target)
        {
            bool moving=AdvanceTarget(deltaTime,referenceHand,out target);
            if(HasContact){target=contactTarget-(pose.worldPositions[pose.Index(contactPoint)]-pose.worldPositions[pose.Index(hand)]);return true;}
            return moving;
        }
        public bool SolveUniversal(Hand3DPoseComposition.Frame pose,Vector3 target,Vector3 referenceElbow)
        {
            int u=pose.Index(upperArm),f=pose.Index(forearm),h=pose.Index(hand);Vector3 shoulder=pose.worldPositions[u];
            Vector3 pole=elbowHint!=null?elbowHint.position-shoulder:referenceElbow-shoulder;
            LastUniversalRequestedTarget=target;
            bool limited=CalculateElbow(ref target,shoulder,pole,out Vector3 elbow);
            LastUniversalFeasibleTarget=target;LastUniversalTargetLimited=limited;
            pose.SetWorldRotation(u,Quaternion.FromToRotation(pose.worldPositions[f]-shoulder,elbow-shoulder)*pose.worldRotations[u]);
            pose.SetWorldRotation(f,Quaternion.FromToRotation(pose.worldPositions[h]-pose.worldPositions[f],target-pose.worldPositions[f])*pose.worldRotations[f]);
            LastUniversalReachError=Vector3.Distance(pose.worldPositions[h],target);
            if(!HasContact)AcceptReachedTarget(limited,pose.worldPositions[h]);
            return limited;
        }

        private void RestoreRestPose()
        {
            upperArm.localRotation = upperRotation;
            forearm.localRotation = forearmRotation;
            forearm.localPosition = forearmPosition;
            hand.localPosition = handPosition;
        }

        private void OnDisable()
        {
            contactPoint = null;
            returningFromContact = false;
            bool universal=rig!=null && rig.UniversalActive;
            if(universal)rig.RestoreAuthoredPose();
            if (!universal && initialized && upperArm != null && forearm != null && hand != null) RestoreRestPose();
            if (!universal && initialized)
            {
                transform.localPosition = originalModelPosition;
                transform.localRotation = originalModelRotation;
            }
            initialized = controlling = returning = false;
        }

        private void OnValidate()
        {
            referenceResolution = new Vector2(Mathf.Max(1f, referenceResolution.x), Mathf.Max(1f, referenceResolution.y));
            horizontalScreenLimits = new Vector2(Mathf.Clamp01(horizontalScreenLimits.x), Mathf.Clamp01(horizontalScreenLimits.y));
            verticalScreenLimits = new Vector2(Mathf.Clamp01(verticalScreenLimits.x), Mathf.Clamp01(verticalScreenLimits.y));
            horizontalScreenLimits.y = Mathf.Max(horizontalScreenLimits.x, horizontalScreenLimits.y);
            verticalScreenLimits.y = Mathf.Max(verticalScreenLimits.x, verticalScreenLimits.y);
        }
    }
}

