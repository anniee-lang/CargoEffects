using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;

namespace CargoEffects
{
    public class CargoConfigEntry
    {
        public string CargoType = "";
        public bool CanExplode = false;
    }

    public class ModConfig
    {
        public List<CargoConfigEntry> Cargos = new List<CargoConfigEntry>
        {
            //new CargoConfigEntry { CargoType = "Gasoline", CanExplode = true },
            new CargoConfigEntry { CargoType = "Goats", CanExplode = false },
            new CargoConfigEntry { CargoType = "Pigs", CanExplode = false },
            new CargoConfigEntry { CargoType = "Poultry", CanExplode = false },
            new CargoConfigEntry { CargoType = "Sheep", CanExplode = false },
            new CargoConfigEntry { CargoType = "Passengers", CanExplode = false },
            new CargoConfigEntry { CargoType = "Cows", CanExplode = false },
        };

        public float SpeedWindowSeconds = 20f;
        public float SpeedGainThresholdKmh = 50f;
        public float SpeedLossThresholdKmh = 50f;
        public float DamageCooldown = 5f;
        public float DamagePercentPerTrigger = 0.20f;
        public float FallbackFixedDamage = 50f;
        public float SampleIntervalSeconds = 0.5f;
        public float CargoWatcherCheckInterval = 2.0f;

        public float ExplosionHealthThreshold = 0.5f;
        public float ExplosionDamage = 10000000f;
        public float ExplosionRadius = 25f;
        public float ExplosionForce = 100f;

        public float StressDamageThreshold = 0.9f;
        public float StressDamageCooldown = 5f;
        public float StressDamagePercentPerTrigger = 0.20f;

        private static string ConfigPath =>
            Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json");

        public static ModConfig LoadOrCreate()
        {
            try
            {
                string path = ConfigPath;
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var loaded = JsonConvert.DeserializeObject<ModConfig>(json);
                    if (loaded != null)
                    {
                        Main.DebugLog($"Config loaded from {path}");
                        return loaded;
                    }
                }

                var defaults = new ModConfig();
                File.WriteAllText(path, JsonConvert.SerializeObject(defaults, Formatting.Indented));
                Main.DebugLog($"No config found, wrote defaults to {path}");
                return defaults;
            }
            catch (Exception ex)
            {
                Debug.LogError("[CargoEffects] Failed to load/create config.json, using in-memory defaults: " + ex);
                return new ModConfig();
            }
        }
    }
}
