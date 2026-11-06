using UnityEngine;

namespace Metroidbrainia
{
    [DisallowMultipleComponent]
    public sealed class MapDiscoveryDiscoverAction : MonoBehaviour
    {
        [SerializeField] private MapJournalState journalState;
        [SerializeField] private MapDiscoveryDefinition[] discoveries = new MapDiscoveryDefinition[0];

        public void Discover()
        {
            if (journalState == null || discoveries == null)
                return;
            foreach (MapDiscoveryDefinition discovery in discoveries)
            {
                if (discovery != null)
                    journalState.DiscoverDiscovery(discovery);
            }
        }
    }
}
