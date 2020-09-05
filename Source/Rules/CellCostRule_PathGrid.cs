using Verse;
using Verse.AI;

namespace Trailblazer.Rules
{
    /// <summary>
    /// Cost penalty to a cell based on the pathGrid (which handles both terrain and objects sitting atop it)
    /// </summary>
    public class CellCostRule_PathGrid : CellCostRule
    {
        private readonly PathGrid pathGrid;
        private readonly TerrainGrid terrainGrid;
        private readonly bool pawnDrafted;

        public CellCostRule_PathGrid(PathfindData pathfindData) : base(pathfindData)
        {
            pathGrid = pathfindData.map.pathGrid;
            terrainGrid = pathfindData.map.terrainGrid;
            pawnDrafted = pathfindData.traverseParms.pawn?.Drafted ?? false;
        }

        public override int GetCost(CellRef cell)
        {
            if (pathGrid.WalkableFast(cell.Index))
            {
                int cost = pathGrid.pathGrid[cell];
                cost += pawnDrafted ? terrainGrid.topGrid[cell].extraDraftedPerceivedPathCost
                        : terrainGrid.topGrid[cell].extraNonDraftedPerceivedPathCost;
                return cost;
            }
            return 0;
        }
    }
}
