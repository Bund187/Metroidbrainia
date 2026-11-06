using UnityEngine;
using UnityEngine.UI;

namespace Metroidbrainia
{
    [DisallowMultipleComponent]
    public sealed class MapDiscoveryConnectionView : MonoBehaviour
    {
        [SerializeField] private MapDiscoveryDefinition fromDiscovery;
        [SerializeField] private MapDiscoveryDefinition toDiscovery;
        [SerializeField] private Image connectionImage;
        [SerializeField] private CanvasGroup canvasGroup;

        public MapDiscoveryDefinition FromDiscovery => fromDiscovery;
        public MapDiscoveryDefinition ToDiscovery => toDiscovery;

        public bool Initialize()
        {
            if (connectionImage == null)
                connectionImage = GetComponent<Image>();
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            if (fromDiscovery == null || toDiscovery == null || connectionImage == null
                || (connectionImage.type != Image.Type.Filled && canvasGroup == null))
            {
                Debug.LogError("Assign both discoveries and a connection Image. Non-Filled Images also need a CanvasGroup for fading.", this);
                return false;
            }
            return true;
        }

        public bool CanRevealWith(MapDiscoveryDefinition discovery, MapJournalState journal)
        {
            if (discovery == null || journal == null)
                return false;
            return fromDiscovery == discovery && journal.GetDiscoveryState(toDiscovery) == MapEntryState.Revealed
                || toDiscovery == discovery && journal.GetDiscoveryState(fromDiscovery) == MapEntryState.Revealed;
        }

        public void Synchronize(MapJournalState journal)
        {
            bool visible = journal != null && fromDiscovery != null && toDiscovery != null
                && journal.GetDiscoveryState(fromDiscovery) == MapEntryState.Revealed
                && journal.GetDiscoveryState(toDiscovery) == MapEntryState.Revealed;
            SetRevealProgress(visible ? 1f : 0f);
        }

        public void SetRevealProgress(float progress)
        {
            if (!Initialize())
                return;
            float amount = Mathf.Clamp01(progress);
            bool filled = connectionImage.type == Image.Type.Filled;
            if (filled)
                connectionImage.fillAmount = amount;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = filled ? (amount > 0f ? 1f : 0f) : amount;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }
    }
}
