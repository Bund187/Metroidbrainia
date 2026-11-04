using System;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace Metroidbrainia
{
    [DisallowMultipleComponent]
    public sealed class MapRoomDiscoveryMarker : MonoBehaviour
    {
        [SerializeField] private MapDiscoveryDefinition discoveryDefinition;
        [SerializeField] private Image iconImage;
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text labelText;

        private bool known;
        private bool interactionBlocked;
        private bool labelOpen;
        private LocalizedString boundLabel;
        private CanvasGroup labelCanvasGroup;
        private bool labelGroupCached;

        public MapDiscoveryDefinition DiscoveryDefinition => discoveryDefinition;
        public event Action<MapRoomDiscoveryMarker> Clicked;

        public bool Initialize()
        {
            if (iconImage == null)
                iconImage = GetComponent<Image>();
            if (button == null)
                button = GetComponent<Button>();
            if (discoveryDefinition == null || iconImage == null || button == null || labelText == null)
            {
                Debug.LogError("Assign the discovery, marker Image/Button and its IconLabel TMP.", this);
                return false;
            }
            if (!labelGroupCached)
            {
                labelCanvasGroup = labelText.GetComponent<CanvasGroup>();
                labelGroupCached = true;
            }
            return true;
        }

        private void OnEnable()
        {
            if (!Initialize())
                return;
            button.onClick.RemoveListener(OnClicked);
            button.onClick.AddListener(OnClicked);
            SetLabelOpen(false);
            ApplyVisibility();
        }

        public void Configure(bool isKnown, Sprite icon, bool blocked)
        {
            if (!Initialize())
                return;
            known = isKnown;
            interactionBlocked = blocked;
            iconImage.sprite = icon;
            if (!known)
                SetLabelOpen(false);
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            if (iconImage != null)
                iconImage.enabled = known && isActiveAndEnabled;
            if (button != null)
                button.interactable = known && !interactionBlocked && isActiveAndEnabled;
            if (labelText != null)
                labelText.enabled = labelOpen && known && isActiveAndEnabled;
        }

        public void SetLabelOpen(bool open)
        {
            if (boundLabel != null)
                boundLabel.StringChanged -= UpdateLabel;
            boundLabel = null;
            labelOpen = open && known && isActiveAndEnabled;
            if (labelText != null)
            {
                if (labelOpen)
                    labelText.gameObject.SetActive(true);
                labelText.text = string.Empty;
                labelText.enabled = labelOpen;
            }
            // Existing labels may have an authored CanvasGroup at alpha zero, independently of TMP.enabled.
            if (labelCanvasGroup != null)
            {
                labelCanvasGroup.alpha = labelOpen ? 1f : 0f;
                labelCanvasGroup.interactable = false;
                labelCanvasGroup.blocksRaycasts = false;
            }
            if (labelOpen && discoveryDefinition != null && discoveryDefinition.DisplayName != null
                && !discoveryDefinition.DisplayName.IsEmpty)
            {
                boundLabel = discoveryDefinition.DisplayName;
                boundLabel.StringChanged += UpdateLabel;
            }
        }

        private void UpdateLabel(string value)
        {
            if (labelText != null)
                labelText.text = value;
        }

        private void OnClicked()
        {
            if (known && !interactionBlocked && isActiveAndEnabled)
                Clicked?.Invoke(this);
        }

        private void OnDisable()
        {
            if (button != null)
                button.onClick.RemoveListener(OnClicked);
            SetLabelOpen(false);
        }
    }
}
