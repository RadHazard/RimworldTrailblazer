using Verse;
using Verse.AI;

namespace Trailblazer
{
    public static class Trailblazer
    {
        /// <summary>
        /// Stub that replaces the vanilla pathfind method. Performs error checking, then creates a TrailblazerPather
        /// to perform the actual pathfinding.
        /// </summary>
        /// <returns>The path.</returns>
        /// <param name="start">Start.</param>
        /// <param name="dest">Destination.</param>
        /// <param name="traverseParms">Traverse parms.</param>
        /// <param name="peMode">Path end mode.</param>
        public static PawnPath FindPath(Map map, IntVec3 start, LocalTargetInfo dest, TraverseParms traverseParms, PathEndMode peMode)
        {
            if (DebugSettings.pathThroughWalls)
            {
                traverseParms.mode = TraverseMode.PassAllDestroyableThings;
            }
            Pawn pawn = traverseParms.pawn;
            if (pawn != null && pawn.Map != map)
            {
                Log.Error("Tried to FindPath for pawn which is spawned in another map. His map PathFinder should have been used, not this one. pawn=" + pawn + " pawn.Map=" + pawn.Map + " map=" + map, false);
                return PawnPath.NotFound;
            }
            if (!start.IsValid)
            {
                Log.Error("Tried to FindPath with invalid start " + start + ", pawn= " + pawn, false);
                return PawnPath.NotFound;
            }
            if (!dest.IsValid)
            {
                Log.Error("Tried to FindPath with invalid dest " + dest + ", pawn= " + pawn, false);
                return PawnPath.NotFound;
            }
            if (traverseParms.mode == TraverseMode.ByPawn)
            {
                if (!pawn.CanReach(dest, peMode, Danger.Deadly, traverseParms.canBash, traverseParms.mode))
                {
                    return PawnPath.NotFound;
                }
            }
            else if (!map.reachability.CanReach(start, dest, peMode, traverseParms))
            {
                return PawnPath.NotFound;
            }

            PathfindData pathfindData = new PathfindData(map, map.GetCellRef(start), dest, traverseParms, peMode);
            return new TrailblazerPather_AStar(pathfindData).FindPath(); //TODO allow swapping out pathers for debugging
        }
    }
}
