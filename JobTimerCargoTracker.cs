//  Portions of this code are adapted from the "ZG2 Zugschlusstafeln 
//  (Train End Boards)" mod created by Fuggschen (c) 2026.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static UnityModManagerNet.UnityModManager;


namespace CargoEffects
{

    public class JobTimerCargoTracker : MonoBehaviour
    {
        private static JobTimerCargoTracker instance;

        private class TimerEntry
        {
            public string JobId;
            public string CargoType;
            public List<TrainCar> Cars;
            public TimerCargoConfigEntry Config;
            public float ElapsedSeconds;
            public bool DamagingActive;
            public readonly List<JobTimerCargoDamage> DamageComponents = new List<JobTimerCargoDamage>();
        }

        private readonly Dictionary<string, TimerEntry> activeTimers = new Dictionary<string, TimerEntry>();
        private float saveAccumulator = 0f;


        public static void Initialize()
        {
            if (instance != null) return;
            var go = new GameObject("CargoEffectsJobTimerTracker");
            UnityEngine.Object.DontDestroyOnLoad(go);
            instance = go.AddComponent<JobTimerCargoTracker>();
            TimerSaveManager.LoadFromDisk();
            Main.DebugLog("JobTimerCargoTracker started.");
        }

        public static void StartTracking(string jobId, string cargoType, List<TrainCar> cars, TimerCargoConfigEntry config)
        {
            if (instance == null || cars == null || cars.Count == 0) return;

            string key = jobId + "::" + cargoType;
            if (instance.activeTimers.ContainsKey(key)) return;

            float startElapsed = 0f;
            bool resumed = TimerSaveManager.TryConsumeElapsed(jobId, cargoType, out startElapsed);

            var entry = new TimerEntry
            {
                JobId = jobId,
                CargoType = cargoType,
                Cars = cars,
                Config = config,
                ElapsedSeconds = startElapsed,
                DamagingActive = false
            };
            instance.activeTimers[key] = entry;

            Main.DebugLog(resumed
                ? $"Job {jobId}: resumed timer for cargo '{cargoType}' at {startElapsed:F0}s ({cars.Count} car(s))."
                : $"Job {jobId}: started timer for cargo '{cargoType}' ({cars.Count} car(s)), damage starts after {config.TimerThresholdMinutes:F1} min.");
        }

        public static void StopTracking(string jobId)
        {
            if (instance == null) return;

            var keys = instance.activeTimers.Keys.Where(k => k.StartsWith(jobId + "::")).ToList();
            foreach (var key in keys)
            {
                var entry = instance.activeTimers[key];
                foreach (var dmg in entry.DamageComponents)
                    if (dmg != null) Destroy(dmg);

                instance.activeTimers.Remove(key);
                Main.DebugLog($"Job {entry.JobId}: stopped timer for cargo '{entry.CargoType}'.");
            }

            if (keys.Count > 0) instance.SaveNow();
        }

        void Update()
        {
            if (!Main.Enabled || !Main.Config.UseTimerBasedDamage || activeTimers.Count == 0) return;

            float dt = Time.deltaTime;
            foreach (var entry in activeTimers.Values.ToList())
            {
                entry.ElapsedSeconds += dt;
                if (entry.DamagingActive) continue;

                float thresholdSeconds = entry.Config.TimerThresholdMinutes * 60f;
                if (entry.ElapsedSeconds < thresholdSeconds) continue;

                entry.DamagingActive = true;
                Main.DebugLog($"Job {entry.JobId}: cargo '{entry.CargoType}' exceeded {entry.Config.TimerThresholdMinutes:F1} min threshold, starting timer-based damage on {entry.Cars.Count} car(s).");

                foreach (var car in entry.Cars)
                {
                    if (car == null) continue;
                    var dmgComp = car.gameObject.AddComponent<JobTimerCargoDamage>();
                    dmgComp.Setup(car, entry.Config);
                    entry.DamageComponents.Add(dmgComp);
                }
            }

            saveAccumulator += dt;
            if (saveAccumulator >= Main.Config.TimerSaveIntervalSeconds)
            {
                saveAccumulator = 0f;
                SaveNow();
            }
        }

        private void SaveNow()
        {
            var entries = activeTimers.Values.Select(e => new TimerSaveEntry
            {
                JobId = e.JobId,
                CargoType = e.CargoType,
                ElapsedSeconds = e.ElapsedSeconds
            });
            TimerSaveManager.SaveToDisk(entries);
        }

        void OnApplicationQuit()
        {
            SaveNow();
        }

        void OnGUI()
        {
            if (!Main.Enabled || !Main.Config.UseTimerBasedDamage || activeTimers.Count == 0) return;

            var style = new GUIStyle(GUI.skin.box) { fontSize = 14, alignment = TextAnchor.MiddleLeft };
            float y = 10f;

            foreach (var entry in activeTimers.Values)
            {
                string label;
                if (entry.DamagingActive)
                {
                    label = $"{entry.CargoType} ({entry.Cars.Count} cars) - TAKING DAMAGE [{TimeSpan.FromSeconds(entry.ElapsedSeconds):mm\\:ss}]";
                }
                else
                {
                    float remaining = Mathf.Max(0f, entry.Config.TimerThresholdMinutes * 60f - entry.ElapsedSeconds);
                    label = $"{entry.CargoType} ({entry.Cars.Count} cars) - {TimeSpan.FromSeconds(remaining):mm\\:ss} until damage";
                }

                GUI.Box(new Rect(10, y, 340, 26), label, style);
                y += 30f;
            }
        }
    }
}