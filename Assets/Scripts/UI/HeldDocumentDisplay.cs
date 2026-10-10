using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace Metroidbrainia
{
    // Follow after ArmView.LateUpdate has placed the hand for this frame.
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class HeldDocumentDisplay : MonoBehaviour
    {
        [SerializeField] private RectTransform viewModel;
        [SerializeField] private Image documentImage;
        [SerializeField] private TMP_Text documentText;
        [SerializeField] private ArmView armView;
        [SerializeField] private RectTransform holdPoint;
        [SerializeField] private RectTransform documentGripPoint;
        [SerializeField, Range(0f, 1f)] private float rotationFollow = 0.25f;
        [SerializeField, Range(0f, 180f)] private float maxRotation = 12f;
        [SerializeField, Min(0.001f)] private float rotationSmoothTime = 0.08f;

        private DocumentHandInteractable activeDocument;
        private LocalizedString activeText;
        private Quaternion baseDocumentRotation;
        private float rotationOffset;
        private float rotationVelocity;
        private bool visualInitialized;

        public bool IsShowing(DocumentHandInteractable document)
        {
            return ReferenceEquals(activeDocument, document) && isActiveAndEnabled;
        }

        public bool TryShow(DocumentHandInteractable document, Sprite sprite, LocalizedString text)
        {
            // This component lives on the initially inactive HeldDocument, so do not depend on Awake.
            if (!enabled || activeDocument != null || document == null || sprite == null
                || viewModel == null || documentImage == null || armView == null
                || !armView.isActiveAndEnabled || !viewModel.gameObject.activeInHierarchy
                || transform.parent != viewModel || holdPoint == null || holdPoint.parent == null
                || documentGripPoint == null || !holdPoint.IsChildOf(viewModel)
                || !documentGripPoint.IsChildOf(documentImage.transform))
                return false;

            Canvas canvas = viewModel.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                return false;

            if (!visualInitialized)
            {
                baseDocumentRotation = transform.localRotation;
                visualInitialized = true;
            }
            ResetRotation();
            activeDocument = document;
            documentImage.sprite = sprite;
            if (documentText != null)
            {
                documentText.text = string.Empty;
                bool hasText = text != null && !text.IsEmpty;
                documentText.enabled = hasText;
                if (hasText)
                {
                    activeText = text;
                    activeText.StringChanged += UpdateText;
                }
            }
            gameObject.SetActive(true);
            FollowHand();
            return true;
        }

        public void Hide(DocumentHandInteractable document)
        {
            if (!ReferenceEquals(activeDocument, document))
                return;

            ClearDisplay();
            gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (activeDocument == null || !activeDocument.isActiveAndEnabled
                || armView == null || !armView.isActiveAndEnabled || holdPoint == null
                || holdPoint.parent == null || documentGripPoint == null || viewModel == null)
            {
                ReleaseDocument();
                return;
            }
            FollowHand();
        }

        private void FollowHand()
        {
            if (viewModel == null || holdPoint == null || holdPoint.parent == null || documentGripPoint == null)
                return;

            // Measure Hand's visual right axis in the same space as the document's parent.
            Vector3 handRight = viewModel.InverseTransformVector(holdPoint.parent.TransformVector(Vector3.right));
            float handAngle = Mathf.Atan2(handRight.y, handRight.x) * Mathf.Rad2Deg;
            float handDelta = Mathf.DeltaAngle(armView.InitialHandAngle, handAngle);
            float limit = Mathf.Max(0f, maxRotation);
            float desiredOffset = Mathf.Clamp(handDelta * rotationFollow, -limit, limit);
            if (Time.deltaTime > 0f)
            {
                rotationOffset = Mathf.SmoothDampAngle(rotationOffset, desiredOffset, ref rotationVelocity,
                    Mathf.Max(0.001f, rotationSmoothTime), Mathf.Infinity, Time.deltaTime);
                rotationOffset = Mathf.Clamp(Mathf.DeltaAngle(0f, rotationOffset), -limit, limit);
            }
            transform.localRotation = baseDocumentRotation * Quaternion.AngleAxis(rotationOffset, Vector3.forward);

            // Recompute the grip AFTER rotating: the root's pivot need not be the grip point.
            Vector3 holdPosition = viewModel.InverseTransformPoint(holdPoint.position);
            Vector3 gripPosition = viewModel.InverseTransformPoint(documentGripPoint.position);
            Vector3 position = transform.localPosition;
            position.x += holdPosition.x - gripPosition.x;
            position.y += holdPosition.y - gripPosition.y;
            transform.localPosition = position;
        }

        private void ResetRotation()
        {
            rotationOffset = 0f;
            rotationVelocity = 0f;
            if (visualInitialized)
                transform.localRotation = baseDocumentRotation;
        }

        private void UpdateText(string value)
        {
            if (activeDocument != null && documentText != null)
                documentText.text = value;
        }

        private void ClearDisplay()
        {
            ResetRotation();
            if (activeText != null)
                activeText.StringChanged -= UpdateText;
            activeText = null;
            activeDocument = null;
            if (documentImage != null)
                documentImage.sprite = null;
            if (documentText != null)
            {
                documentText.text = string.Empty;
                documentText.enabled = false;
            }
        }

        private void ReleaseDocument()
        {
            DocumentHandInteractable document = activeDocument;
            ClearDisplay();
            if (document != null)
                document.EndInteraction();
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            ReleaseDocument();
        }

        private void OnDestroy()
        {
            ClearDisplay();
        }
    }
}
