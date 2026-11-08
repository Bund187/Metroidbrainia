using UnityEngine;

namespace Metroidbrainia.Hand3DExperiment
{
    // Local to one experimental hand. Does not select targets or own movement/bones.
    [DisallowMultipleComponent]
    public sealed class Hand3DInteractionCoordinator : MonoBehaviour
    {
        private MonoBehaviour owner;
        public bool IsOccupied => owner != null;
        public bool CanAcquire(MonoBehaviour requester) => owner == requester || owner == null && Ready;
        private bool Ready => !TryGetComponent<Hand3DInteractionPose>(out var pose) || pose.Weight <= 0.001f;
        public bool Owns(MonoBehaviour requester) => owner == requester;
        public bool TryAcquire(MonoBehaviour requester)
        {
            if (requester == null || !ExperimentScope.IsAllowed(this) || !CanAcquire(requester)) return false;
            owner = requester;
            return true;
        }
        public void Release(MonoBehaviour requester) { if (owner == requester) owner = null; }
    }
}
