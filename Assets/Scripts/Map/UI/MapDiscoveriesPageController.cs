using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Metroidbrainia
{
    [DisallowMultipleComponent]
    public sealed class MapDiscoveriesPageController : MonoBehaviour
    {
        [SerializeField] private MapJournalState journalState;
        [SerializeField] private MapScreenController mapScreenController;
        [SerializeField] private MapRoomsPageController roomsPageController;
        [SerializeField] private MapDiscoveryNodeView[] nodeViews = new MapDiscoveryNodeView[0];
        [SerializeField] private MapDiscoveryConnectionView[] connectionViews = new MapDiscoveryConnectionView[0];
        [SerializeField] private Button deselectAreaButton;
        [SerializeField, Min(0f)] private float discoveryRevealDuration = 0.6f;
        [SerializeField, Min(0f)] private float connectionRevealDuration = 0.4f;
        [SerializeField, Min(0f)] private float selectionAnimationDuration = 0.22f;

        private readonly List<MapDiscoveryConnectionView> revealingConnections = new List<MapDiscoveryConnectionView>();
        private MapJournalState subscribedJournal;
        private MapDiscoveryNodeView selectedNode;
        private Coroutine revealRoutine;
        private bool revealing;
        private MapDiscoveryDefinition requestedFocus;

        private bool IsPageVisible => isActiveAndEnabled && mapScreenController != null
            && mapScreenController.IsOpen && mapScreenController.CurrentTab == MapTab.Discoveries;

        private void OnEnable()
        {
            if (journalState == null || mapScreenController == null || roomsPageController == null
                || deselectAreaButton == null)
            {
                Debug.LogError("Assign the journal, MapScreenController, rooms page controller and Discoveries DeselectArea Button.", this);
                enabled = false;
                return;
            }
            subscribedJournal = journalState;
            subscribedJournal.DiscoveryStateChanged += OnDiscoveryStateChanged;
            subscribedJournal.RoomStateChanged += OnRoomStateChanged;
            subscribedJournal.DiscoveryResolutionChanged += OnDiscoveryResolutionChanged;
            deselectAreaButton.onClick.RemoveListener(OnDeselectClicked);
            deselectAreaButton.onClick.AddListener(OnDeselectClicked);
            if (nodeViews != null)
            {
                foreach (MapDiscoveryNodeView node in nodeViews)
                {
                    if (node == null || !node.Initialize())
                        continue;
                    node.Selected -= OnNodeSelected;
                    node.Selected += OnNodeSelected;
                    node.RoomLinkRequested -= OnRoomLinkRequested;
                    node.RoomLinkRequested += OnRoomLinkRequested;
                    node.ApplyState(journalState.GetDiscoveryState(node.DiscoveryDefinition));
                    node.SetExpanded(false, 0f);
                    node.SetInteractionBlocked(false);
                    node.SetResolved(journalState.IsDiscoveryResolved(node.DiscoveryDefinition));
                }
            }
            RefreshRoomLinks();
            SynchronizeConnections();
            TryApplyRequestedFocus();
            TryStartReveals();
        }

        private void OnDiscoveryStateChanged(MapDiscoveryDefinition discovery, MapEntryState state)
        {
            if (nodeViews != null)
            {
                foreach (MapDiscoveryNodeView node in nodeViews)
                {
                    if (node != null && node.DiscoveryDefinition == discovery)
                    {
                        node.ApplyState(state);
                        node.SetInteractionBlocked(revealing);
                    }
                }
            }
            // Do not reset a connection whose fill is currently being animated.
            if (!revealing)
            {
                SynchronizeConnections();
                TryApplyRequestedFocus();
            }
            if (state == MapEntryState.PendingReveal)
                TryStartReveals();
        }

        private void OnRoomStateChanged(MapRoomDefinition room, MapEntryState state)
        {
            RefreshRoomLinks();
        }

        private void OnDiscoveryResolutionChanged(MapDiscoveryDefinition discovery, bool resolved)
        {
            if (nodeViews == null)
                return;
            foreach (MapDiscoveryNodeView node in nodeViews)
            {
                if (node != null && node.DiscoveryDefinition == discovery)
                    node.SetResolved(resolved);
            }
        }

        private void RefreshRoomLinks()
        {
            if (nodeViews == null)
                return;
            foreach (MapDiscoveryNodeView node in nodeViews)
            {
                if (node != null && node.DiscoveryDefinition != null)
                    node.SetRoomLinkAvailable(IsRoomRevealed(node.DiscoveryDefinition.RelatedRoom));
            }
        }

        private bool IsRoomRevealed(MapRoomDefinition room)
        {
            return room != null && journalState != null && journalState.GetRoomState(room) == MapEntryState.Revealed;
        }

        private void SynchronizeConnections()
        {
            if (connectionViews == null)
                return;
            foreach (MapDiscoveryConnectionView connection in connectionViews)
            {
                if (connection != null)
                    connection.Synchronize(journalState);
            }
        }

        private MapDiscoveryNodeView FindNextPendingNode()
        {
            if (journalState == null || nodeViews == null)
                return null;
            foreach (MapDiscoveryDefinition discovery in journalState.GetPendingDiscoveries())
            {
                foreach (MapDiscoveryNodeView node in nodeViews)
                {
                    if (node != null && node.isActiveAndEnabled && node.DiscoveryDefinition == discovery && node.Initialize())
                        return node;
                }
            }
            return null;
        }

        private void TryStartReveals()
        {
            if (revealing || !IsPageVisible || mapScreenController.IsContentInteractionBlocked || FindNextPendingNode() == null)
                return;
            revealing = true;
            mapScreenController.SetContentInteractionBlocked(true);
            SetNodesInteractionBlocked(true);
            revealRoutine = StartCoroutine(RevealPendingDiscoveries());
        }

        private bool CanContinueReveal(MapDiscoveryNodeView node, MapDiscoveryDefinition discovery)
        {
            return IsPageVisible && node != null && node.isActiveAndEnabled && journalState != null
                && journalState.GetDiscoveryState(discovery) == MapEntryState.PendingReveal;
        }

        private IEnumerator RevealPendingDiscoveries()
        {
            yield return null;
            while (IsPageVisible && journalState != null)
            {
                MapDiscoveryNodeView node = FindNextPendingNode();
                if (node == null)
                    break;
                MapDiscoveryDefinition discovery = node.DiscoveryDefinition;
                float elapsed = 0f;
                while (elapsed < discoveryRevealDuration && CanContinueReveal(node, discovery))
                {
                    elapsed += Time.unscaledDeltaTime;
                    node.SetRevealProgress(Mathf.SmoothStep(0f, 1f, elapsed / discoveryRevealDuration));
                    yield return null;
                }
                if (!CanContinueReveal(node, discovery))
                    break;
                node.SetRevealProgress(1f);

                CollectConnections(discovery);
                elapsed = 0f;
                while (revealingConnections.Count > 0 && elapsed < connectionRevealDuration
                    && CanContinueReveal(node, discovery))
                {
                    elapsed += Time.unscaledDeltaTime;
                    SetConnectionsProgress(Mathf.SmoothStep(0f, 1f, elapsed / connectionRevealDuration));
                    yield return null;
                }
                if (!CanContinueReveal(node, discovery))
                    break;
                SetConnectionsProgress(1f);
                journalState.MarkDiscoveryRevealed(discovery);
                revealingConnections.Clear();
            }
            FinishReveals();
        }

        private void CollectConnections(MapDiscoveryDefinition discovery)
        {
            revealingConnections.Clear();
            if (connectionViews == null)
                return;
            foreach (MapDiscoveryConnectionView connection in connectionViews)
            {
                if (connection != null && connection.isActiveAndEnabled && connection.Initialize()
                    && connection.CanRevealWith(discovery, journalState))
                    revealingConnections.Add(connection);
            }
        }

        private void SetConnectionsProgress(float progress)
        {
            foreach (MapDiscoveryConnectionView connection in revealingConnections)
            {
                if (connection != null && connection.isActiveAndEnabled)
                    connection.SetRevealProgress(progress);
            }
        }

        private void SetNodesInteractionBlocked(bool blocked)
        {
            if (nodeViews == null)
                return;
            foreach (MapDiscoveryNodeView node in nodeViews)
            {
                if (node != null)
                    node.SetInteractionBlocked(blocked);
            }
        }

        private void FinishReveals()
        {
            bool ownedBlock = revealing;
            revealing = false;
            revealRoutine = null;
            revealingConnections.Clear();
            if (ownedBlock && mapScreenController != null)
                mapScreenController.SetContentInteractionBlocked(false);
            if (nodeViews != null && journalState != null)
            {
                foreach (MapDiscoveryNodeView node in nodeViews)
                {
                    if (node != null && journalState.GetDiscoveryState(node.DiscoveryDefinition) == MapEntryState.PendingReveal)
                        node.ApplyState(MapEntryState.PendingReveal);
                }
            }
            SynchronizeConnections();
            SetNodesInteractionBlocked(false);
            TryApplyRequestedFocus();
        }

        private void OnNodeSelected(MapDiscoveryNodeView node)
        {
            SelectNode(node);
        }

        private bool SelectNode(MapDiscoveryNodeView node)
        {
            if (!IsPageVisible || revealing || mapScreenController.IsContentInteractionBlocked || node == null
                || journalState == null || journalState.GetDiscoveryState(node.DiscoveryDefinition) != MapEntryState.Revealed)
                return false;
            if (selectedNode == node)
                return true;
            ClearSelection();
            selectedNode = node;
            node.SetExpanded(true, selectionAnimationDuration);
            return true;
        }

        public bool FocusDiscovery(MapDiscoveryDefinition discovery)
        {
            if (!enabled || discovery == null || journalState == null || mapScreenController == null
                || !mapScreenController.isActiveAndEnabled || !mapScreenController.IsOpen
                || journalState.GetDiscoveryState(discovery) == MapEntryState.Undiscovered
                || FindConfiguredNode(discovery) == null
                || (mapScreenController.IsContentInteractionBlocked && !revealing))
                return false;

            // Set before activating the page: OnEnable may immediately start its pending reveal sequence.
            requestedFocus = discovery;
            mapScreenController.ShowDiscoveriesTab();
            TryApplyRequestedFocus();
            TryStartReveals();
            return mapScreenController.CurrentTab == MapTab.Discoveries;
        }

        private MapDiscoveryNodeView FindConfiguredNode(MapDiscoveryDefinition discovery)
        {
            if (nodeViews == null)
                return null;
            foreach (MapDiscoveryNodeView node in nodeViews)
            {
                if (node != null && node.enabled && node.DiscoveryDefinition == discovery && node.Initialize())
                    return node;
            }
            return null;
        }

        private void TryApplyRequestedFocus()
        {
            if (!IsPageVisible || requestedFocus == null || journalState == null || revealing
                || mapScreenController.IsContentInteractionBlocked
                || journalState.GetDiscoveryState(requestedFocus) != MapEntryState.Revealed)
                return;
            MapDiscoveryNodeView node = FindConfiguredNode(requestedFocus);
            if (node != null && SelectNode(node))
                requestedFocus = null;
        }

        public void ClearSelection()
        {
            if (selectedNode != null)
                selectedNode.SetExpanded(false, IsPageVisible ? selectionAnimationDuration : 0f);
            selectedNode = null;
        }

        private void OnDeselectClicked()
        {
            if (IsPageVisible && !revealing && !mapScreenController.IsContentInteractionBlocked)
                ClearSelection();
        }

        private void OnRoomLinkRequested(MapDiscoveryNodeView node)
        {
            if (!IsPageVisible || revealing || mapScreenController.IsContentInteractionBlocked
                || node == null || node.DiscoveryDefinition == null)
                return;
            MapRoomDefinition room = node.DiscoveryDefinition.RelatedRoom;
            if (!IsRoomRevealed(room))
                return;
            // ShowMapTab activates and synchronizes the rooms page before selecting its known room.
            mapScreenController.ShowMapTab();
            if (roomsPageController != null)
                roomsPageController.SelectRoom(room);
        }

        private void OnDisable()
        {
            if (subscribedJournal != null)
            {
                subscribedJournal.DiscoveryStateChanged -= OnDiscoveryStateChanged;
                subscribedJournal.RoomStateChanged -= OnRoomStateChanged;
                subscribedJournal.DiscoveryResolutionChanged -= OnDiscoveryResolutionChanged;
            }
            subscribedJournal = null;
            if (deselectAreaButton != null)
                deselectAreaButton.onClick.RemoveListener(OnDeselectClicked);
            if (nodeViews != null)
            {
                foreach (MapDiscoveryNodeView node in nodeViews)
                {
                    if (node == null)
                        continue;
                    node.Selected -= OnNodeSelected;
                    node.RoomLinkRequested -= OnRoomLinkRequested;
                    node.SetExpanded(false, 0f);
                }
            }
            selectedNode = null;
            requestedFocus = null;
            if (revealRoutine != null)
                StopCoroutine(revealRoutine);
            FinishReveals();
        }
    }
}
