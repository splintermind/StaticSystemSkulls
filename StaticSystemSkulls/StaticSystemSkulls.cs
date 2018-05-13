using BattleTech;
using BattleTech.Save;
using BattleTech.UI;
using Harmony;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using HBS.Logging;

namespace StaticSystemSkulls
{
    public class StaticSystemSkulls
    {
        private static ILog m_log = HBS.Logging.Logger.GetLogger(typeof(StaticSystemSkulls).Name, LogLevel.Log);

        public static void Init()
        {
            var harmony = HarmonyInstance.Create("io.github.splintermind.StaticSystemSkulls");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        public static void Log(string message)
        {
            m_log.Log(message);
        }
    }

    [HarmonyPatch(typeof(BattleTech.SimGameState), "GetDifficultyRangeForContract")]
    public static class BattleTech_SimGameState_GetDifficultyRangeForContract_Patch
    {
        // remove the global difficulty from the contract difficulty calculation and instead use the system difficulty
        // system difficulty is in skulls (1-5) but contracts are 1-10, so *2 afterwards. ie. a 2.5 skulls = 5 difficulty
        private static void Prefix(SimGameState __instance, ref int baseDiff)
        {
            baseDiff = (baseDiff - Mathf.FloorToInt(__instance.GlobalDifficulty)) * 2;
        }
    }

    [HarmonyPatch(typeof(BattleTech.SimGameState), "Rehydrate")]
    public static class BattleTech_SimGameState_Rehydrate_Patch
    {
        // these are the difficulty modifiers and their related story milestones that trigger them
        private static readonly SortedList<int, string> m_milestones = new SortedList<int, string>
        {
            {8,"milestone_700_notify_complete"},
            {7,"milestone_604_sim_argo"},
            {6,"milestone_425_sim_argo"},
            {5,"milestone_356_sim_argo"},
            {4,"milestone_305_sim_argo_start"},
            {3,"milestone_203_sim_leopard"},
            {2,"milestone_114_sim_leopard_start"}
        };

        private static void Prefix(SimGameState __instance, GameInstanceSave gameInstanceSave)
        {
            // check the consumed milestones in the save file, and reset the save file's difficulty according to the highest milestone trigger
            StaticSystemSkulls.Log("Validating save file global difficulty...");
            float currentDiff = gameInstanceSave.SimGameSave.CompanyStats.GetValue<float>("Difficulty");
            float newDiff = 1;
            List<string> consumed = gameInstanceSave.SimGameSave.ConsumedMilestones;

            for (int idx = m_milestones.Count - 1; idx >= 0; idx--)
            {
                if (consumed.Contains(m_milestones.Values[idx]))
                {
                    StaticSystemSkulls.Log("  last critical story milestone " + m_milestones.Values[idx].ToString() + " " + m_milestones.Keys[idx].ToString());
                    newDiff = m_milestones.Keys[idx];
                    break;
                }
            }

            if (currentDiff != newDiff)
            {
                gameInstanceSave.SimGameSave.CompanyStats.Set("Difficulty", newDiff);
                StaticSystemSkulls.Log(string.Format("  reset global difficulty from {0} to {1}.", currentDiff, newDiff));
            }
            else
            {
                StaticSystemSkulls.Log("  no global difficulty change required.");
            }

        }
    }

    [HarmonyPatch(typeof(BattleTech.UI.SGSystemViewPopulator), "UpdateRoutedSystem")]
    public static class BattleTech_UI_SGSystemViewPopulator_Patch
    {
        private static void Postfix(BattleTech.UI.SGSystemViewPopulator __instance)
        {

            //StaticSystemSkulls.Log("Refreshing system skull indicators...");
            //DateTime start = DateTime.Now;
            int sysDiff = Traverse.Create(__instance).Field("starSystem").Property("Def").Property("Difficulty").GetValue<int>();
            List<SGDifficultyIndicatorWidget> widgets = Traverse.Create(__instance).Field("SystemDifficultyWidget").GetValue<List<SGDifficultyIndicatorWidget>>();
            if (widgets != null)
            {
                int normalizedDifficulty = Mathf.Clamp(Mathf.RoundToInt((float)sysDiff * 2f), 1, 10);
                widgets.ForEach(delegate (SGDifficultyIndicatorWidget widget)
                {
                    widget.SetDifficulty(normalizedDifficulty);
                });
            }
            //TimeSpan diff = DateTime.Now - start;
            //StaticSystemSkulls.Log("  completed in " + diff.TotalMilliseconds + " ms.");
        }
    }

}