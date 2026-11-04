using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;

namespace Metroidbrainia
{
    [DisallowMultipleComponent]
    public sealed class DocumentHandInteractable : MonoBehaviour, IFreeHandInteractable
    {
        [SerializeField] private MeshRenderer worldRenderer;
        [SerializeField] private Collider worldCollider;
        [SerializeField] private HeldDocumentDisplay heldDocumentDisplay;
        [SerializeField] private Sprite heldSprite;
        [SerializeField] private LocalizedString localizedText = new LocalizedString();
        [Header("Text Style")]
        [SerializeField] private TMP_FontAsset documentFont;
        [SerializeField, Min(1f)] private float documentFontSize = 36f;
        [SerializeField] private Color documentTextColor = Color.black;
        [Header("Interaction")]
        [SerializeField, Min(0.01f)] private float maxInteractionDistance = 1.5f;
        [SerializeField] private UnityEvent onPickedUp = new UnityEvent();

        private bool held;
        private bool rendererWasEnabled;
        private bool colliderWasEnabled;

        public Transform InteractionPoint => transform;
        public TMP_FontAsset DocumentFont => documentFont;
        public float DocumentFontSize => documentFontSize;
        public Color DocumentTextColor => documentTextColor;
        public HandInteractionVisual HandVisual => HandInteractionVisual.Hold;
        public bool IsInteractionActive => held && worldRenderer != null && worldCollider != null
            && heldDocumentDisplay != null && heldDocumentDisplay.IsShowing(this);

        private void Reset()
        {
            worldRenderer = GetComponent<MeshRenderer>();
            worldCollider = GetComponent<Collider>();
        }

        public bool TryBeginInteraction(Camera playerCamera, Collider hitCollider)
        {
            if (!isActiveAndEnabled || held || playerCamera == null || worldRenderer == null
                || worldCollider == null || hitCollider != worldCollider || !worldCollider.enabled
                || !worldRenderer.enabled || heldDocumentDisplay == null || heldSprite == null
                || (playerCamera.transform.position - worldCollider.ClosestPoint(playerCamera.transform.position))
                    .sqrMagnitude > maxInteractionDistance * maxInteractionDistance)
                return false;

            if (!heldDocumentDisplay.TryShow(this, heldSprite, localizedText))
                return false;

            rendererWasEnabled = worldRenderer.enabled;
            colliderWasEnabled = worldCollider.enabled;
            held = true;
            worldRenderer.enabled = false;
            worldCollider.enabled = false;
            onPickedUp?.Invoke();
            return true;
        }

        public bool UpdateInteraction(Camera playerCamera, Vector2 screenDisplacement)
        {
            // Free-hand interactions are checked through IsInteractionActive; input stays in ArmView.
            return IsInteractionActive;
        }

        public void EndInteraction()
        {
            if (!held)
                return;

            held = false;
            if (heldDocumentDisplay != null)
                heldDocumentDisplay.Hide(this);
            if (worldRenderer != null)
                worldRenderer.enabled = rendererWasEnabled;
            if (worldCollider != null)
                worldCollider.enabled = colliderWasEnabled;
        }

        private void OnDisable()
        {
            EndInteraction();
        }

        private void OnDestroy()
        {
            EndInteraction();
        }
    }
}
