using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.SceneManagement;

namespace Metroidbrainia.Hand3DExperiment
{
    // Instance-only bridge for the sprite controller's serialized document data.
    [DisallowMultipleComponent]
    public sealed class Hand3DDocumentBinding : MonoBehaviour
    {
        [SerializeField] private DocumentHandInteractable document;
        [SerializeField] private MeshRenderer worldRenderer;
        [SerializeField] private Collider worldCollider;
        [SerializeField] private Sprite heldSprite;
        [SerializeField] private LocalizedString localizedText = new LocalizedString();
        [SerializeField] private float maxInteractionDistance = 1.5f;
        [SerializeField] private UnityEvent onPickedUp = new UnityEvent();
        private static readonly HashSet<Hand3DDocumentBinding> active = new HashSet<Hand3DDocumentBinding>();
        private Hand3DDocumentDisplay display;
        private bool held, rendererWasEnabled, colliderWasEnabled;
        public DocumentHandInteractable Document => document;
        public Vector3 ContactPosition => document.InteractionPoint.position;
        public bool IsHeld => held && display != null && display.IsShowing(this);
        public float MaximumDistance => maxInteractionDistance;
        public bool IsReady => isActiveAndEnabled && document != null && document.isActiveAndEnabled
            && !held && worldRenderer != null && worldRenderer.enabled
            && worldCollider != null && worldCollider.enabled && heldSprite != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => active.Clear();
        private void OnEnable() { if (ExperimentScope.IsAllowed(this)) active.Add(this); }
        private void OnDisable() { EndHold(); active.Remove(this); }
        private void OnDestroy() { EndHold(); active.Remove(this); }
        public static void Collect(Scene scene, List<Hand3DDocumentBinding> result)
        {
            result.Clear();
            foreach (var item in active)
                if (item != null && item.gameObject.scene == scene && item.isActiveAndEnabled
                    && item.document != null && item.document.isActiveAndEnabled) result.Add(item);
        }

        public bool BeginHold(Hand3DDocumentDisplay target)
        {
            if (!IsReady || target == null || !target.TryShow(this, heldSprite, localizedText)) return false;
            display = target;
            rendererWasEnabled = worldRenderer.enabled;
            colliderWasEnabled = worldCollider.enabled;
            held = true;
            worldRenderer.enabled = worldCollider.enabled = false;
            onPickedUp?.Invoke();
            return IsHeld && document != null && document.isActiveAndEnabled && isActiveAndEnabled;
        }

        public void EndHold()
        {
            if (!held) return;
            held = false;
            if (display != null) display.Hide(this);
            display = null;
            if (worldRenderer != null) worldRenderer.enabled = rendererWasEnabled;
            if (worldCollider != null) worldCollider.enabled = colliderWasEnabled;
            // The world Transform is never moved or reparented, so its authored placement is intact.
        }
    }
}
