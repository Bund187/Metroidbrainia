using UnityEngine;
using UnityEngine.Localization;

namespace Metroidbrainia
{
    [CreateAssetMenu(menuName = "Metroidbrainia/Map/Room Variant Definition", fileName = "MapRoomVariant")]
    public sealed class MapRoomVariantDefinition : ScriptableObject
    {
        [SerializeField] private string id = string.Empty;
        [SerializeField] private LocalizedString displayName = new LocalizedString();
        [SerializeField] private LocalizedString summary = new LocalizedString();
        [SerializeField] private Sprite mapSprite;

        public string Id => id;
        public LocalizedString DisplayName => displayName;
        public LocalizedString Summary => summary;
        public Sprite MapSprite => mapSprite;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id))
                Debug.LogWarning("Assign a stable, non-empty ID to this room variant.", this);
        }
    }
}
