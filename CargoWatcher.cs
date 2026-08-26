using UnityEngine;
using DV;

namespace CargoEffects
{
    public class CargoWatcher : MonoBehaviour
    {
        private float timer = 0f;

        public static void Initialize()
        {
            var go = new GameObject("CargoEffectsWatcher");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<CargoWatcher>();
            Main.DebugLog("CargoWatcher started.");
        }

        void Update()
        {
            if (!Main.Enabled) return;
            timer += Time.deltaTime;
            if (timer < Main.Config.CargoWatcherCheckInterval) return;
            timer = 0f;
            CheckForTargetCargo();
        }

        private void CheckForTargetCargo()
        {
            var allCars = CarSpawner.Instance?.AllCars;
            if (allCars == null) return;

            foreach (var car in allCars)
            {
                if (car == null || car.IsLoco) continue;

                var tracker = car.GetComponent<AccelerationCargoDamage>();

                if (Main.TargetCargos.TryGetValue(car.LoadedCargo, out var entry))
                {
                    if (tracker == null)
                    {
                        Main.DebugLog($"Found target cargo '{car.LoadedCargo}' on car {car.ID} (CanExplode={entry.CanExplode}), attaching tracker.");
                        tracker = car.gameObject.AddComponent<AccelerationCargoDamage>();
                        tracker.Setup(car, entry);
                    }
                }
                else if (tracker != null)
                {
                    Main.DebugLog($"{car.ID} no longer has a tracked cargo, removing tracker.");
                    Destroy(tracker);
                }
            }
        }
    }
}
