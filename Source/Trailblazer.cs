using Verse;
using Verse.AI;

namespace Trailblazer
{
    /// <summary>
    /// Stub class that replaces the vanilla PathFinder class.  Each pathfinding attempt creates a new TrailblazerPather
    /// that performs the actual pathfinding work
    /// </summary>
    public class Trailblazer : PathFinder
    {
        private readonly Map map;
        private readonly TrailblazerPather pather;

        public Trailblazer(Map map) : base(map)
        {
            this.map = map;
            pather = new TrailblazerPather_AStar(map);
        }

        /// <summary>
        /// Stub that replaces the vanilla pathfind method. Performs error checking, then creates a TrailblazerPather
        /// to perform the actual pathfinding.
        /// </summary>
        /// <returns>The path.</returns>
        /// <param name="start">Start.</param>
        /// <param name="dest">Destination.</param>
        /// <param name="pawn">Pawn.</param>
        /// <param name="peMode">Pe mode.</param>
        public new PawnPath FindPath(IntVec3 start, LocalTargetInfo dest, Pawn pawn, PathEndMode peMode = PathEndMode.OnCell)
        {
            bool canBash = pawn?.CurJob?.canBash ?? false;
            return FindPath(start, dest, TraverseParms.For(pawn, Danger.Deadly, TraverseMode.ByPawn, canBash), peMode);
        }


        /// <summary>
        /// Stub that replaces the vanilla pathfind method. Performs error checking, then creates a TrailblazerPather
        /// to perform the actual pathfinding.
        /// </summary>
        /// <returns>The path.</returns>
        /// <param name="start">Start.</param>
        /// <param name="dest">Destination.</param>
        /// <param name="traverseParms">Traverse parms.</param>
        /// <param name="peMode">Path end mode.</param>
        public new PawnPath FindPath(IntVec3 start, LocalTargetInfo dest, TraverseParms traverseParms, PathEndMode peMode = PathEndMode.OnCell)
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

            //TODO this might be threadable
            return pather.FindPath(start, dest, traverseParms, peMode);
        }
    }
}
