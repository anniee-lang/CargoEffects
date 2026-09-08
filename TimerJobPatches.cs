using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using DV.Logic.Job;
using DV.ThingTypes;
namespace CargoEffects
{
    [HarmonyPatch(typeof(JobsManager), "TakeJob")]
    public static class TimerJobTakenPatch
    {
        private static FieldInfo jobToJobCarsField;
        private static bool fieldSearched = false;
        static void Postfix(JobsManager __instance, Job job, bool takenViaLoadGame)
        {
            if (!Main.Config.UseTimerBasedDamage) return;
            if (job.jobType != JobType.Transport && job.jobType != JobType.EmptyHaul) return;
            var logicCars = GetLogicCarsForJob(__instance, job);
            if (logicCars == null || logicCars.Count == 0) return;
            var trainCars = ResolveTrainCars(logicCars);
            if (trainCars.Count == 0) return;
            foreach (var group in trainCars.Where(tc => tc != null).GroupBy(tc => tc.LoadedCargo))
            {
                if (Main.TimerCargos.TryGetValue(group.Key, out var timerEntry))
                {
                    JobTimerCargoTracker.StartTracking(job.ID, group.Key.ToString(), group.ToList(), timerEntry);
                }
            }
        }
        private static HashSet<Car> GetLogicCarsForJob(JobsManager manager, Job job)
        {
            if (!fieldSearched)
            {
                fieldSearched = true;
                jobToJobCarsField = typeof(JobsManager).GetField("jobToJobCars", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? typeof(JobsManager).GetField("jobToJobCars", BindingFlags.Public | BindingFlags.Instance);
                if (jobToJobCarsField == null)
                    Main.DebugLog("CargoEffects -timer-based damage cannot map job to cars.");
            }
            if (jobToJobCarsField == null) return null;
            if (!(jobToJobCarsField.GetValue(manager) is Dictionary<Job, HashSet<Car>> dict))
                return null;
            return dict.TryGetValue(job, out var cars) ? cars : null;
        }
        private static List<TrainCar> ResolveTrainCars(HashSet<Car> logicCars)
        {
            var result = new List<TrainCar>();
            var allCars = CarSpawner.Instance?.AllCars;
            if (allCars == null) return result;
            foreach (var logicCar in logicCars)
            {
                if (logicCar == null) continue;
                var match = allCars.FirstOrDefault(tc => tc != null && tc.ID == logicCar.ID);
                if (match != null)
                    result.Add(match);

            }
            return result;
        }
    }
    [HarmonyPatch(typeof(JobsManager), "AbandonJob")]
    public static class TimerJobAbandonedPatch
    {
        static void Postfix(Job job)
        {
            JobTimerCargoTracker.StopTracking(job.ID);
        }
    }
    [HarmonyPatch(typeof(JobValidator), nameof(JobValidator.ValidateJob))]
    public static class TimerJobValidatorPatch
    {
        static void Postfix(JobBooklet jobBooklet)
        {
            if (jobBooklet?.job == null) return;
            JobTimerCargoTracker.StopTracking(jobBooklet.job.ID);
        }
    }
}