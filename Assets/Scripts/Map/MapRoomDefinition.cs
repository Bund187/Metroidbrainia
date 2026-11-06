using UnityEngine;
using UnityEngine.Localization;

namespace Metroidbrainia
{
    [CreateAssetMenu(menuName = "Metroidbrainia/Map/Room Definition", fileName = "MapRoom")]
    public sealed class MapRoomDefinition : ScriptableObject
    {
        [SerializeField] private string id = string.Empty;
        [SerializeField] private LocalizedString displayName = new LocalizedString();
        [SerializeField] private LocalizedString summary = new LocalizedString();
        [SerializeField] private MapRoomVariantDefinition initialVariant;

        public string Id => id;
        public LocalizedString DisplayName => displayName;
        public LocalizedString Summary => summary;
        public MapRoomVariantDefinition InitialVariant => initialVariant;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id))
                Debug.LogWarning("Assign a stable, non-empty ID to this room definition.", this);
        }
    }
}
