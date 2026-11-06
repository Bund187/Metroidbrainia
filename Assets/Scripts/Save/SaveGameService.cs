using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Metroidbrainia
{
    public sealed class SaveGameService
    {
        public string SavePath { get; }

        public SaveGameService(string directory = null)
        {
            SavePath = Path.Combine(directory ?? Application.persistentDataPath, "savegame.json");
        }

        public bool HasSave() => File.Exists(SavePath);

        public SaveGameData Load()
        {
            try
            {
                SaveGameData data = JsonUtility.FromJson<SaveGameData>(File.ReadAllText(SavePath));
                if (!Validate(data, out string error))
                    throw new InvalidDataException(error);
                return data;
            }
            catch (Exception error)
            {
                Debug.LogError($"Cannot load '{SavePath}': {error.Message}");
                return null;
            }
        }

        public bool Save(SaveGameData data)
        {
            if (!Validate(data, out string validationError))
            {
                Debug.LogError($"Cannot save progress: {validationError}");
                return false;
            }
            string temporaryPath = SavePath + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
                byte[] bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(data, true));
                using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                // Both files are in the same directory. Never delete the previous save before replacement.
                if (File.Exists(SavePath))
                    File.Replace(temporaryPath, SavePath, null);
                else
                    File.Move(temporaryPath, SavePath);
                return true;
            }
            catch (Exception error)
            {
                Debug.LogError($"Cannot write '{SavePath}'; the previous save was preserved: {error.Message}");
                return false;
            }
        }

        public bool DeleteSave()
        {
            try
            {
                // A leftover temporary file is never treated as a valid slot.
                File.Delete(SavePath + ".tmp");
                File.Delete(SavePath);
                return true;
            }
            catch (Exception error)
            {
                Debug.LogError($"Cannot delete '{SavePath}': {error.Message}");
                return false;
            }
        }

        public static bool Validate(SaveGameData data, out string error)
        {
            error = null;
            if (data == null || data.version != SaveGameData.CurrentVersion)
                error = "Missing or unsupported save version (expected version 1).";
            else if (data.map == null || data.world == null || data.map.rooms == null || data.map.discoveries == null)
                error = "Missing map or world data.";
            if (error != null)
                return false;

            var roomIds = new HashSet<string>(StringComparer.Ordinal);
            var discoveryIds = new HashSet<string>(StringComparer.Ordinal);
            var order = new HashSet<int>();
            foreach (RoomProgressEntry entry in data.map.rooms)
            {
                if (entry == null || !ValidEntry(entry.id, entry.state, entry.discoveryOrder, roomIds, order)
                    || (!entry.hasExplicitVariant && !string.IsNullOrEmpty(entry.variantId)))
                {
                    error = "Invalid or duplicate room entry, state, variant or discovery order.";
                    return false;
                }
            }
            foreach (DiscoveryProgressEntry entry in data.map.discoveries)
            {
                if (entry == null || !ValidEntry(entry.id, entry.state, entry.discoveryOrder, discoveryIds, order))
                {
                    error = "Invalid or duplicate discovery entry, state or discovery order.";
                    return false;
                }
            }
            return true;
        }

        private static bool ValidEntry(string id, MapEntryState state, int index,
            HashSet<string> ids, HashSet<int> order)
        {
            if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)
                || state < MapEntryState.Undiscovered || state > MapEntryState.Revealed)
                return false;
            return state == MapEntryState.Undiscovered ? index == -1 : index >= 0 && order.Add(index);
        }
    }
}
