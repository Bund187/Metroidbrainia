using System;
using System.Collections.Generic;

namespace Metroidbrainia
{
    [Serializable]
    public sealed class SaveGameData
    {
        public const int CurrentVersion = 1;
        public int version;
        public MapProgressData map;
        public WorldProgressData world;
    }

    [Serializable]
    public sealed class MapProgressData
    {
        public List<RoomProgressEntry> rooms = new List<RoomProgressEntry>();
        public List<DiscoveryProgressEntry> discoveries = new List<DiscoveryProgressEntry>();
    }

    [Serializable]
    public sealed class RoomProgressEntry
    {
        public string id;
        public MapEntryState state;
        public int discoveryOrder = -1;
        public bool hasExplicitVariant;
        public string variantId;
    }

    [Serializable]
    public sealed class DiscoveryProgressEntry
    {
        public string id;
        public MapEntryState state;
        public int discoveryOrder = -1;
        public bool resolved;
    }

    [Serializable]
    public sealed class WorldProgressData
    {
        public bool bellPuzzleSolved;
        public bool studioSwitchOn;
    }
}
