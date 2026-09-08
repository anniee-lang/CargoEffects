using System.Collections.Generic;
using UnityEngine;
using DV.Damage;

namespace CargoEffects
{
    public class AccelerationCargoDamage : CargoDamageBase
    {
        private TrainStress trainStress;
        private float lastDamageTime = -999f;
        private float lastStressDamageTime = -999f;
        private float sampleTimer = 0f;

        private struct Sample { public float time; public float speedKmh; }
        private readonly Queue<Sample> samples = new Queue<Sample>();

        public void Setup(TrainCar car, CargoConfigEntry entry)
        {
            if (!Initialize(car, entry.CanExplode)) return;

            trainStress = car.stress;
            if (trainStress == null)
                Main.DebugLog($"{car.ID}: WARNING - TrainCar.stress is null, stress-based damage disabled for this car.");
        }

        void Update()
        {
            if (car == null || cargoDamageModel == null) return;

            var cfg = Main.Config;

            sampleTimer += Time.deltaTime;
            if (sampleTimer < cfg.SampleIntervalSeconds) return;
            sampleTimer = 0f;

            float speedKmh = car.GetAbsSpeed() * 3.6f;
            float now = Time.time;

            samples.Enqueue(new Sample { time = now, speedKmh = speedKmh });
            while (samples.Count > 0 && now - samples.Peek().time > cfg.SpeedWindowSeconds)
                samples.Dequeue();

            if (samples.Count > 0 && now - lastDamageTime >= cfg.DamageCooldown)
            {
                float delta = speedKmh - samples.Peek().speedKmh;
                if (delta >= cfg.SpeedGainThresholdKmh)
                    TriggerDamage("acceleration", cfg.DamagePercentPerTrigger);
                else if (delta <= -cfg.SpeedLossThresholdKmh)
                    TriggerDamage("deceleration", cfg.DamagePercentPerTrigger);
            }

            if (trainStress != null && now - lastStressDamageTime >= cfg.StressDamageCooldown)
            {
                float currentStress = trainStress.stress;
                if (currentStress >= cfg.StressDamageThreshold)
                {
                    lastStressDamageTime = now;
                    TriggerDamage("stress", cfg.StressDamagePercentPerTrigger);
                }
            }
        }

        private void TriggerDamage(string kind, float damagePercent)
        {
            if (HasExploded || cargoDamageModel.currentDamageState == DamageState.Destroyed)
                return;

            lastDamageTime = Time.time;
            ApplyDamage(kind, damagePercent);
        }
    }
}
