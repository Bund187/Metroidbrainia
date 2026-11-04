using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Metroidbrainia
{
    [DisallowMultipleComponent]
    public sealed class MapRoomView : MonoBehaviour
    {
        [SerializeField] private MapRoomDefinition roomDefinition;
        [SerializeField] private Image roomImage;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Button[] interactionButtons = new Button[0];
        [FormerlySerializedAs("button"), FormerlySerializedAs("interactionButton")]
        [SerializeField, HideInInspector] private Button legacyInteractionButton;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color selectedColor = new Color(1f, 0.8f, 0.3f, 1f);

        private MapEntryState visualState;
        private bool interactionBlocked;
        private bool initialized;
        private bool isSelected;
        private Sprite baseSprite;

        public MapRoomDefinition RoomDefinition => roomDefinition;
        public event Action<MapRoomView> Selected;

        public bool Initialize()
        {
            if (initialized)
                return true;
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            if (roomImage == null)
                roomImage = GetComponent<Image>();
            if ((interactionButtons == null || interactionButtons.Length == 0) && legacyInteractionButton != null)
                interactionButtons = new[] { legacyInteractionButton };
            bool hasButton = false;
            if (interactionButtons != null)
            {
                foreach (Button button in interactionButtons)
                    hasButton |= button != null;
            }
            if (roomDefinition == null || canvasGroup == null || roomImage == null || !hasButton)
            {
                Debug.LogError("Assign the room definition, drawing Image, parent CanvasGroup and HitArea Button.", this);
                return false;
            }
            // HitArea is interaction-only. Its disabled tint must not fade the room drawing.
            foreach (Button button in interactionButtons)
            {
                if (button != null && button.targetGraphic == roomImage
                    && button.transition == Selectable.Transition.ColorTint)
                {
                    button.transition = Selectable.Transition.None;
                    roomImage.CrossFadeColor(Color.white, 0f, true, true);
                }
            }
            baseSprite = roomImage.sprite;
            initialized = true;
            return true;
        }

        public void ApplyVariant(MapRoomVariantDefinition variant)
        {
            if (Initialize() && roomImage != null)
                roomImage.sprite = variant != null && variant.MapSprite != null ? variant.MapSprite : baseSprite;
        }

        private void OnEnable()
        {
            if (!Initialize())
                return;
            foreach (Button button in interactionButtons)
            {
                if (button == null)
                    continue;
                button.onClick.RemoveListener(OnClicked);
                button.onClick.AddListener(OnClicked);
            }
            ApplyState(visualState);
        }

        public void ApplyState(MapEntryState state)
        {
            if (!Initialize())
                return;
            visualState = state;
            if (state != MapEntryState.Revealed)
                isSelected = false;
            canvasGroup.alpha = state == MapEntryState.Revealed ? 1f : 0f;
            ApplyInteraction();
            ApplyColor();
        }

        public void SetRevealProgress(float progress)
        {
            if (!Initialize() || visualState == MapEntryState.Revealed)
                return;
            visualState = MapEntryState.PendingReveal;
            isSelected = false;
            canvasGroup.alpha = Mathf.Clamp01(progress);
            ApplyInteraction();
            ApplyColor();
        }

        public void SetSelected(bool selected)
        {
            if (!Initialize())
                return;
            isSelected = selected && visualState == MapEntryState.Revealed;
            if (isSelected)
                transform.SetAsLastSibling();
            ApplyColor();
        }

        private void ApplyColor()
        {
            roomImage.color = isSelected ? selectedColor : normalColor;
        }

        public void SetInteractionBlocked(bool blocked)
        {
            interactionBlocked = blocked;
            if (Initialize())
                ApplyInteraction();
        }

        private void ApplyInteraction()
        {
            bool canSelect = visualState == MapEntryState.Revealed && !interactionBlocked;
            canvasGroup.interactable = canSelect;
            canvasGroup.blocksRaycasts = canSelect;
            foreach (Button button in interactionButtons)
            {
                if (button != null)
                    button.interactable = canSelect;
            }
        }

        private void OnClicked()
        {
            if (isActiveAndEnabled && visualState == MapEntryState.Revealed && !interactionBlocked)
                Selected?.Invoke(this);
        }

        private void OnDisable()
        {
            if (interactionButtons == null)
                return;
            foreach (Button button in interactionButtons)
            {
                if (button != null)
                    button.onClick.RemoveListener(OnClicked);
            }
        }
    }
}
