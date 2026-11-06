using UnityEngine;

namespace Metroidbrainia
{
    [DisallowMultipleComponent]
    public sealed class MapInitialDiscoverySeeder : MonoBehaviour
    {
        [SerializeField] private MapJournalState journalState;
        [SerializeField] private MapRoomDefinition[] initialRooms = new MapRoomDefinition[0];

        private void Start()
        {
            if (journalState == null)
            {
                Debug.LogError("Assign MapJournalState before seeding initial rooms.", this);
                return;
            }
            if (initialRooms == null || journalState.WasRestored)
                return;

            foreach (MapRoomDefinition room in initialRooms)
            {
                if (room != null)
                    journalState.DiscoverRoom(room);
            }
        }
    }
}
