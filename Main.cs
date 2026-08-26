using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;
using DV.ThingTypes;

namespace CargoEffects
{
    public static class Main
    {
        public static UnityModManager.ModEntry mod;
        public static bool Enabled = true;
        public static ModConfig Config;
        public static Dictionary<CargoType, CargoConfigEntry> TargetCargos = new Dictionary<CargoType, CargoConfigEntry>();

        private static bool Load(UnityModManager.ModEntry modEntry)
        {
            Harmony harmony = null;
            try
            {
                mod = modEntry;
                modEntry.OnToggle = OnToggle;

                Config = ModConfig.LoadOrCreate();

                TargetCargos.Clear();
                foreach (var entry in Config.Cargos)
                {
                    if (Enum.TryParse<CargoType>(entry.CargoType, ignoreCase: true, out var parsed))
                    {
                        TargetCargos[parsed] = entry;
                    }
                    else
                    {
                        Debug.LogError($"[CargoEffects] Config cargo entry '{entry.CargoType}' is not a valid CargoType, skipping.");
                    }
                }

                harmony = new Harmony(modEntry.Info.Id);
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                CargoWatcher.Initialize();
                DebugLog($"Loaded. Watching {TargetCargos.Count} cargo type(s): " + string.Join(", ", TargetCargos.Keys));
                return true;
            }
            catch (Exception ex)
            {
                modEntry.Logger.LogException("Failed to load " + modEntry.Info.DisplayName + ":", ex);
                if (harmony != null)
                    harmony.UnpatchAll(modEntry.Info.Id);
                return false;
            }
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            Enabled = value;
            return true;
        }

        public static void DebugLog(string message)
        {
            Debug.Log("[CargoEffects] " + message);
        }
    }
}
