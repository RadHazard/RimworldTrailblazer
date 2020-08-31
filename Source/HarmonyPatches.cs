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

            Log.Message("[Trailblazer] Patching pathfinder");
        }
    }

    [HarmonyPatch(typeof(PathFinder))]
    [HarmonyPatch("FindPath", new Type[] { typeof(IntVec3), typeof(LocalTargetInfo), typeof(TraverseParms), typeof(PathEndMode) })]
    static class Patch_PathFinder_FindPath
    {
        static bool Prefix(IntVec3 start, LocalTargetInfo dest, TraverseParms traverseParms, PathEndMode peMode, Map ___map, ref PawnPath __result)
        {
            __result = Trailblazer.FindPath(___map, start, dest, traverseParms, peMode);
            return false;
        }
    }
}
