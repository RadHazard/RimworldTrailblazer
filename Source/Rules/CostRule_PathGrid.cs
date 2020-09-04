using Verse;
using Verse.AI;

namespace Trailblazer.Rules
{
    /// <summary>
    /// Cost penalty to a cell based on the pathGrid (which handles both terrain and objects sitting atop it)
    /// </summary>
    public class CostRule_PathGrid : CostRule
    {
        private readonly PathGrid pathGrid;
        private readonly TerrainGrid terrainGrid;
        private readonly bool pawnDrafted;

        public CostRule_PathGrid(PathfindData pathfindData) : base(pathfindData)
        {
            pathGrid = pathfindData.map.pathGrid;
            terrainGrid = pathfindData.map.terrainGrid;
            pawnDrafted = pathfindData.traverseParms.pawn?.Drafted ?? false;
        }

        public override int GetCost(MoveData moveData)
        {
            if (pathGrid.WalkableFast(moveData.cell.Index))
            {
                int cost = pathGrid.pathGrid[moveData.cell];
                cost += pawnDrafted ? terrainGrid.topGrid[moveData.cell].extraDraftedPerceivedPathCost
                        : terrainGrid.topGrid[moveData.cell].extraNonDraftedPerceivedPathCost;
                return cost;
            }
            return 0;
        }
    }
}
