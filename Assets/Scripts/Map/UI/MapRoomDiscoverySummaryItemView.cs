using System;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace Metroidbrainia
{
    [DisallowMultipleComponent]
    public sealed class MapRoomDiscoverySummaryItemView : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Button button;

        private MapDiscoveryDefinition discovery;
        private LocalizedString boundTitle;
        private Action<MapDiscoveryDefinition> clicked;

        private bool Initialize()
        {
            if (iconImage == null)
                iconImage = GetComponent<Image>();
            if (button == null)
                button = GetComponent<Button>();
            if (iconImage == null || button == null || titleText == null)
            {
                Debug.LogError("Assign the summary item's icon Image, title TMP and root Button.", this);
                return false;
            }
            return true;
        }

        private void OnEnable()
        {
            if (!Initialize())
                return;
            button.onClick.RemoveListener(OnClicked);
            button.onClick.AddListener(OnClicked);
        }

        public bool Bind(MapDiscoveryDefinition definition, Sprite icon, Action<MapDiscoveryDefinition> onClicked)
        {
            Unbind();
            if (!Initialize() || definition == null)
                return false;
            discovery = definition;
            clicked = onClicked;
            iconImage.sprite = icon;
            button.interactable = true;
            if (definition.DisplayName != null && !definition.DisplayName.IsEmpty)
            {
                boundTitle = definition.DisplayName;
                boundTitle.StringChanged += UpdateTitle;
            }
            return true;
        }

        public void Unbind()
        {
            if (boundTitle != null)
                boundTitle.StringChanged -= UpdateTitle;
            boundTitle = null;
            discovery = null;
            clicked = null;
            if (titleText != null)
                titleText.text = string.Empty;
            if (button != null)
                button.interactable = false;
        }

        private void UpdateTitle(string value)
        {
            if (titleText != null)
                titleText.text = value;
        }

        private void OnClicked()
        {
            if (isActiveAndEnabled && discovery != null && button != null && button.IsInteractable())
                clicked?.Invoke(discovery);
        }

        private void OnDisable()
        {
            if (button != null)
                button.onClick.RemoveListener(OnClicked);
            Unbind();
        }
    }
}
