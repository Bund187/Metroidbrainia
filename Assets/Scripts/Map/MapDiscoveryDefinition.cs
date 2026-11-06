using UnityEngine;
using UnityEngine.Localization;

namespace Metroidbrainia
{
    [CreateAssetMenu(menuName = "Metroidbrainia/Map/Discovery Definition", fileName = "MapDiscovery")]
    public sealed class MapDiscoveryDefinition : ScriptableObject
    {
        [SerializeField] private string id = string.Empty;
        [SerializeField] private LocalizedString displayName = new LocalizedString();
        [SerializeField] private LocalizedString summary = new LocalizedString();
        [SerializeField] private MapRoomDefinition relatedRoom;
        [SerializeField] private MapDiscoveryType type;

        public string Id => id;
        public LocalizedString DisplayName => displayName;
        public LocalizedString Summary => summary;
        public MapRoomDefinition RelatedRoom => relatedRoom;
        public MapDiscoveryType Type => type;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id))
                Debug.LogWarning("Assign a stable, non-empty ID to this discovery definition.", this);
        }
    }
}
