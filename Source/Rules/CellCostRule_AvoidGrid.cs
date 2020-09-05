using RimWorld;
using Verse;

namespace Trailblazer.Rules
{
    /// <summary>
    /// Cost penalty to a cell based on the AvoidGrid
    /// </summary>
    public class CellCostRule_AvoidGrid : CellCostRule
    {
        protected readonly ByteGrid avoidGrid;

        public CellCostRule_AvoidGrid(PathfindData pathfindData) : base(pathfindData)
        {
            avoidGrid = pathfindData.traverseParms.pawn?.GetAvoidGrid(true);
        }

        public override bool Applies()
        {
            return avoidGrid != null;
        }

        public override int GetCost(CellRef cell)
        {
            // NOTE
            // For some reason vanilla multiplies this value by 8
            return avoidGrid[cell.Index] * 8;
        }
    }
}
