using RimWorld;
using Verse;

namespace Trailblazer.Rules
{
    /// <summary>
    /// Cost penalty to a cell for the buildings sitting on it.
    /// Note -- Walls and Doors have special rules that are handled separately
    /// </summary>
    public class CostRule_Buildings : CostRule
    {
        private readonly EdificeGrid edificeGrid;
        private readonly Pawn pawn;

        public CostRule_Buildings(PathfindData pathfindData) : base(pathfindData)
        {
            edificeGrid = pathfindData.map.edificeGrid;
            pawn = pathfindData.traverseParms.pawn;
        }

        public override bool Applies()
        {
            return pawn != null;
        }

        public override int GetCost(MoveData moveData)
        {
            Building building = edificeGrid[moveData.cell.Index];
            // Note - vanilla never calls PathFindCostFor() on doors, but it does do so for walls.  I don't know why.
            if (building != null && !(building is Building_Door))
            {
                return building.PathFindCostFor(pawn);
            }
            return 0;
        }
    }
}
