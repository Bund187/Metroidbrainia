using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace Metroidbrainia
{
    [DisallowMultipleComponent]
    public sealed class MapDiscoveryNodeView : MonoBehaviour
    {
        [SerializeField] private MapDiscoveryDefinition discoveryDefinition;
        [SerializeField] private RectTransform nodeRect;
        [SerializeField] private CanvasGroup nodeCanvasGroup;
        [SerializeField] private Button interactionButton;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text summaryText;
        [SerializeField] private CanvasGroup summaryCanvasGroup;
        [SerializeField] private Button roomLinkButton;
        [SerializeField] private CanvasGroup roomLinkCanvasGroup;
        [SerializeField] private GameObject resolvedIndicator;
        [SerializeField, Min(1f)] private float collapsedHeight = 210f;
        [SerializeField, Min(1f)] private float expandedHeight = 350f;

        private MapEntryState visualState;
        private bool initialized;
        private bool interactionBlocked;
        private bool expanded;
        private bool roomLinkAvailable;
        private float expansion;
        private Coroutine selectionRoutine;
        private LocalizedString boundTitle;
        private LocalizedString boundSummary;

        public MapDiscoveryDefinition DiscoveryDefinition => discoveryDefinition;
        public event Action<MapDiscoveryNodeView> Selected;
        public event Action<MapDiscoveryNodeView> RoomLinkRequested;

        public void SetResolved(bool resolved)
        {
            if (resolvedIndicator != null)
                resolvedIndicator.SetActive(discoveryDefinition != null
                    && discoveryDefinition.Type == MapDiscoveryType.Question && resolved);
        }

        public bool Initialize()
        {
            if (initialized)
                return true;
            if (nodeRect == null)
                nodeRect = GetComponent<RectTransform>();
            if (nodeCanvasGroup == null)
                nodeCanvasGroup = GetComponent<CanvasGroup>();
            if (discoveryDefinition == null || nodeRect == null || nodeCanvasGroup == null
                || interactionButton == null || titleText == null || summaryText == null
                || summaryCanvasGroup == null || roomLinkButton == null || roomLinkCanvasGroup == null)
            {
                Debug.LogError("Assign the discovery, root RectTransform/CanvasGroup, HitArea Button, localized texts and summary/room-link controls.", this);
                return false;
            }
            initialized = true;
            ApplyExpansion(0f);
            return true;
        }

        private void OnEnable()
        {
            if (!Initialize())
                return;
            interactionButton.onClick.RemoveListener(OnSelected);
            interactionButton.onClick.AddListener(OnSelected);
            roomLinkButton.onClick.RemoveListener(OnRoomLink);
            roomLinkButton.onClick.AddListener(OnRoomLink);
            BindText();
            ApplyState(visualState);
        }

        public void ApplyState(MapEntryState state)
        {
            if (!Initialize())
                return;
            visualState = state;
            nodeCanvasGroup.alpha = state == MapEntryState.Revealed ? 1f : 0f;
            if (state != MapEntryState.Revealed)
                SetExpanded(false, 0f);
            ApplyInteraction();
        }

        public void SetRevealProgress(float progress)
        {
            if (!Initialize() || visualState == MapEntryState.Revealed)
                return;
            visualState = MapEntryState.PendingReveal;
            nodeCanvasGroup.alpha = Mathf.Clamp01(progress);
            ApplyInteraction();
        }

        public void SetInteractionBlocked(bool blocked)
        {
            interactionBlocked = blocked;
            if (Initialize())
                ApplyInteraction();
        }

        public void SetRoomLinkAvailable(bool available)
        {
            roomLinkAvailable = available;
            if (Initialize())
                ApplyInteraction();
        }

        public void SetExpanded(bool value, float duration)
        {
            if (!Initialize())
                return;
            if (selectionRoutine != null)
                StopCoroutine(selectionRoutine);
            selectionRoutine = null;
            expanded = value && visualState == MapEntryState.Revealed;
            float target = expanded ? 1f : 0f;
            if (!isActiveAndEnabled || duration <= 0f || Mathf.Approximately(expansion, target))
                ApplyExpansion(target);
            else
                selectionRoutine = StartCoroutine(AnimateExpansion(expansion, target, duration));
        }

        private IEnumerator AnimateExpansion(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                ApplyExpansion(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / duration)));
                yield return null;
            }
            ApplyExpansion(to);
            selectionRoutine = null;
        }

        private void ApplyExpansion(float progress)
        {
            expansion = Mathf.Clamp01(progress);
            if (nodeRect != null)
            {
                // Only the root height changes. The top pivot and all child layouts remain authored in Unity.
                Vector2 size = nodeRect.sizeDelta;
                size.y = Mathf.Lerp(collapsedHeight, expandedHeight, expansion);
                nodeRect.sizeDelta = size;
            }
            ApplyInteraction();
        }

        private void ApplyInteraction()
        {
            bool canSelect = visualState == MapEntryState.Revealed && !interactionBlocked;
            if (nodeCanvasGroup != null)
            {
                nodeCanvasGroup.interactable = canSelect;
                nodeCanvasGroup.blocksRaycasts = canSelect;
            }
            if (interactionButton != null)
                interactionButton.interactable = canSelect;
            if (summaryCanvasGroup != null)
            {
                summaryCanvasGroup.alpha = expansion;
                summaryCanvasGroup.interactable = false;
                summaryCanvasGroup.blocksRaycasts = false;
            }
            bool canLink = canSelect && roomLinkAvailable && expanded && expansion >= 0.999f;
            if (roomLinkCanvasGroup != null)
            {
                roomLinkCanvasGroup.alpha = roomLinkAvailable ? expansion : 0f;
                roomLinkCanvasGroup.interactable = canLink;
                roomLinkCanvasGroup.blocksRaycasts = canLink;
            }
            if (roomLinkButton != null)
                roomLinkButton.interactable = canLink;
        }

        private void OnSelected()
        {
            if (isActiveAndEnabled && visualState == MapEntryState.Revealed && !interactionBlocked)
                Selected?.Invoke(this);
        }

        private void OnRoomLink()
        {
            if (isActiveAndEnabled && visualState == MapEntryState.Revealed && !interactionBlocked
                && roomLinkAvailable && expanded && expansion >= 0.999f)
                RoomLinkRequested?.Invoke(this);
        }

        private void BindText()
        {
            UnbindText();
            titleText.text = string.Empty;
            summaryText.text = string.Empty;
            if (discoveryDefinition.DisplayName != null && !discoveryDefinition.DisplayName.IsEmpty)
            {
                boundTitle = discoveryDefinition.DisplayName;
                boundTitle.StringChanged += UpdateTitle;
            }
            if (discoveryDefinition.Summary != null && !discoveryDefinition.Summary.IsEmpty)
            {
                boundSummary = discoveryDefinition.Summary;
                boundSummary.StringChanged += UpdateSummary;
            }
        }

        private void UpdateTitle(string value)
        {
            if (titleText != null)
                titleText.text = value;
        }

        private void UpdateSummary(string value)
        {
            if (summaryText != null)
                summaryText.text = value;
        }

        private void UnbindText()
        {
            if (boundTitle != null)
                boundTitle.StringChanged -= UpdateTitle;
            if (boundSummary != null)
                boundSummary.StringChanged -= UpdateSummary;
            boundTitle = null;
            boundSummary = null;
        }

        private void OnDisable()
        {
            if (interactionButton != null)
                interactionButton.onClick.RemoveListener(OnSelected);
            if (roomLinkButton != null)
                roomLinkButton.onClick.RemoveListener(OnRoomLink);
            UnbindText();
            if (selectionRoutine != null)
                StopCoroutine(selectionRoutine);
            selectionRoutine = null;
            expanded = false;
            if (initialized)
                ApplyExpansion(0f);
        }

        private void OnValidate()
        {
            collapsedHeight = Mathf.Max(1f, collapsedHeight);
            expandedHeight = Mathf.Max(collapsedHeight, expandedHeight);
        }
    }
}
