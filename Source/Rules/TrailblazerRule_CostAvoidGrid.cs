using RimWorld;
using Verse;

namespace Trailblazer.Rules
{
    /// <summary>
    /// Cost penalty to a cell based on the AvoidGrid
    /// </summary>
    public abstract class TrailblazerRule_CostAvoidGrid : TrailblazerRule
    {
        protected readonly ByteGrid avoidGrid;

        TrailblazerRule_CostAvoidGrid(PathData pathData) : base(pathData)
        {
            avoidGrid = pathData.traverseParms.pawn?.GetAvoidGrid(true);
        }

        public override bool Applies()
        {
            return avoidGrid != null;
        }

        public override int? GetConstantCost(MoveData moveData)
        {
            // NOTE
            // For some reason vanilla multiplies this value by 8
            return avoidGrid[moveData.cellIndex] * 8;
        }
    }
}
