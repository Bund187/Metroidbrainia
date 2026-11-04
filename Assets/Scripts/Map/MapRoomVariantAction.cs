using UnityEngine;

namespace Metroidbrainia
{
    [DisallowMultipleComponent]
    public sealed class MapRoomVariantAction : MonoBehaviour
    {
        [SerializeField] private MapJournalState journalState;
        [SerializeField] private MapRoomDefinition room;
        [SerializeField] private MapRoomVariantDefinition variant;

        public void Apply()
        {
            if (journalState != null && room != null)
                journalState.SetRoomVariant(room, variant);
        }
    }
}
