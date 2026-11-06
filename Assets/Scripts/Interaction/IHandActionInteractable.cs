using UnityEngine;

namespace Metroidbrainia
{
    // Explicit pose actions do not capture the hand like continuous door interactions.
    public interface IHandActionInteractable
    {
        bool TryPerformHandAction(ArmPose pose, Camera playerCamera, RaycastHit hit);
    }
}
