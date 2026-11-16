using System;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Metroidbrainia.Hand3DExperiment
{
    // Full-resolution paper/text; the existing pixel texture is composed behind it and through a thumb stencil in front.
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
        [SerializeField] private Hand3DThumbOverlay thumbOverlay;
        [SerializeField, Range(0.1f, 0.95f)] private float heldDocumentScreenHeight = 0.58f;
        [Tooltip("Pixels at 1080p, relative to the projected DocumentHoldPoint; positive X is screen-right.")]
        [SerializeField, FormerlySerializedAs("screenOffset")] private Vector2 documentHoldOffset;
        [SerializeField, FormerlySerializedAs("rotationOffset")] private Vector3 documentHoldRotation;
        [SerializeField, FormerlySerializedAs("attachmentDuration"), Min(0f)] private float pickupTransitionDuration = 0.16f;
        [SerializeField, Min(0f)] private float releaseTransitionDuration = 0.18f;
        [SerializeField, HideInInspector] private float visualScale = 1f; // Retained serialized legacy data; screen height now determines size.
        private Hand3DDocumentBinding active;
        private LocalizedString activeText;
        private Quaternion baseRotation, fromRotation;
        private Vector3 baseScale, fromScale, fromGrip;
        private float elapsed;
        private TMP_FontAsset defaultFont;
        private Material defaultMaterial;
        private bool initialized, releasing;
        private Action releaseCompleted;
        private int restSibling;
        public bool IsShowing(Hand3DDocumentBinding value) => active == value && !releasing && isActiveAndEnabled
            && visualRoot != null && visualRoot.gameObject.activeInHierarchy;
        public bool IsReleasing => releasing;

        public bool TryShow(Hand3DDocumentBinding value, Sprite sprite, LocalizedString text)
        {
            if (releasing) FinishRelease();
            if (!ExperimentScope.IsAllowed(this) || !isActiveAndEnabled || active != null || value == null || sprite == null
                || movement == null || documentHoldPoint == null || visualRoot == null || paperGrip == null
                || documentImage == null || documentText == null || !paperGrip.IsChildOf(visualRoot)) return false;
            var canvas = visualRoot.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay) return false;
            if (armDisplay == null) armDisplay = Hand3DScreenProjection.FindDisplay(movement.ArmCamera);
            if (armDisplay == null || visualRoot.parent != armDisplay.transform.parent) return false;
            if (!Hand3DScreenProjection.TryWorldToScreen(movement.ArmCamera, armDisplay, documentHoldPoint.position, out _, out _)) return false;
            if (!initialized)
            {
                baseRotation = visualRoot.localRotation;
                baseScale = visualRoot.localScale;
                restSibling = visualRoot.GetSiblingIndex();
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
            visualRoot.gameObject.SetActive(true);
            visualRoot.localScale = baseScale;
            visualRoot.localRotation = baseRotation;
            Canvas.ForceUpdateCanvases();
            // Preserve other UI siblings: base arm, paper/text, masked copy of the same arm texture.
            visualRoot.SetAsLastSibling();
            visualRoot.SetSiblingIndex(armDisplay.transform.GetSiblingIndex() + 1);
            if (thumbOverlay != null)
            {
                thumbOverlay.gameObject.SetActive(true);
                thumbOverlay.transform.SetAsLastSibling();
                thumbOverlay.transform.SetSiblingIndex(visualRoot.GetSiblingIndex() + 1);
                thumbOverlay.RefreshMask();
            }
            else Debug.LogWarning("[Hand3D Documents] Prepare Document Visuals to configure the thumb stencil. Paper is drawn above the base hand.", this);
            SourceFrame(out fromGrip, out fromScale, out fromRotation);
            ApplyFrame(fromGrip, fromScale, fromRotation);
            return true;
        }

        private void UpdateText(string value) { if (active != null && documentText != null) documentText.text = value; }
        private void LateUpdate() => Tick(Time.deltaTime);

        private void Tick(float deltaTime)
        {
            if (releasing)
            {
                if (active == null || !active.isActiveAndEnabled || active.Document == null || !active.Document.isActiveAndEnabled)
                { FinishRelease(); return; }
                elapsed += deltaTime;
                float t = Ease(elapsed, releaseTransitionDuration);
                SourceFrame(out Vector3 grip, out Vector3 scale, out Quaternion rotation);
                ApplyFrame(Vector3.Lerp(fromGrip, grip, t), Vector3.Lerp(fromScale, scale, t), Quaternion.Slerp(fromRotation, rotation, t));
                if (t >= 1f) FinishRelease();
                return;
            }
            if (active == null) return;
            if (!active.isActiveAndEnabled || active.Document == null || !active.Document.isActiveAndEnabled
                || movement == null || !movement.isActiveAndEnabled || documentHoldPoint == null)
            { active.EndHold(true); return; }
            if (!Hand3DScreenProjection.TryWorldToScreen(movement.ArmCamera, armDisplay, documentHoldPoint.position, out Vector2 screen, out _)) return;
            elapsed += deltaTime;
            float blend = Ease(elapsed, pickupTransitionDuration);
            Vector3 target = screen + documentHoldOffset * Screen.height / 1080f;
            Quaternion heldRotation = baseRotation * Quaternion.Euler(documentHoldRotation);
            ApplyFrame(Vector3.Lerp(fromGrip, target, blend), Vector3.Lerp(fromScale, ScaleForHeight(Screen.height * heldDocumentScreenHeight), blend), Quaternion.Slerp(fromRotation, heldRotation, blend));
        }

        private static float Ease(float time, float duration) => duration > 0f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / duration)) : 1f;

        private Vector3 ScaleForHeight(float pixels)
        {
            // Measure the paper itself, including preserveAspect/CanvasScaler, rather than a fixed world or UI scale.
            Rect drawing = documentImage.GetPixelAdjustedRect();
            float height = drawing.height;
            if (documentImage.preserveAspect && documentImage.sprite != null && drawing.width > 0f)
                height = Mathf.Min(height, drawing.width * documentImage.sprite.rect.height / documentImage.sprite.rect.width);
            Vector3 unit = visualRoot.InverseTransformVector(documentImage.rectTransform.TransformVector(Vector3.up));
            Vector3 screenUnit = visualRoot.parent.TransformVector(Vector3.Scale(baseScale, unit));
            float naturalHeight = height * new Vector2(screenUnit.x, screenUnit.y).magnitude;
            return baseScale * (Mathf.Max(1f, pixels) / Mathf.Max(0.001f, naturalHeight));
        }

        private void SourceFrame(out Vector3 grip, out Vector3 scale, out Quaternion rotation)
        {
            var contact = GetComponent<Hand3DDocumentInteractor>();
            Vector2 center = paperGrip.position;
            float height = Screen.height * 0.12f;
            rotation = baseRotation;
            if (active != null && contact != null)
            {
                contact.TryGetWorldScreen(active.ContactPosition, out center, out _);
                Bounds bounds = active.WorldBounds;
                float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    if (contact.TryGetWorldScreen(corner, out Vector2 pixel, out _)) { minY = Mathf.Min(minY, pixel.y); maxY = Mathf.Max(maxY, pixel.y); }
                }
                if (!float.IsInfinity(minY)) height = Mathf.Max(1f, maxY - minY);
                if (contact.TryGetWorldScreen(active.ContactPosition + active.transform.up * 0.01f, out Vector2 up, out _))
                    rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(up.y - center.y, up.x - center.x) * Mathf.Rad2Deg - 90f);
            }
            scale = ScaleForHeight(height);
            // ContactPosition denotes the world paper's center, not the finger contact on the enlarged UI paper.
            Vector3 offset = visualRoot.InverseTransformVector(paperGrip.position - documentImage.rectTransform.TransformPoint(documentImage.rectTransform.rect.center));
            grip = (Vector3)center + visualRoot.parent.TransformVector(rotation * Vector3.Scale(scale, offset));
        }

        private void ApplyFrame(Vector3 grip, Vector3 scale, Quaternion rotation)
        {
            visualRoot.localScale = scale;
            visualRoot.localRotation = rotation;
            visualRoot.position += grip - paperGrip.position;
            if (thumbOverlay != null && thumbOverlay.gameObject.activeSelf) thumbOverlay.RefreshMask();
        }

        public void Release(Hand3DDocumentBinding value, Action completed)
        {
            if (!ReferenceEquals(active, value)) { completed?.Invoke(); return; }
            if (!isActiveAndEnabled || releaseTransitionDuration <= 0f || value == null || !value.isActiveAndEnabled
                || value.Document == null || !value.Document.isActiveAndEnabled)
            { Hide(value); completed?.Invoke(); return; }
            releasing = true;
            releaseCompleted = completed;
            elapsed = 0f;
            fromGrip = paperGrip.position; fromScale = visualRoot.localScale; fromRotation = visualRoot.localRotation;
        }

        public void Hide(Hand3DDocumentBinding value)
        {
            if (!ReferenceEquals(active, value)) return;
            FinishRelease();
        }

        private void FinishRelease()
        {
            if (activeText != null) activeText.StringChanged -= UpdateText;
            activeText = null;
            active = null;
            releasing = false;
            if (thumbOverlay != null) thumbOverlay.gameObject.SetActive(false);
            if (documentImage != null) documentImage.sprite = null;
            if (documentText != null) documentText.text = string.Empty;
            if (visualRoot != null)
            {
                visualRoot.gameObject.SetActive(false);
                if (initialized)
                {
                    visualRoot.localScale = baseScale;
                    visualRoot.localRotation = baseRotation;
                    visualRoot.SetSiblingIndex(restSibling);
                }
            }
            Action callback = releaseCompleted;
            releaseCompleted = null;
            callback?.Invoke();
        }
        private void OnDisable()
        {
            var previous = active;
            if (previous != null) previous.EndHold(true);
            FinishRelease();
        }
        private void OnDestroy() { if (activeText != null) activeText.StringChanged -= UpdateText; }
    }
}
