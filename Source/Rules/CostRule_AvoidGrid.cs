using RimWorld;
using Verse;

namespace Trailblazer.Rules
{
    /// <summary>
    /// Cost penalty to a cell based on the AvoidGrid
    /// </summary>
    public class CostRule_AvoidGrid : CostRule
    {
        protected readonly ByteGrid avoidGrid;

        public CostRule_AvoidGrid(PathfindData pathfindData) : base(pathfindData)
        {
            avoidGrid = pathfindData.traverseParms.pawn?.GetAvoidGrid(true);
        }

        public override bool Applies()
        {
            return avoidGrid != null;
        }

        public override int GetCost(MoveData moveData)
        {
            // NOTE
            // For some reason vanilla multiplies this value by 8
            return avoidGrid[moveData.cell.Index] * 8;
        }
    }
}
