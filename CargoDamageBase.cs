using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using DV.Damage;

namespace CargoEffects
{

    public abstract class CargoDamageBase : MonoBehaviour
    {
        protected TrainCar car;
        protected CargoDamageModel cargoDamageModel;
        protected bool HasExploded { get; private set; }

        private bool canExplode;
        private MethodInfo applyDamageMethod;
        private FieldInfo cargoDamagePropertiesField;
        private float cachedMaxHealth = -1f;
        private float cachedDamageTolerance = -1f;

        private static MethodInfo explosionMethod;
        private static bool explosionMethodSearched = false;

   
        protected bool Initialize(TrainCar car, bool canExplode)
        {
            this.car = car;
            this.canExplode = canExplode;

            cargoDamageModel = car.GetComponentInChildren<CargoDamageModel>();
            if (cargoDamageModel == null)
            {
                enabled = false;
                return false;
            }

            applyDamageMethod = typeof(CargoDamageModel).GetMethod(
                "ApplyDamageToCargo", BindingFlags.NonPublic | BindingFlags.Instance);
            if (applyDamageMethod == null)

            cargoDamagePropertiesField = typeof(CargoDamageModel).GetField(
                "cargoDamageProperties", BindingFlags.NonPublic | BindingFlags.Instance);
            if (cargoDamagePropertiesField == null)

            ResolveExplosionMethod();
            return true;
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
                return;
            }

            explosionMethod = explosionType.GetMethod("CreateExplosion", BindingFlags.Public | BindingFlags.Static);
            if (explosionMethod == null)
                Main.DebugLog("WARNING: found TrainCarExplosion, explosion method is null");
        }

        private object GetCargoDamageProperties()
        {
            if (cargoDamagePropertiesField == null) return null;
            return cargoDamagePropertiesField.GetValue(cargoDamageModel);
        }

        protected float GetMaxHealth()
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

        protected float GetDamageTolerance()
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

        protected float GetEffectiveMaxHealth()
        {
            float mh = GetMaxHealth();
            float tol = GetDamageTolerance();
            if (mh <= 0f || tol < 0f) return -1f;
            return mh * (1f - tol);
        }


        protected void ApplyDamage(string kind, float damagePercent)
        {
            if (applyDamageMethod == null)
            {
                return;
            }

            if (HasExploded || cargoDamageModel.currentDamageState == DamageState.Destroyed)
                return;

            var cfg = Main.Config;
            float effectiveMaxHealth = GetEffectiveMaxHealth();
            float rawMaxHealth = GetMaxHealth();
            float damageAmount =
                effectiveMaxHealth > 0f ? effectiveMaxHealth * damagePercent :
                rawMaxHealth > 0f ? rawMaxHealth * damagePercent :
                cfg.FallbackFixedDamage;

            try
            {
                applyDamageMethod.Invoke(cargoDamageModel, new object[] { damageAmount, true });
            }
            catch (Exception ex)
            {
                Main.DebugLog($"{car.ID}: EXCEPTION calling ApplyDamageToCargo ({kind}): {ex.InnerException ?? ex}");
                return;
            }

            float effectiveAfter = cargoDamageModel.EffectiveHealthPercentage100Notation;
            CheckExplosion(effectiveAfter / 100f);
        }

        private void CheckExplosion(float effectiveHealthPercentage)
        {
            var cfg = Main.Config;
            if (HasExploded || effectiveHealthPercentage > cfg.ExplosionHealthThreshold) return;
            HasExploded = true;
            Explode();
        }

        private void Explode()
        {
            var cfg = Main.Config;

            if (!canExplode)
            {
                cargoDamageModel.DestroyCargo();
                return;
            }


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