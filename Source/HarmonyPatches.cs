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
        //TODO drop the main body of this into Trailblazer and just keep this patch minimal
    /// <summary>
    /// Stub that replaces the vanilla pathfind method. Performs error checking, then creates a TrailblazerPather
    /// to perform the actual pathfinding.
    /// </summary>
    /// <returns>The path.</returns>
    /// <param name="start">Start.</param>
    /// <param name="dest">Destination.</param>
    /// <param name="traverseParms">Traverse parms.</param>
    /// <param name="peMode">Path end mode.</param>
    [HarmonyPatch(typeof(PathFinder))]
    [HarmonyPatch("FindPath", new Type[] { typeof(IntVec3), typeof(LocalTargetInfo), typeof(TraverseParms), typeof(PathEndMode) })]
    static class Patch_PathFinder_FindPath
    {
        static bool Prefix(IntVec3 start, LocalTargetInfo dest, TraverseParms traverseParms, PathEndMode peMode, Map ___map, ref PawnPath __result)
        {
            if (DebugSettings.pathThroughWalls)
            {
                traverseParms.mode = TraverseMode.PassAllDestroyableThings;
            }
            Pawn pawn = traverseParms.pawn;
            if (pawn != null && pawn.Map != ___map)
            {
                Log.Error("Tried to FindPath for pawn which is spawned in another map. His map PathFinder should have been used, not this one. pawn=" + pawn + " pawn.Map=" + pawn.Map + " map=" + ___map, false);
                __result = PawnPath.NotFound;
            }
            if (!start.IsValid)
            {
                Log.Error("Tried to FindPath with invalid start " + start + ", pawn= " + pawn, false);
                __result = PawnPath.NotFound;
            }
            if (!dest.IsValid)
            {
                Log.Error("Tried to FindPath with invalid dest " + dest + ", pawn= " + pawn, false);
                __result = PawnPath.NotFound;
            }
            if (traverseParms.mode == TraverseMode.ByPawn)
            {
                if (!pawn.CanReach(dest, peMode, Danger.Deadly, traverseParms.canBash, traverseParms.mode))
                {
                    __result = PawnPath.NotFound;
                }
            }
            else if (!___map.reachability.CanReach(start, dest, peMode, traverseParms))
            {
                __result = PawnPath.NotFound;
            }

            //TODO this might be threadable
            __result = new TrailblazerPather_AStar(___map).FindPath(start, dest, traverseParms, peMode);
            return false;
        }
    }
}
