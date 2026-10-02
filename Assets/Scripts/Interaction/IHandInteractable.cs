using UnityEngine;

namespace Metroidbrainia
{
    public interface IHandInteractable
    {
        Transform InteractionPoint { get; }
        bool TryBeginInteraction(Camera playerCamera, Collider hitCollider);
        // Return false when complete or no longer valid. Movement is already in screen pixels.
        bool UpdateInteraction(Camera playerCamera, Vector2 screenDisplacement);
        // Also called when an unsuccessful contact ends, so feedback can reset on exit.
        void EndInteraction();
    }
}
