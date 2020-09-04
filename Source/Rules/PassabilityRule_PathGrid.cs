using Verse;
using Verse.AI;

namespace Trailblazer.Rules
{
    /// <summary>
    /// Rule that decideds whether cells are impassible based on the path grid, and also whether or not the traverse
    /// mode allows destroying buildings
    /// </summary>
    public class PassabilityRule_PathGrid : PassabilityRule
    {
        private readonly EdificeGrid edificeGrid;
        private readonly PathGrid pathGrid;
        private readonly Pawn pawn;
        private readonly bool canDestroy;

        public PassabilityRule_PathGrid(PathfindData pathfindData) : base(pathfindData)
        {
            edificeGrid = pathfindData.map.edificeGrid;
            pathGrid = pathfindData.map.pathGrid;
            pawn = pathfindData.traverseParms.pawn;
            TraverseMode mode = pathfindData.traverseParms.mode;
            canDestroy = mode == TraverseMode.PassAllDestroyableThings ||
                    mode == TraverseMode.PassAllDestroyableThingsNotWater;
        }

        public override bool IsPassable(MoveData moveData)
        {
            if (!pathGrid.WalkableFast(moveData.cell.Index))
            {
                Building building = edificeGrid[moveData.cell.Index];
                return canDestroy && building != null && PathFinder.IsDestroyable(building);
            }
            return true;
        }
    }
}
