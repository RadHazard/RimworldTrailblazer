
namespace Trailblazer.Rules
{
    /// <summary>
    /// Cost for simply moving from one cell to the next
    /// </summary>
    public abstract class TrailblazerRule_CostMoveTicks : TrailblazerRule
    {
        protected const int DefaultMoveTicksCardinal = 13;
        protected const int DefaultMoveTicksDiagonal = 18;

        protected readonly int moveTicksCardinal;
        protected readonly int moveTicksDiagonal;

        TrailblazerRule_CostMoveTicks(PathData pathData) : base(pathData)
        {

            if (pathData.traverseParms.pawn != null)
            {
                moveTicksCardinal = pathData.traverseParms.pawn.TicksPerMoveCardinal;
                moveTicksDiagonal = pathData.traverseParms.pawn.TicksPerMoveDiagonal;
            }
            else
            {
                moveTicksCardinal = DefaultMoveTicksCardinal;
                moveTicksDiagonal = DefaultMoveTicksDiagonal;
            }
        }

        public override int? GetConstantCost(MoveData moveData)
        {
            return moveData.diagonal ? moveTicksDiagonal : moveTicksCardinal;
        }
    }
}
