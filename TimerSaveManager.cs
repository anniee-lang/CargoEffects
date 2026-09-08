using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;

namespace CargoEffects
{
    public class TimerSaveEntry
    {
        public string JobId = "";
        public string CargoType = "";
        public float ElapsedSeconds = 0f;
    }

    public class TimerSaveFile
    {
        public List<TimerSaveEntry> Entries = new List<TimerSaveEntry>();
    }

    public static class TimerSaveManager
    {
        private static string SavePath =>
            Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "timersave.json");

        private static readonly Dictionary<string, float> pendingEntries = new Dictionary<string, float>();

        public static void LoadFromDisk()
        {
            pendingEntries.Clear();
            try
            {
                string path = SavePath;
                if (!File.Exists(path)) return;

                string json = File.ReadAllText(path);
                var loaded = JsonConvert.DeserializeObject<TimerSaveFile>(json);
                if (loaded?.Entries == null) return;

                foreach (var e in loaded.Entries)
                {
                    if (string.IsNullOrEmpty(e.JobId) || string.IsNullOrEmpty(e.CargoType)) continue;
                    pendingEntries[Key(e.JobId, e.CargoType)] = e.ElapsedSeconds;
                }

                Main.DebugLog($"TimerSaveManager: loaded {pendingEntries.Count} saved timer entry(ies) from {path}.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[CargoEffects] Failed to load timersave.json: " + ex);
                pendingEntries.Clear();
            }
        }

        public static bool TryConsumeElapsed(string jobId, string cargoType, out float elapsedSeconds)
        {
            string key = Key(jobId, cargoType);
            if (pendingEntries.TryGetValue(key, out elapsedSeconds))
            {
                pendingEntries.Remove(key);
                return true;
            }
            elapsedSeconds = 0f;
            return false;
        }

        public static void SaveToDisk(IEnumerable<TimerSaveEntry> entries)
        {
            try
            {
                var file = new TimerSaveFile { Entries = entries.ToList() };
                File.WriteAllText(SavePath, JsonConvert.SerializeObject(file, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Debug.LogError("[CargoEffects] Failed to write timersave.json: " + ex);
            }
        }

        private static string Key(string jobId, string cargoType) => jobId + "::" + cargoType;
    }
}