using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using DV.Damage;

namespace CargoEffects
{
    public class AccelerationCargoDamage : MonoBehaviour
    {
        private TrainCar car;
        private CargoDamageModel cargoDamageModel;
        private CargoConfigEntry cargoEntry;
        private TrainStress trainStress;
        private MethodInfo applyDamageMethod;
        private FieldInfo cargoDamagePropertiesField;
        private float cachedMaxHealth = -1f;
        private float cachedDamageTolerance = -1f;
        private float lastDamageTime = -999f;
        private float lastStressDamageTime = -999f;
        private float sampleTimer = 0f;
        private bool hasExploded = false;

        private static MethodInfo explosionMethod;
        private static bool explosionMethodSearched = false;

        private struct Sample { public float time; public float speedKmh; }
        private readonly Queue<Sample> samples = new Queue<Sample>();

        public void Setup(TrainCar car, CargoConfigEntry entry)
        {
            this.car = car;
            this.cargoEntry = entry;

            cargoDamageModel = car.GetComponentInChildren<CargoDamageModel>();
            if (cargoDamageModel == null)
            {
                Main.DebugLog($"No CargoDamageModel on {car.ID}, disabling tracker.");
                enabled = false;
                return;
            }

            trainStress = car.stress;
            if (trainStress == null)
                Main.DebugLog($"{car.ID}: WARNING - TrainCar.stress is null, stress-based damage disabled for this car.");

            applyDamageMethod = typeof(CargoDamageModel).GetMethod(
                "ApplyDamageToCargo", BindingFlags.NonPublic | BindingFlags.Instance);
            if (applyDamageMethod == null)
                Main.DebugLog($"{car.ID}: WARNING - could not find ApplyDamageToCargo via reflection.");

            cargoDamagePropertiesField = typeof(CargoDamageModel).GetField(
                "cargoDamageProperties", BindingFlags.NonPublic | BindingFlags.Instance);
            if (cargoDamagePropertiesField == null)
                Main.DebugLog($"{car.ID}: WARNING - could not find cargoDamageProperties field via reflection.");

            ResolveExplosionMethod();

            float mh = GetMaxHealth();
            float tol = GetDamageTolerance();
            float emh = GetEffectiveMaxHealth();
            //Main.DebugLog($"{car.ID}: tracker attached (CanExplode={entry.CanExplode}). " +
            //    $"maxHealth={(mh > 0f ? mh.ToString("F1") : "UNKNOWN")}, damageTolerance={(tol >= 0f ? tol.ToString("P0") : "UNKNOWN")}, " +
            //    $"effectiveMaxHealth={(emh > 0f ? emh.ToString("F1") : "UNKNOWN")}");
        }

        private static void ResolveExplosionMethod()
        {
            if (explosionMethodSearched) return;
            explosionMethodSearched = true;

            Type explosionType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .FirstOrDefault(t => t.Name == "TrainCarExplosion");

            if (explosionType == null)
            {
                Main.DebugLog("WARNING: could not find type 'TrainCarExplosion' - explosion will fall back to DestroyCargo only.");
                return;
            }

            explosionMethod = explosionType.GetMethod("CreateExplosion", BindingFlags.Public | BindingFlags.Static);
            if (explosionMethod == null)
                Main.DebugLog("WARNING: found TrainCarExplosion but not static method CreateExplosion.");
        }

        private object GetCargoDamageProperties()
        {
            if (cargoDamagePropertiesField == null) return null;
            return cargoDamagePropertiesField.GetValue(cargoDamageModel);
        }

        private float GetMaxHealth()
        {
            if (cachedMaxHealth > 0f) return cachedMaxHealth;

            object propsObj = GetCargoDamageProperties();
            if (propsObj == null) return -1f;

            Type propsType = propsObj.GetType();
            FieldInfo field = propsType.GetField("maxHealth", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            PropertyInfo prop = propsType.GetProperty("maxHealth", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            object val = field != null ? field.GetValue(propsObj) : prop?.GetValue(propsObj);
            if (val is float f) cachedMaxHealth = f;
            return cachedMaxHealth;
        }

        private float GetDamageTolerance()
        {
            if (cachedDamageTolerance >= 0f) return cachedDamageTolerance;

            object propsObj = GetCargoDamageProperties();
            if (propsObj == null) return -1f;

            Type propsType = propsObj.GetType();
            FieldInfo field = propsType.GetField("damageTolerance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            PropertyInfo prop = propsType.GetProperty("damageTolerance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            object val = field != null ? field.GetValue(propsObj) : prop?.GetValue(propsObj);
            if (val is float f) cachedDamageTolerance = f;
            return cachedDamageTolerance;
        }

        private float GetEffectiveMaxHealth()
        {
            float mh = GetMaxHealth();
            float tol = GetDamageTolerance();
            if (mh <= 0f || tol < 0f) return -1f;
            return mh * (1f - tol);
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
                    TriggerDamage("acceleration", delta, cfg.DamagePercentPerTrigger);
                else if (delta <= -cfg.SpeedLossThresholdKmh)
                    TriggerDamage("deceleration", delta, cfg.DamagePercentPerTrigger);
            }

            if (trainStress != null && now - lastStressDamageTime >= cfg.StressDamageCooldown)
            {
                float currentStress = trainStress.stress;
                if (currentStress >= cfg.StressDamageThreshold)
                {
                    lastStressDamageTime = now;
                    TriggerDamage("stress", currentStress, cfg.StressDamagePercentPerTrigger);
                }
            }
        }

        private void TriggerDamage(string kind, float value, float damagePercent)
        {
            if (applyDamageMethod == null)
            {
                Main.DebugLog($"{car.ID}: ERROR - applyDamageMethod is null, reflection lookup failed earlier!");
                return;
            }

            if (hasExploded || cargoDamageModel.currentDamageState == DamageState.Destroyed)
                return;

            lastDamageTime = Time.time;
            var cfg = Main.Config;

            
            float effectiveMaxHealth = GetEffectiveMaxHealth();
            float rawMaxHealth = GetMaxHealth();
            float damageAmount =
                effectiveMaxHealth > 0f ? effectiveMaxHealth * damagePercent :
                rawMaxHealth > 0f ? rawMaxHealth * damagePercent :
                cfg.FallbackFixedDamage;

            float healthBefore = cargoDamageModel.HealthPercentage;
            float effectiveBefore = cargoDamageModel.EffectiveHealthPercentage100Notation;

            try
            {
                applyDamageMethod.Invoke(cargoDamageModel, new object[] { damageAmount, true });
            }
            catch (Exception ex)
            {
                Main.DebugLog($"{car.ID}: EXCEPTION calling ApplyDamageToCargo: {ex.InnerException ?? ex}");
                return;
            }

            float healthAfter = cargoDamageModel.HealthPercentage;
            float effectiveAfter = cargoDamageModel.EffectiveHealthPercentage100Notation;

            //Main.DebugLog(
            //    $"{car.ID}: {kind} of {value:F1} -> applied {damageAmount:F1} dmg " +
            //    $"({damagePercent:P0} of effectiveMaxHealth {effectiveMaxHealth:F1}). " +
            //    $"Raw HP%: {healthBefore:P1} -> {healthAfter:P1}, Effective%: {effectiveBefore:F1} -> {effectiveAfter:F1}, " +
            //    $"state: {cargoDamageModel.currentDamageState}");

            CheckExplosion(effectiveAfter / 100f);
        }

        private void CheckExplosion(float effectiveHealthPercentage)
        {
            var cfg = Main.Config;
            if (hasExploded || effectiveHealthPercentage > cfg.ExplosionHealthThreshold) return;
            hasExploded = true;
            Explode();
        }

        private void Explode()
        {
            var cfg = Main.Config;

            if (!cargoEntry.CanExplode)
            {
                Main.DebugLog($"{car.ID}: cargo ruined (effective health at/below {cfg.ExplosionHealthThreshold:P0}) - CanExplode=false, destroying without explosion.");
                cargoDamageModel.DestroyCargo();
                return;
            }

            Main.DebugLog($"{car.ID}: !!! CARGO EXPLODED !!! (effective health at/below {cfg.ExplosionHealthThreshold:P0})");

            if (explosionMethod != null)
            {
                try
                {
                    explosionMethod.Invoke(null, new object[]
                    {
                        cfg.ExplosionDamage,
                        car.transform.position,
                        cfg.ExplosionRadius,
                        -1f,
                        cfg.ExplosionForce
                    });
                }
                catch (Exception ex)
                {
                    Main.DebugLog($"{car.ID}: EXCEPTION calling TrainCarExplosion.CreateExplosion: {ex.InnerException ?? ex}");
                }
            }
            else
            {
                Main.DebugLog($"{car.ID}: explosion method unavailable, skipping visual explosion.");
            }

            cargoDamageModel.DestroyCargo();
        }
    }
}
