using Verse;
using Verse.AI;

namespace Trailblazer.Rules
{
    /// <summary>
    /// Rule that decideds whether cells are impassible based on the path grid, and also whether or not the traverse
    /// mode allows destroying buildings
    /// </summary>
    public class CellPassabilityRule_PathGrid : CellPassabilityRule
    {
        private readonly EdificeGrid edificeGrid;
        private readonly PathGrid pathGrid;
        private readonly Pawn pawn;
        private readonly bool canDestroy;

        public CellPassabilityRule_PathGrid(PathfindData pathfindData) : base(pathfindData)
        {
            edificeGrid = pathfindData.map.edificeGrid;
            pathGrid = pathfindData.map.pathGrid;
            pawn = pathfindData.traverseParms.pawn;
            TraverseMode mode = pathfindData.traverseParms.mode;
            canDestroy = mode == TraverseMode.PassAllDestroyableThings ||
                    mode == TraverseMode.PassAllDestroyableThingsNotWater;
        }

        public override bool IsPassable(CellRef cell)
        {
            if (!pathGrid.WalkableFast(cell.Index))
            {
                Building building = edificeGrid[cell.Index];
                return canDestroy && building != null && PathFinder.IsDestroyable(building);
            }
            return true;
        }
    }
}
