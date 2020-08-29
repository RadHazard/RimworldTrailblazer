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

    //TODO replace the whole pathfinder with Trailblazer
    //[HarmonyPatch(typeof(PathFinder), "FindPath", new Type[] { typeof(IntVec3), typeof(LocalTargetInfo), typeof(TraverseParms), typeof(PathEndMode) })]
    //static class Patch_PawnUtility_GetAvoidGrid
    //{
    //    static bool Prefix(IntVec3 start, LocalTargetInfo dest, TraverseParms traverseParms, PathEndMode peMode, ref PawnPath __result)
    //    {
    //        __result = Trailblazer.FindPath(start, dest, traverseParms, peMode);
    //        return false;
    //    }
    //}
}
