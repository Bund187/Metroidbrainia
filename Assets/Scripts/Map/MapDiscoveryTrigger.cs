using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;

namespace Metroidbrainia
{
    [DisallowMultipleComponent]
    [MovedFrom(true, sourceNamespace: "Metroidbrainia", sourceAssembly: "Assembly-CSharp", sourceClassName: "RoomDiscoveryTrigger")]
    public sealed class MapDiscoveryTrigger : MonoBehaviour, ISerializationCallbackReceiver
    {
        [SerializeField] private MapJournalState journalState;
        [SerializeField] private CharacterController player;
        [SerializeField] private MapRoomDefinition[] roomsToDiscover = new MapRoomDefinition[0];
        [SerializeField] private MapDiscoveryDefinition[] discoveriesToDiscover = new MapDiscoveryDefinition[0];
        [SerializeField] private bool triggerOnlyOnce = true;

        [FormerlySerializedAs("room")]
        [SerializeField, HideInInspector] private MapRoomDefinition legacyRoom;

        private bool hasTriggered;

        private void Awake()
        {
            Collider trigger = GetComponent<Collider>();
            if (journalState == null || player == null || trigger == null || !trigger.isTrigger)
            {
                Debug.LogError("Assign the journal and PlayerCapsule CharacterController on an object with a trigger Collider.", this);
                enabled = false;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isActiveAndEnabled || (triggerOnlyOnce && hasTriggered) || journalState == null
                || player == null || other == null || other.GetComponentInParent<CharacterController>() != player)
                return;

            // Set before notifying the journal so event callbacks cannot re-enter this activation.
            hasTriggered = true;
            if (roomsToDiscover != null)
            {
                foreach (MapRoomDefinition room in roomsToDiscover)
                {
                    if (room != null)
                        journalState.DiscoverRoom(room);
                }
            }
            if (discoveriesToDiscover != null)
            {
                foreach (MapDiscoveryDefinition discovery in discoveriesToDiscover)
                {
                    if (discovery != null)
                        journalState.DiscoverDiscovery(discovery);
                }
            }
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            // Serialization callbacks may run off the main thread; use only managed operations.
            if (!ReferenceEquals(legacyRoom, null) && (roomsToDiscover == null || roomsToDiscover.Length == 0))
                roomsToDiscover = new[] { legacyRoom };
            legacyRoom = null;
        }
    }
}
