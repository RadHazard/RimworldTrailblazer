using System;
using System.Reflection;
using HarmonyLib;
using Verse;
using Verse.AI;

namespace Trailblazer
{
    [StaticConstructorOnStartup]
    class HarmonyPatches
    {
        static HarmonyPatches()
        {
            var harmony = new Harmony("pausbrak.trailblazer");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            Log.Message("Trailblazer: Patching pathfinder");
        }
    }

    [HarmonyPatch(typeof(Map), "ConstructComponents")]
    static class Patch_Map_ConstructComponents
    {
        static void Postfix(Map __instance)
        {
            __instance.pathFinder = new Trailblazer(__instance);
        }
    }
}
