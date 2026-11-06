using UnityEngine;

namespace Metroidbrainia
{
    [DisallowMultipleComponent]
    public sealed class MapDiscoveryResolveAction : MonoBehaviour
    {
        [SerializeField] private MapJournalState journalState;
        [SerializeField] private MapDiscoveryDefinition[] discoveries = new MapDiscoveryDefinition[0];

        public void Resolve()
        {
            if (journalState == null || discoveries == null)
                return;
            foreach (MapDiscoveryDefinition discovery in discoveries)
            {
                if (discovery != null)
                    journalState.ResolveDiscovery(discovery);
            }
        }
    }
}
