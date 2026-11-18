using System.Collections.Generic;
using System.Reflection;
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
        private bool held, releasing, rendererWasEnabled, colliderWasEnabled;
        private bool automatic;
        // The shared 2D document exposes style but no public content API. Read its
        // serialized fields at discovery/pickup boundaries, with cached field lookups.
        private const BindingFlags SourceFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo SourceRenderer = typeof(DocumentHandInteractable).GetField("worldRenderer", SourceFlags);
        private static readonly FieldInfo SourceCollider = typeof(DocumentHandInteractable).GetField("worldCollider", SourceFlags);
        private static readonly FieldInfo SourceSprite = typeof(DocumentHandInteractable).GetField("heldSprite", SourceFlags);
        private static readonly FieldInfo SourceText = typeof(DocumentHandInteractable).GetField("localizedText", SourceFlags);
        private static readonly FieldInfo SourceReach = typeof(DocumentHandInteractable).GetField("maxInteractionDistance", SourceFlags);
        private static readonly FieldInfo SourceEvent = typeof(DocumentHandInteractable).GetField("onPickedUp", SourceFlags);
        public DocumentHandInteractable Document => document;
        public Vector3 ContactPosition => document.InteractionPoint.position;
        public bool IsHeld => held && display != null && display.IsShowing(this);
        public float MaximumDistance => maxInteractionDistance;
        public Bounds WorldBounds => worldRenderer != null ? worldRenderer.bounds : new Bounds(transform.position, Vector3.zero);
        internal Collider DiagnosticCollider => worldCollider;
        internal bool DebugPickup { get; set; }
        internal string PickupDiagnostic { get; private set; }
        internal string ReadinessDiagnostic => !isActiveAndEnabled ? "binding disabled" : document == null ? "missing document"
            : !document.isActiveAndEnabled ? "document disabled" : held ? "already held" : releasing ? "return animation active"
            : worldRenderer == null ? "missing renderer" : !worldRenderer.enabled ? "renderer disabled"
            : worldCollider == null ? "missing collider" : !worldCollider.enabled ? "collider disabled"
            : heldSprite == null ? "missing sprite" : "ready";
        public bool IsReady => isActiveAndEnabled && document != null && document.isActiveAndEnabled
            && !held && !releasing && worldRenderer != null && worldRenderer.enabled
            && worldCollider != null && worldCollider.enabled && heldSprite != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => active.Clear();
        internal void ConfigureAutomatic(DocumentHandInteractable source)
        {
            document = source;
            automatic = true;
            RefreshAutomaticData();
            if (isActiveAndEnabled && ExperimentScope.IsAllowed(this)) active.Add(this);
        }

        internal void RefreshAutomaticData()
        {
            if (!automatic || document == null || held || releasing) return;
            worldRenderer = (MeshRenderer)SourceRenderer.GetValue(document);
            worldCollider = (Collider)SourceCollider.GetValue(document);
            heldSprite = (Sprite)SourceSprite.GetValue(document);
            localizedText = (LocalizedString)SourceText.GetValue(document);
            maxInteractionDistance = (float)SourceReach.GetValue(document);
            // Share the original event; do not copy listeners or invoke the 2D pickup route.
            onPickedUp = (UnityEvent)SourceEvent.GetValue(document);
        }

        private void OnEnable() { if (ExperimentScope.IsAllowed(this)) active.Add(this); }
        private void OnDisable() { EndHold(true); active.Remove(this); }
        private void OnDestroy() { EndHold(true); active.Remove(this); }
        public static void Collect(Scene scene, List<Hand3DDocumentBinding> result)
        {
            result.Clear();
            foreach (var item in active)
                if (item != null && item.gameObject.scene == scene && item.isActiveAndEnabled
                    && item.document != null && item.document.isActiveAndEnabled) result.Add(item);
        }

        public bool BeginHold(Hand3DDocumentDisplay target)
        {
            RefreshAutomaticData();
            if (!IsReady || target == null || !target.TryShow(this, heldSprite, localizedText))
            {
                if (DebugPickup) PickupDiagnostic = !IsReady ? ReadinessDiagnostic : target == null ? "missing display" : "display rejected: " + target.DiagnosticRejection;
                return false;
            }
            display = target;
            rendererWasEnabled = worldRenderer.enabled;
            colliderWasEnabled = worldCollider.enabled;
            held = true;
            worldRenderer.enabled = worldCollider.enabled = false;
            if (DebugPickup) PickupDiagnostic = onPickedUp != null ? "onPickedUp dispatched" : "onPickedUp is null";
            onPickedUp?.Invoke();
            return IsHeld && document != null && document.isActiveAndEnabled && isActiveAndEnabled;
        }

        public void EndHold(bool immediate = false)
        {
            if (!held && !releasing) return;
            held = false;
            if (immediate || display == null)
            {
                if (display != null) display.Hide(this);
                FinishReturn();
            }
            else if (!releasing)
            {
                releasing = true;
                display.Release(this, FinishReturn);
            }
        }

        private void FinishReturn()
        {
            releasing = false;
            display = null;
            if (worldRenderer != null) worldRenderer.enabled = rendererWasEnabled;
            if (worldCollider != null) worldCollider.enabled = colliderWasEnabled;
            // The world Transform is never moved or reparented, so its authored placement is intact.
        }
    }
}
