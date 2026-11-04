using System;
using System.Collections.Generic;
using UnityEngine;

namespace Metroidbrainia
{
    [DisallowMultipleComponent]
    public sealed class MapJournalState : MonoBehaviour
    {
        private readonly Dictionary<MapRoomDefinition, MapEntryState> roomStates
            = new Dictionary<MapRoomDefinition, MapEntryState>();
        private readonly Dictionary<MapDiscoveryDefinition, MapEntryState> discoveryStates
            = new Dictionary<MapDiscoveryDefinition, MapEntryState>();

        // A single sequence also preserves discovery order across both kinds of entry.
        private readonly List<ScriptableObject> discoveryOrder = new List<ScriptableObject>();
        private readonly HashSet<MapDiscoveryDefinition> resolvedDiscoveries = new HashSet<MapDiscoveryDefinition>();
        private readonly Dictionary<MapRoomDefinition, MapRoomVariantDefinition> roomVariants
            = new Dictionary<MapRoomDefinition, MapRoomVariantDefinition>();

        public event Action<MapRoomDefinition, MapEntryState> RoomStateChanged;
        public event Action<MapDiscoveryDefinition, MapEntryState> DiscoveryStateChanged;
        public event Action<MapDiscoveryDefinition, bool> DiscoveryResolutionChanged;
        public event Action<MapRoomDefinition, MapRoomVariantDefinition, MapRoomVariantDefinition> RoomVariantChanged;
        public bool WasRestored { get; private set; }

        public MapProgressData CaptureProgress()
        {
            var result = new MapProgressData();
            var rooms = new HashSet<MapRoomDefinition>(roomStates.Keys);
            rooms.UnionWith(roomVariants.Keys);
            foreach (MapRoomDefinition room in rooms)
            {
                if (room == null)
                    continue;
                bool explicitVariant = roomVariants.TryGetValue(room, out MapRoomVariantDefinition variant);
                result.rooms.Add(new RoomProgressEntry
                {
                    id = room.Id, state = GetRoomState(room), discoveryOrder = discoveryOrder.IndexOf(room),
                    hasExplicitVariant = explicitVariant, variantId = variant != null ? variant.Id : null
                });
            }
            var discoveries = new HashSet<MapDiscoveryDefinition>(discoveryStates.Keys);
            discoveries.UnionWith(resolvedDiscoveries);
            foreach (MapDiscoveryDefinition discovery in discoveries)
            {
                if (discovery == null)
                    continue;
                result.discoveries.Add(new DiscoveryProgressEntry
                {
                    id = discovery.Id, state = GetDiscoveryState(discovery),
                    discoveryOrder = discoveryOrder.IndexOf(discovery), resolved = IsDiscoveryResolved(discovery)
                });
            }
            return result;
        }

        // Startup only. Validate every ID into temporary collections before replacing any runtime state.
        // This deliberately bypasses all gameplay change events.
        public bool RestoreProgressSilently(MapProgressData progress,
            IReadOnlyDictionary<string, MapRoomDefinition> rooms,
            IReadOnlyDictionary<string, MapDiscoveryDefinition> discoveries,
            IReadOnlyDictionary<string, MapRoomVariantDefinition> variants,
            bool completePendingReveals = true)
        {
            var envelope = new SaveGameData { version = SaveGameData.CurrentVersion, map = progress, world = new WorldProgressData() };
            if (rooms == null || discoveries == null || variants == null || !SaveGameService.Validate(envelope, out _))
                return false;
            var restoredRooms = new Dictionary<MapRoomDefinition, MapEntryState>();
            var restoredDiscoveries = new Dictionary<MapDiscoveryDefinition, MapEntryState>();
            var restoredVariants = new Dictionary<MapRoomDefinition, MapRoomVariantDefinition>();
            var restoredResolved = new HashSet<MapDiscoveryDefinition>();
            var ordered = new SortedDictionary<int, ScriptableObject>();
            foreach (RoomProgressEntry entry in progress.rooms)
            {
                if (!rooms.TryGetValue(entry.id, out MapRoomDefinition room) || room == null)
                    return false;
                MapRoomVariantDefinition variant = null;
                if (entry.hasExplicitVariant && !string.IsNullOrEmpty(entry.variantId)
                    && (!variants.TryGetValue(entry.variantId, out variant) || variant == null))
                    return false;
                restoredRooms[room] = RestoredState(entry.state, completePendingReveals);
                if (entry.hasExplicitVariant)
                    restoredVariants[room] = variant;
                if (entry.discoveryOrder >= 0)
                    ordered.Add(entry.discoveryOrder, room);
            }
            foreach (DiscoveryProgressEntry entry in progress.discoveries)
            {
                if (!discoveries.TryGetValue(entry.id, out MapDiscoveryDefinition discovery) || discovery == null
                    || entry.resolved && discovery.Type != MapDiscoveryType.Question)
                    return false;
                restoredDiscoveries[discovery] = RestoredState(entry.state, completePendingReveals);
                if (entry.resolved)
                    restoredResolved.Add(discovery);
                if (entry.discoveryOrder >= 0)
                    ordered.Add(entry.discoveryOrder, discovery);
            }
            roomStates.Clear();
            discoveryStates.Clear();
            roomVariants.Clear();
            resolvedDiscoveries.Clear();
            discoveryOrder.Clear();
            foreach (var entry in restoredRooms) roomStates.Add(entry.Key, entry.Value);
            foreach (var entry in restoredDiscoveries) discoveryStates.Add(entry.Key, entry.Value);
            foreach (var entry in restoredVariants) roomVariants.Add(entry.Key, entry.Value);
            resolvedDiscoveries.UnionWith(restoredResolved);
            discoveryOrder.AddRange(ordered.Values);
            WasRestored = true;
            return true;
        }

        private static MapEntryState RestoredState(MapEntryState state, bool completePendingReveals)
        {
            return completePendingReveals && state == MapEntryState.PendingReveal ? MapEntryState.Revealed : state;
        }

        public bool IsDiscoveryResolved(MapDiscoveryDefinition discovery)
        {
            return discovery != null && discovery.Type == MapDiscoveryType.Question && resolvedDiscoveries.Contains(discovery);
        }

        public bool ResolveDiscovery(MapDiscoveryDefinition discovery)
        {
            if (discovery == null || discovery.Type != MapDiscoveryType.Question || !resolvedDiscoveries.Add(discovery))
                return false;
            DiscoveryResolutionChanged?.Invoke(discovery, true);
            return true;
        }

        public MapRoomVariantDefinition GetRoomVariant(MapRoomDefinition room)
        {
            if (room == null)
                return null;
            // A stored null explicitly selects the base presentation, overriding InitialVariant.
            return roomVariants.TryGetValue(room, out MapRoomVariantDefinition variant) ? variant : room.InitialVariant;
        }

        public bool SetRoomVariant(MapRoomDefinition room, MapRoomVariantDefinition variant)
        {
            if (room == null)
                return false;
            MapRoomVariantDefinition previous = GetRoomVariant(room);
            if (previous == variant)
                return false;
            roomVariants[room] = variant;
            RoomVariantChanged?.Invoke(room, previous, variant);
            return true;
        }

        public MapEntryState GetRoomState(MapRoomDefinition room)
        {
            return room != null && roomStates.TryGetValue(room, out MapEntryState state)
                ? state : MapEntryState.Undiscovered;
        }

        public MapEntryState GetDiscoveryState(MapDiscoveryDefinition discovery)
        {
            return discovery != null && discoveryStates.TryGetValue(discovery, out MapEntryState state)
                ? state : MapEntryState.Undiscovered;
        }

        public bool DiscoverRoom(MapRoomDefinition room)
        {
            if (room == null || GetRoomState(room) != MapEntryState.Undiscovered)
                return false;

            roomStates[room] = MapEntryState.PendingReveal;
            discoveryOrder.Add(room);
            RoomStateChanged?.Invoke(room, MapEntryState.PendingReveal);
            return true;
        }

        public bool DiscoverDiscovery(MapDiscoveryDefinition discovery)
        {
            if (discovery == null || GetDiscoveryState(discovery) != MapEntryState.Undiscovered)
                return false;

            discoveryStates[discovery] = MapEntryState.PendingReveal;
            discoveryOrder.Add(discovery);
            DiscoveryStateChanged?.Invoke(discovery, MapEntryState.PendingReveal);
            return true;
        }

        public bool MarkRoomRevealed(MapRoomDefinition room)
        {
            if (room == null || GetRoomState(room) != MapEntryState.PendingReveal)
                return false;

            roomStates[room] = MapEntryState.Revealed;
            RoomStateChanged?.Invoke(room, MapEntryState.Revealed);
            return true;
        }

        public bool MarkDiscoveryRevealed(MapDiscoveryDefinition discovery)
        {
            if (discovery == null || GetDiscoveryState(discovery) != MapEntryState.PendingReveal)
                return false;

            discoveryStates[discovery] = MapEntryState.Revealed;
            DiscoveryStateChanged?.Invoke(discovery, MapEntryState.Revealed);
            return true;
        }

        // Queries return snapshots, so callers can reveal entries while processing the results.
        public IReadOnlyList<MapRoomDefinition> GetPendingRooms()
        {
            var results = new List<MapRoomDefinition>();
            foreach (ScriptableObject entry in discoveryOrder)
            {
                if (entry is MapRoomDefinition room && GetRoomState(room) == MapEntryState.PendingReveal)
                    results.Add(room);
            }
            return results;
        }

        public IReadOnlyList<MapDiscoveryDefinition> GetPendingDiscoveries()
        {
            var results = new List<MapDiscoveryDefinition>();
            foreach (ScriptableObject entry in discoveryOrder)
            {
                if (entry is MapDiscoveryDefinition discovery
                    && GetDiscoveryState(discovery) == MapEntryState.PendingReveal)
                    results.Add(discovery);
            }
            return results;
        }

        public IReadOnlyList<ScriptableObject> GetPendingEntries()
        {
            var results = new List<ScriptableObject>();
            foreach (ScriptableObject entry in discoveryOrder)
            {
                if (entry is MapRoomDefinition room && GetRoomState(room) == MapEntryState.PendingReveal
                    || entry is MapDiscoveryDefinition discovery
                    && GetDiscoveryState(discovery) == MapEntryState.PendingReveal)
                    results.Add(entry);
            }
            return results;
        }

        public IReadOnlyList<ScriptableObject> GetKnownEntries()
        {
            var results = new List<ScriptableObject>();
            foreach (ScriptableObject entry in discoveryOrder)
            {
                if (entry != null)
                    results.Add(entry);
            }
            return results;
        }
    }
}
