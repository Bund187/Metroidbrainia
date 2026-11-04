using System;
using System.Collections.Generic;
using UnityEngine;

namespace Metroidbrainia
{
    // Start runs after all Awake calls, but before initial discovery seeders and player movement.
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class SaveGameController : MonoBehaviour
    {
        [Header("Progress")]
        [SerializeField] private MapJournalState journalState;
        [SerializeField] private BellSequencePuzzle bellPuzzle;
        [SerializeField] private LightSwitchInteractable studioSwitch;
        [Tooltip("Optional: derived from the Studio switch, never stored as separate progress.")]
        [SerializeField] private GameObject studioBlueprintTrigger;
        [Tooltip("Optional: enables Blueprint observation from the restored Studio switch without firing gameplay events.")]
        [SerializeField] private MapObservationDiscovery studioBlueprintObservation;

        [Header("Definition Catalogs")]
        [SerializeField] private MapRoomDefinition[] roomDefinitions = new MapRoomDefinition[0];
        [SerializeField] private MapDiscoveryDefinition[] discoveryDefinitions = new MapDiscoveryDefinition[0];
        [SerializeField] private MapRoomVariantDefinition[] roomVariantDefinitions = new MapRoomVariantDefinition[0];

        private readonly Dictionary<string, MapRoomDefinition> rooms = new Dictionary<string, MapRoomDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, MapDiscoveryDefinition> discoveries = new Dictionary<string, MapDiscoveryDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, MapRoomVariantDefinition> variants = new Dictionary<string, MapRoomVariantDefinition>(StringComparer.Ordinal);
        private SaveGameService service;
        private bool sessionActive;
        private bool subscribed;
        private bool dirty;

        private void Start()
        {
            GameLaunchMode mode = GameLaunchContext.Consume();
            service = new SaveGameService();
            if (journalState == null || bellPuzzle == null || studioSwitch == null
                || !BuildCatalog(roomDefinitions, room => room.Id, rooms)
                || !BuildCatalog(discoveryDefinitions, discovery => discovery.Id, discoveries)
                || !BuildCatalog(roomVariantDefinitions, variant => variant.Id, variants))
            {
                Debug.LogError("Saving disabled. Assign the journal, Bells puzzle, Studio switch and valid definition catalogs.", this);
                return;
            }
            foreach (MapRoomDefinition room in rooms.Values)
            {
                if (room.InitialVariant != null && (!variants.TryGetValue(room.InitialVariant.Id, out var variant)
                    || variant != room.InitialVariant))
                {
                    Debug.LogError($"Saving disabled: register the InitialVariant of room '{room.Id}'.", this);
                    return;
                }
            }

            if (mode == GameLaunchMode.Continue)
            {
                SaveGameData data = service.Load();
                if (data == null || !ValidateCatalogReferences(data.map)
                    || !journalState.RestoreProgressSilently(data.map, rooms, discoveries, variants))
                {
                    // Preserve the existing file; never autosave an unsuccessful Continue over it.
                    Debug.LogError("Continue failed. Scene defaults remain active and autosave is disabled; return to TitleScreen to start a new game.", this);
                    return;
                }
                bellPuzzle.RestoreSolvedState(data.world.bellPuzzleSolved);
                studioSwitch.RestoreState(data.world.studioSwitchOn);
            }
            ApplyBlueprintTriggerState();
            sessionActive = true;
            Subscribe();
        }

        private bool BuildCatalog<T>(T[] definitions, Func<T, string> getId, Dictionary<string, T> lookup)
            where T : UnityEngine.Object
        {
            if (definitions == null)
                return true;
            foreach (T definition in definitions)
            {
                if (definition == null)
                    continue;
                string id = getId(definition);
                if (string.IsNullOrWhiteSpace(id) || lookup.ContainsKey(id))
                {
                    Debug.LogError($"Empty or duplicate {typeof(T).Name} ID '{id}' in save catalog.", this);
                    return false;
                }
                lookup.Add(id, definition);
            }
            return true;
        }

        private bool ValidateCatalogReferences(MapProgressData map)
        {
            foreach (RoomProgressEntry entry in map.rooms)
            {
                if (!rooms.ContainsKey(entry.id) || entry.hasExplicitVariant
                    && !string.IsNullOrEmpty(entry.variantId) && !variants.ContainsKey(entry.variantId))
                {
                    Debug.LogError($"Unregistered room or variant ID for room '{entry.id}'. No progress was applied/saved.", this);
                    return false;
                }
            }
            foreach (DiscoveryProgressEntry entry in map.discoveries)
            {
                if (!discoveries.TryGetValue(entry.id, out MapDiscoveryDefinition discovery)
                    || entry.resolved && discovery.Type != MapDiscoveryType.Question)
                {
                    Debug.LogError($"Unregistered or incompatible discovery ID '{entry.id}'. No progress was applied/saved.", this);
                    return false;
                }
            }
            return true;
        }

        public void SaveNow()
        {
            if (!sessionActive || service == null || journalState == null || bellPuzzle == null || studioSwitch == null)
                return;
            // A failed write is retried on the next real change or Quit, not every frame.
            dirty = false;
            var data = new SaveGameData
            {
                version = SaveGameData.CurrentVersion,
                map = journalState.CaptureProgress(),
                world = new WorldProgressData { bellPuzzleSolved = bellPuzzle.IsSolved, studioSwitchOn = studioSwitch.IsOn }
            };
            if (!SaveGameService.Validate(data, out string error))
            {
                Debug.LogError($"Cannot capture progress: {error}", this);
                return;
            }
            if (ValidateCatalogReferences(data.map))
                service.Save(data);
        }

        private void LateUpdate()
        {
            // Coalesce the journal changes and all UnityEvent actions from the same interaction.
            if (dirty)
                SaveNow();
        }

        private void OnRoomChanged(MapRoomDefinition room, MapEntryState state) => dirty = true;
        private void OnDiscoveryChanged(MapDiscoveryDefinition discovery, MapEntryState state) => dirty = true;
        private void OnResolutionChanged(MapDiscoveryDefinition discovery, bool resolved) => dirty = true;
        private void OnVariantChanged(MapRoomDefinition room, MapRoomVariantDefinition previous, MapRoomVariantDefinition current) => dirty = true;
        private void OnPuzzleSolved() => dirty = true;

        private void OnStudioSwitchChanged()
        {
            ApplyBlueprintTriggerState();
            dirty = true;
        }

        private void ApplyBlueprintTriggerState()
        {
            if (studioBlueprintTrigger != null && studioSwitch != null)
                studioBlueprintTrigger.SetActive(studioSwitch.IsOn);
            if (studioBlueprintObservation != null && studioSwitch != null)
                studioBlueprintObservation.SetObservationEnabled(studioSwitch.IsOn);
        }

        private void Subscribe()
        {
            if (subscribed || !sessionActive)
                return;
            journalState.RoomStateChanged += OnRoomChanged;
            journalState.DiscoveryStateChanged += OnDiscoveryChanged;
            journalState.DiscoveryResolutionChanged += OnResolutionChanged;
            journalState.RoomVariantChanged += OnVariantChanged;
            bellPuzzle.OnSolved.AddListener(OnPuzzleSolved);
            studioSwitch.OnTurnedOn.AddListener(OnStudioSwitchChanged);
            studioSwitch.OnTurnedOff.AddListener(OnStudioSwitchChanged);
            subscribed = true;
        }

        private void OnEnable()
        {
            if (sessionActive)
                Subscribe();
        }

        private void OnDisable()
        {
            if (!subscribed)
                return;
            if (journalState != null)
            {
                journalState.RoomStateChanged -= OnRoomChanged;
                journalState.DiscoveryStateChanged -= OnDiscoveryChanged;
                journalState.DiscoveryResolutionChanged -= OnResolutionChanged;
                journalState.RoomVariantChanged -= OnVariantChanged;
            }
            if (bellPuzzle != null) bellPuzzle.OnSolved.RemoveListener(OnPuzzleSolved);
            if (studioSwitch != null)
            {
                studioSwitch.OnTurnedOn.RemoveListener(OnStudioSwitchChanged);
                studioSwitch.OnTurnedOff.RemoveListener(OnStudioSwitchChanged);
            }
            subscribed = false;
        }

        private void OnApplicationQuit() => SaveNow();
    }
}
