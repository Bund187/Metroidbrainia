using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace Metroidbrainia.Hand3DExperiment
{
    // Full-resolution UI, updated after the rig and contact correction, never inside RT_PixelArm.
    [DefaultExecutionOrder(240)]
    [DisallowMultipleComponent]
    public sealed class Hand3DDocumentDisplay : MonoBehaviour
    {
        [SerializeField] private Hand3DArmMovement movement;
        [SerializeField] private Transform documentHoldPoint;
        [SerializeField] private RawImage armDisplay;
        [SerializeField] private RectTransform visualRoot;
        [SerializeField] private RectTransform paperGrip;
        [SerializeField] private Image documentImage;
        [SerializeField] private TMP_Text documentText;
        [Tooltip("Pixels at 1080p, relative to DocumentHoldPoint.")]
        [SerializeField] private Vector2 screenOffset;
        [SerializeField] private Vector3 rotationOffset;
        [SerializeField, Min(0.01f)] private float visualScale = 1f;
        [SerializeField, Min(0f)] private float attachmentDuration = 0.12f;
        private Hand3DDocumentBinding active;
        private LocalizedString activeText;
        private Quaternion baseRotation;
        private Vector3 baseScale;
        private Vector3 snapFrom;
        private float elapsed;
        private TMP_FontAsset defaultFont;
        private Material defaultMaterial;
        private bool initialized;
        public bool IsShowing(Hand3DDocumentBinding value) => active == value && isActiveAndEnabled
            && visualRoot != null && visualRoot.gameObject.activeInHierarchy;

        public bool TryShow(Hand3DDocumentBinding value, Sprite sprite, LocalizedString text)
        {
            if (!ExperimentScope.IsAllowed(this) || !isActiveAndEnabled || active != null || value == null || sprite == null
                || movement == null || documentHoldPoint == null || visualRoot == null || paperGrip == null
                || documentImage == null || documentText == null || !paperGrip.IsChildOf(visualRoot)) return false;
            var canvas = visualRoot.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay) return false;
            if (armDisplay == null) armDisplay = Hand3DScreenProjection.FindDisplay(movement.ArmCamera);
            if (!Hand3DScreenProjection.TryWorldToScreen(movement.ArmCamera, armDisplay, documentHoldPoint.position, out _, out _)) return false;
            if (!initialized)
            {
                baseRotation = visualRoot.localRotation;
                baseScale = visualRoot.localScale;
                defaultFont = documentText.font;
                defaultMaterial = documentText.fontSharedMaterial;
                initialized = true;
            }
            active = value;
            elapsed = 0f;
            documentImage.sprite = sprite;
            documentImage.raycastTarget = documentText.raycastTarget = false;
            documentText.font = value.Document.DocumentFont != null ? value.Document.DocumentFont : defaultFont;
            if (value.Document.DocumentFont == null) documentText.fontSharedMaterial = defaultMaterial;
            documentText.fontSize = value.Document.DocumentFontSize;
            documentText.color = value.Document.DocumentTextColor;
            documentText.text = string.Empty;
            documentText.enabled = text != null && !text.IsEmpty;
            if (documentText.enabled) { activeText = text; activeText.StringChanged += UpdateText; }
            visualRoot.localRotation = baseRotation * Quaternion.Euler(rotationOffset);
            visualRoot.localScale = baseScale * visualScale;
            // Start at the source document projection; only acquisition is smoothed. Tracking has no lag.
            Vector2 source = movement.ArmCamera.WorldToScreenPoint(documentHoldPoint.position);
            var contact = GetComponent<Hand3DDoorInteractor>();
            if (contact != null) contact.TryGetWorldScreen(value.ContactPosition, out source, out _);
            PlaceGrip(source);
            snapFrom = paperGrip.position;
            visualRoot.gameObject.SetActive(true);
            return true;
        }

        private void UpdateText(string value) { if (active != null && documentText != null) documentText.text = value; }

        private void LateUpdate()
        {
            if (active == null) return;
            if (!active.isActiveAndEnabled || active.Document == null || !active.Document.isActiveAndEnabled
                || movement == null || !movement.isActiveAndEnabled || documentHoldPoint == null
                || !Hand3DScreenProjection.TryWorldToScreen(movement.ArmCamera, armDisplay,
                    documentHoldPoint.position, out Vector2 screen, out _))
            { active.EndHold(); return; }
            elapsed += Time.deltaTime;
            float t = attachmentDuration > 0f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / attachmentDuration)) : 1f;
            Vector3 target = screen + screenOffset * Screen.height / 1080f;
            PlaceGrip(Vector3.Lerp(snapFrom, target, t));
        }

        private void PlaceGrip(Vector3 screen)
        {
            // Overlay UI uses screen coordinates; both image and text share this root.
            visualRoot.position += screen - paperGrip.position;
        }

        public void Hide(Hand3DDocumentBinding value)
        {
            if (!ReferenceEquals(active, value)) return;
            if (activeText != null) activeText.StringChanged -= UpdateText;
            activeText = null;
            active = null;
            if (documentImage != null) documentImage.sprite = null;
            if (documentText != null) documentText.text = string.Empty;
            if (visualRoot != null) visualRoot.gameObject.SetActive(false);
        }
        private void OnDisable() { if (active != null) active.EndHold(); }
        private void OnDestroy() { if (activeText != null) activeText.StringChanged -= UpdateText; }
    }
}
