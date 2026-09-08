using UnityEngine;
using DV.Damage;

namespace CargoEffects
{

    public class JobTimerCargoDamage : CargoDamageBase
    {
        private TimerCargoConfigEntry timerConfig;
        private float lastDamageTime = -999f;

        public void Setup(TrainCar car, TimerCargoConfigEntry config)
        {
            timerConfig = config;
            Initialize(car, config.CanExplode);
        }

        void Update()
        {
            if (car == null || cargoDamageModel == null || timerConfig == null) return;

            if (HasExploded || cargoDamageModel.currentDamageState == DamageState.Destroyed)
            {
                enabled = false;
                return;
            }

            if (Time.time - lastDamageTime < timerConfig.TimerDamageCooldown) return;
            lastDamageTime = Time.time;

            ApplyDamage("timer", timerConfig.TimerDamagePercentPerTrigger);
        }
    }
}