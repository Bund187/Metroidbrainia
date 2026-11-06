using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace Metroidbrainia
{
    [DisallowMultipleComponent]
    public sealed class MapRoomsPageController : MonoBehaviour
    {
        [SerializeField] private MapJournalState journalState;
        [SerializeField] private MapScreenController mapScreenController;
        [SerializeField] private MapRoomView[] roomViews = new MapRoomView[0];
        [SerializeField] private TMP_Text roomTitleText;
        [SerializeField] private TMP_Text roomSummaryText;
        [SerializeField] private GameObject roomInfoPanel;
        [SerializeField] private Button deselectAreaButton;
        [SerializeField, Min(0f)] private float roomRevealDuration = 0.6f;
        [SerializeField, Min(0f)] private float delayBetweenRoomReveals = 0.15f;
        [SerializeField] private AudioSource revealAudioSource;
        [SerializeField] private AudioClip roomRevealClip;
        [Header("Discovery markers and list")]
        [SerializeField] private Transform discoveryListContainer;
        [SerializeField] private MapRoomDiscoverySummaryItemView discoverySummaryItemPrefab;
        [SerializeField] private MapDiscoveriesPageController discoveriesPageController;
        [SerializeField] private Sprite documentSprite;
        [SerializeField] private Sprite questionSprite;
        [SerializeField] private Sprite resolvedQuestionSprite;

        private MapJournalState subscribedJournal;
        private Coroutine revealRoutine;
        private bool revealing;
        private LocalizedString selectedTitle;
        private LocalizedString selectedSummary;
        private MapRoomView selectedRoom;
        private readonly Dictionary<MapRoomView, MapRoomDiscoveryMarker[]> roomMarkers
            = new Dictionary<MapRoomView, MapRoomDiscoveryMarker[]>();
        private readonly List<MapRoomDiscoveryMarker> markers = new List<MapRoomDiscoveryMarker>();
        private readonly List<MapRoomDiscoverySummaryItemView> summaryItems = new List<MapRoomDiscoverySummaryItemView>();
        private MapRoomDiscoveryMarker openMarker;
        private bool markersCached;
        private MapScreenController closeEventSource;

        private bool IsMapPageVisible => isActiveAndEnabled && mapScreenController != null
            && mapScreenController.IsOpen && mapScreenController.CurrentTab == MapTab.Map;

        private void OnEnable()
        {
            if (journalState == null || mapScreenController == null || roomTitleText == null
                || roomSummaryText == null || roomInfoPanel == null || deselectAreaButton == null)
            {
                Debug.LogError("Assign the journal, MapScreenController, room information panel/texts and DeselectArea Button.", this);
                enabled = false;
                return;
            }

            subscribedJournal = journalState;
            // Keep this subscription while the Map tab is inactive so closing from Discoveries also clears rooms.
            if (closeEventSource != mapScreenController)
            {
                if (closeEventSource != null)
                    closeEventSource.MapClosed -= ClearSelection;
                closeEventSource = mapScreenController;
                closeEventSource.MapClosed += ClearSelection;
            }
            subscribedJournal.RoomStateChanged += OnRoomStateChanged;
            subscribedJournal.RoomVariantChanged += OnRoomVariantChanged;
            subscribedJournal.DiscoveryStateChanged += OnDiscoveryStateChanged;
            subscribedJournal.DiscoveryResolutionChanged += OnDiscoveryResolutionChanged;
            CacheMarkers();
            foreach (MapRoomDiscoveryMarker marker in markers)
            {
                if (marker == null)
                    continue;
                marker.Clicked -= OnMarkerClicked;
                marker.Clicked += OnMarkerClicked;
            }
            deselectAreaButton.onClick.RemoveListener(OnDeselectClicked);
            deselectAreaButton.onClick.AddListener(OnDeselectClicked);
            if (roomViews != null)
            {
                foreach (MapRoomView view in roomViews)
                {
                    if (view != null && view.Initialize())
                    {
                        // Also safe if an Inspector array accidentally contains the same view twice.
                        view.Selected -= OnRoomSelected;
                        view.Selected += OnRoomSelected;
                    }
                }
            }
            SynchronizeViews();
            RefreshMarkers();
            BindSelectionText();
            TryStartReveals();
        }

        private void SynchronizeViews()
        {
            if (roomViews == null)
                return;
            foreach (MapRoomView view in roomViews)
            {
                if (view == null || !view.Initialize())
                    continue;
                view.ApplyState(journalState != null
                    ? journalState.GetRoomState(view.RoomDefinition) : MapEntryState.Undiscovered);
                view.SetInteractionBlocked(revealing);
                view.SetSelected(view == selectedRoom);
                view.ApplyVariant(journalState != null ? journalState.GetRoomVariant(view.RoomDefinition) : null);
            }
        }

        private void CacheMarkers()
        {
            if (markersCached)
                return;
            markersCached = true;
            if (roomViews == null)
                return;
            foreach (MapRoomView view in roomViews)
            {
                if (view == null || roomMarkers.ContainsKey(view))
                    continue;
                MapRoomDiscoveryMarker[] children = view.GetComponentsInChildren<MapRoomDiscoveryMarker>(true);
                roomMarkers.Add(view, children);
                foreach (MapRoomDiscoveryMarker marker in children)
                {
                    if (marker != null && !markers.Contains(marker) && marker.Initialize())
                        markers.Add(marker);
                }
            }
        }

        private Sprite GetDiscoverySprite(MapDiscoveryDefinition discovery)
        {
            if (discovery.Type == MapDiscoveryType.Document)
                return documentSprite;
            return journalState != null && journalState.IsDiscoveryResolved(discovery)
                ? resolvedQuestionSprite : questionSprite;
        }

        private void RefreshMarkers()
        {
            bool blocked = revealing || (mapScreenController != null && mapScreenController.IsContentInteractionBlocked);
            foreach (MapRoomDiscoveryMarker marker in markers)
            {
                if (marker == null || marker.DiscoveryDefinition == null)
                    continue;
                bool known = journalState != null
                    && journalState.GetDiscoveryState(marker.DiscoveryDefinition) != MapEntryState.Undiscovered;
                marker.Configure(known, GetDiscoverySprite(marker.DiscoveryDefinition), blocked);
            }
        }

        private void OnMarkerClicked(MapRoomDiscoveryMarker marker)
        {
            if (!IsMapPageVisible || revealing || mapScreenController.IsContentInteractionBlocked
                || journalState == null || marker == null
                || journalState.GetDiscoveryState(marker.DiscoveryDefinition) == MapEntryState.Undiscovered)
                return;
            bool wasOpen = openMarker == marker;
            CloseMarkerLabel();
            if (!wasOpen)
            {
                openMarker = marker;
                marker.SetLabelOpen(true);
            }
        }

        private void CloseMarkerLabel()
        {
            if (openMarker != null)
                openMarker.SetLabelOpen(false);
            openMarker = null;
        }

        private void OnDiscoveryStateChanged(MapDiscoveryDefinition discovery, MapEntryState state)
        {
            RefreshDiscoveryPresentation(discovery);
        }

        private void OnDiscoveryResolutionChanged(MapDiscoveryDefinition discovery, bool resolved)
        {
            RefreshDiscoveryPresentation(discovery);
        }

        private void RefreshDiscoveryPresentation(MapDiscoveryDefinition discovery)
        {
            RefreshMarkers();
            if (selectedRoom != null && roomMarkers.TryGetValue(selectedRoom, out MapRoomDiscoveryMarker[] children))
            {
                foreach (MapRoomDiscoveryMarker marker in children)
                {
                    if (marker != null && marker.DiscoveryDefinition == discovery)
                    {
                        RebuildDiscoveryList();
                        break;
                    }
                }
            }
        }

        private void OnRoomVariantChanged(MapRoomDefinition room, MapRoomVariantDefinition previous, MapRoomVariantDefinition current)
        {
            if (roomViews != null)
            {
                foreach (MapRoomView view in roomViews)
                {
                    if (view != null && view.RoomDefinition == room)
                        view.ApplyVariant(current);
                }
            }
            if (selectedRoom != null && selectedRoom.RoomDefinition == room)
                BindSelectionText();
        }

        private void RebuildDiscoveryList()
        {
            ClearDiscoveryList();
            if (selectedRoom == null || journalState == null || discoveryListContainer == null
                || discoverySummaryItemPrefab == null
                || !roomMarkers.TryGetValue(selectedRoom, out MapRoomDiscoveryMarker[] children))
                return;

            var included = new HashSet<MapDiscoveryDefinition>();
            foreach (MapRoomDiscoveryMarker marker in children)
            {
                MapDiscoveryDefinition discovery = marker != null ? marker.DiscoveryDefinition : null;
                if (discovery == null || journalState.GetDiscoveryState(discovery) == MapEntryState.Undiscovered
                    || !included.Add(discovery))
                    continue;
                MapRoomDiscoverySummaryItemView item = Instantiate(discoverySummaryItemPrefab, discoveryListContainer, false);
                item.gameObject.SetActive(true);
                summaryItems.Add(item);
                item.Bind(discovery, GetDiscoverySprite(discovery), OnDiscoveryItemClicked);
            }
        }

        private void ClearDiscoveryList()
        {
            foreach (MapRoomDiscoverySummaryItemView item in summaryItems)
            {
                if (item == null)
                    continue;
                item.Unbind();
                item.gameObject.SetActive(false);
                Destroy(item.gameObject);
            }
            summaryItems.Clear();
        }

        private void OnDiscoveryItemClicked(MapDiscoveryDefinition discovery)
        {
            if (IsMapPageVisible && !revealing && !mapScreenController.IsContentInteractionBlocked
                && discoveriesPageController != null)
                discoveriesPageController.FocusDiscovery(discovery);
        }

        private void OnRoomStateChanged(MapRoomDefinition room, MapEntryState state)
        {
            if (roomViews != null)
            {
                foreach (MapRoomView view in roomViews)
                {
                    if (view != null && view.RoomDefinition == room)
                    {
                        view.ApplyState(state);
                        view.SetInteractionBlocked(revealing);
                    }
                }
            }
            if (state == MapEntryState.PendingReveal)
                TryStartReveals();
        }

        private MapRoomView FindNextPendingView()
        {
            if (journalState == null || roomViews == null)
                return null;
            foreach (MapRoomDefinition room in journalState.GetPendingRooms())
            {
                foreach (MapRoomView view in roomViews)
                {
                    if (view != null && view.isActiveAndEnabled && view.RoomDefinition == room
                        && view.Initialize())
                        return view;
                }
            }
            // Entries without a configured view stay pending; do not silently reveal them.
            return null;
        }

        private void TryStartReveals()
        {
            if (revealing || !IsMapPageVisible || FindNextPendingView() == null)
                return;

            revealing = true;
            mapScreenController.SetContentInteractionBlocked(true);
            SetViewsInteractionBlocked(true);
            revealRoutine = StartCoroutine(RevealPendingRooms());
        }

        private IEnumerator RevealPendingRooms()
        {
            // Allow child views to complete OnEnable before writing the first fade frame.
            yield return null;
            while (IsMapPageVisible && journalState != null)
            {
                MapRoomView view = FindNextPendingView();
                if (view == null)
                    break;

                MapRoomDefinition room = view.RoomDefinition;
                if (revealAudioSource != null && roomRevealClip != null)
                    revealAudioSource.PlayOneShot(roomRevealClip);
                float elapsed = 0f;
                while (elapsed < roomRevealDuration && IsMapPageVisible && view != null
                    && view.isActiveAndEnabled && journalState != null
                    && journalState.GetRoomState(room) == MapEntryState.PendingReveal)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / roomRevealDuration);
                    view.SetRevealProgress(Mathf.SmoothStep(0f, 1f, t));
                    yield return null;
                }

                if (!IsMapPageVisible || journalState == null)
                    break;
                if (view != null && view.isActiveAndEnabled && elapsed >= roomRevealDuration)
                {
                    view.SetRevealProgress(1f);
                    journalState.MarkRoomRevealed(room);
                }
                if (view != null && journalState.GetRoomState(room) == MapEntryState.PendingReveal)
                    view.ApplyState(MapEntryState.PendingReveal);

                if (FindNextPendingView() != null && delayBetweenRoomReveals > 0f)
                    yield return new WaitForSecondsRealtime(delayBetweenRoomReveals);
            }
            FinishReveals();
        }

        private void FinishReveals()
        {
            bool ownedBlock = revealing;
            revealing = false;
            revealRoutine = null;
            if (ownedBlock && mapScreenController != null)
                mapScreenController.SetContentInteractionBlocked(false);
            ResetPendingViews();
            SetViewsInteractionBlocked(false);
        }

        private void SetViewsInteractionBlocked(bool blocked)
        {
            RefreshMarkers();
            if (roomViews == null)
                return;
            foreach (MapRoomView view in roomViews)
            {
                if (view != null)
                    view.SetInteractionBlocked(blocked);
            }
        }

        private void ResetPendingViews()
        {
            if (roomViews == null || journalState == null)
                return;
            foreach (MapRoomView view in roomViews)
            {
                if (view != null && journalState.GetRoomState(view.RoomDefinition) == MapEntryState.PendingReveal)
                    view.ApplyState(MapEntryState.PendingReveal);
            }
        }

        private void OnRoomSelected(MapRoomView view)
        {
            if (mapScreenController == null || revealing || mapScreenController.IsContentInteractionBlocked)
                return;
            SelectRoomView(view);
        }

        public bool SelectRoom(MapRoomDefinition room)
        {
            if (room == null || roomViews == null)
                return false;
            foreach (MapRoomView view in roomViews)
            {
                if (view != null && view.isActiveAndEnabled && view.RoomDefinition == room && view.Initialize())
                    return SelectRoomView(view);
            }
            return false;
        }

        private bool SelectRoomView(MapRoomView view)
        {
            if (!IsMapPageVisible || journalState == null || view == null
                || journalState.GetRoomState(view.RoomDefinition) != MapEntryState.Revealed)
                return false;

            if (selectedRoom == view)
                return true;
            ClearSelection();
            selectedRoom = view;
            selectedRoom.SetSelected(true);
            BindSelectionText();
            return true;
        }

        private void BindSelectionText()
        {
            if (selectedRoom == null || journalState == null
                || journalState.GetRoomState(selectedRoom.RoomDefinition) != MapEntryState.Revealed)
            {
                ClearSelection();
                return;
            }

            UnbindSelectionText();
            if (roomInfoPanel != null)
                roomInfoPanel.SetActive(true);
            MapRoomDefinition room = selectedRoom.RoomDefinition;
            MapRoomVariantDefinition variant = journalState.GetRoomVariant(room);
            LocalizedString displayName = variant != null ? variant.DisplayName : room.DisplayName;
            LocalizedString summary = variant != null ? variant.Summary : room.Summary;
            if (displayName != null && !displayName.IsEmpty)
            {
                selectedTitle = displayName;
                selectedTitle.StringChanged += UpdateTitle;
            }
            if (summary != null && !summary.IsEmpty)
            {
                selectedSummary = summary;
                selectedSummary.StringChanged += UpdateSummary;
            }
            RebuildDiscoveryList();
        }

        private void UpdateTitle(string value)
        {
            if (roomTitleText != null)
                roomTitleText.text = value;
        }

        private void UpdateSummary(string value)
        {
            if (roomSummaryText != null)
                roomSummaryText.text = value;
        }

        private void UnbindSelectionText()
        {
            if (selectedTitle != null)
                selectedTitle.StringChanged -= UpdateTitle;
            if (selectedSummary != null)
                selectedSummary.StringChanged -= UpdateSummary;
            selectedTitle = null;
            selectedSummary = null;
            if (roomTitleText != null)
                roomTitleText.text = string.Empty;
            if (roomSummaryText != null)
                roomSummaryText.text = string.Empty;
        }

        private void HideRoomInfo()
        {
            UnbindSelectionText();
            ClearDiscoveryList();
            if (roomInfoPanel != null)
                roomInfoPanel.SetActive(false);
        }

        public void ClearSelection()
        {
            CloseMarkerLabel();
            if (selectedRoom != null)
                selectedRoom.SetSelected(false);
            selectedRoom = null;
            HideRoomInfo();
        }

        private void OnDeselectClicked()
        {
            if (IsMapPageVisible && !revealing && !mapScreenController.IsContentInteractionBlocked)
                ClearSelection();
        }

        private void OnDisable()
        {
            if (subscribedJournal != null)
            {
                subscribedJournal.RoomStateChanged -= OnRoomStateChanged;
                subscribedJournal.RoomVariantChanged -= OnRoomVariantChanged;
                subscribedJournal.DiscoveryStateChanged -= OnDiscoveryStateChanged;
                subscribedJournal.DiscoveryResolutionChanged -= OnDiscoveryResolutionChanged;
            }
            subscribedJournal = null;
            CloseMarkerLabel();
            foreach (MapRoomDiscoveryMarker marker in markers)
            {
                if (marker != null)
                    marker.Clicked -= OnMarkerClicked;
            }
            if (deselectAreaButton != null)
                deselectAreaButton.onClick.RemoveListener(OnDeselectClicked);
            if (roomViews != null)
            {
                foreach (MapRoomView view in roomViews)
                {
                    if (view != null)
                        view.Selected -= OnRoomSelected;
                }
            }
            if (revealRoutine != null)
                StopCoroutine(revealRoutine);
            if (revealAudioSource != null)
                revealAudioSource.Stop();
            FinishReveals();
            HideRoomInfo();
        }

        private void OnDestroy()
        {
            if (closeEventSource != null)
                closeEventSource.MapClosed -= ClearSelection;
            closeEventSource = null;
        }
    }
}
