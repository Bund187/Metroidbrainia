using UnityEngine;

namespace Metroidbrainia.Hand3DExperiment
{
    public interface IHand3DInteractionSession
    {
        void ReleaseInteraction();
        bool ProcessMovement(Vector2 integratedInput, bool usingMouse);
    }

    public enum Hand3DInteractionState { Free, Door, Document, Other, Restoring }

    // Owns the local lease and visual requests, never the object's behavior or skeleton composition.
    [DefaultExecutionOrder(215)]
    [DisallowMultipleComponent]
    public sealed class Hand3DInteractionCoordinator : MonoBehaviour
    {
        private MonoBehaviour owner;
        private Hand3DInteractionState kind;
        private bool restrictsMovement, releasing, initialized;
        private Hand3DInteractionPose pose;
        private Hand3DPoseActions actions;
        private Hand3DArmMovement movement;
        private Hand3DRig rig;
        public event System.Action ArmControlEnded;
        public MonoBehaviour Owner => owner;
        public bool IsOccupied => owner != null;
        public bool IsRestoring { get { Initialize(); return !IsOccupied && pose != null && pose.Weight > 0.001f; } }
        public Hand3DInteractionState State => IsOccupied ? kind : releasing || IsRestoring ? Hand3DInteractionState.Restoring : Hand3DInteractionState.Free;
        public bool IsMovementRestricted => IsOccupied && restrictsMovement;
        public bool CanExecuteGestureAction => isActiveAndEnabled && !IsOccupied && !releasing && !IsRestoring;

        private void Awake() => Initialize();
        private void Initialize()
        {
            if (initialized) return;
            initialized = true;
            pose = GetComponent<Hand3DInteractionPose>();
            actions = GetComponent<Hand3DPoseActions>();
            movement = GetComponent<Hand3DArmMovement>();
            rig = GetComponent<Hand3DRig>();
        }
        public bool Owns(MonoBehaviour requester) => requester != null && owner == requester;
        public bool CanAcquire(MonoBehaviour requester) => requester != null && requester.isActiveAndEnabled
            && requester.gameObject == gameObject && isActiveAndEnabled && !releasing
            && (Owns(requester) || !IsOccupied && !IsRestoring)
            && (pose == null || pose.isActiveAndEnabled) && (movement == null || movement.isActiveAndEnabled)
            && (rig == null || rig.isActiveAndEnabled);

        public bool TryAcquire(MonoBehaviour requester) => TryAcquire(requester, Hand3DInteractionState.Other, false, Quaternion.identity);
        public bool TryAcquire(MonoBehaviour requester, Hand3DInteractionState interaction, bool restrictMovement, Quaternion wristOffset)
        {
            Initialize();
            if (!(requester is IHand3DInteractionSession) || !ExperimentScope.IsAllowed(this) || !CanAcquire(requester)) return false;
            if (Owns(requester)) return true;
            owner = requester;
            kind = interaction;
            restrictsMovement = restrictMovement;
            if (actions != null) actions.CancelAction();
            if (pose != null)
            {
                if (kind == Hand3DInteractionState.Document) pose.SetHandHold(true);
                else if (kind == Hand3DInteractionState.Door) pose.SetGrab(true, wristOffset);
            }
            return true;
        }

        public void Release(MonoBehaviour requester)
        {
            if (ReferenceEquals(owner, null) || !ReferenceEquals(owner, requester)) return;
            var previous = owner as IHand3DInteractionSession;
            var previousKind = kind;
            bool releaseContact = restrictsMovement;
            owner = null;
            restrictsMovement = false;
            releasing = true;
            try { previous?.ReleaseInteraction(); }
            finally
            {
                if (pose != null)
                {
                    if (previousKind == Hand3DInteractionState.Document) pose.SetHandHold(false);
                    else if (previousKind == Hand3DInteractionState.Door) pose.SetGrab(false);
                }
                if (releaseContact && movement != null) movement.ReleaseContact();
                releasing = false;
            }
        }

        public bool ProcessMovement(Vector2 integratedInput, bool usingMouse) => IsMovementRestricted
            && owner is IHand3DInteractionSession session && session.ProcessMovement(integratedInput, usingMouse);
        public void CancelSession() { if (!ReferenceEquals(owner, null)) Release(owner); }
        public void EndArmSession()
        {
            CancelSession();
            // Local control lifecycle notification, including a door attempt whose lease has already ended.
            ArmControlEnded?.Invoke();
        }
        private void LateUpdate()
        {
            if (!ReferenceEquals(owner, null) && (owner == null || !owner.isActiveAndEnabled
                || pose != null && !pose.isActiveAndEnabled || movement != null && !movement.isActiveAndEnabled
                || rig != null && !rig.isActiveAndEnabled)) CancelSession();
        }
        private void OnDisable() => EndArmSession();
    }
}
