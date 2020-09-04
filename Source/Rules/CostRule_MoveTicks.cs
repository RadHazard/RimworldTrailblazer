namespace Trailblazer.Rules
{
    /// <summary>
    /// Cost for simply moving from one cell to the next
    /// </summary>
    public class CostRule_MoveTicks : CostRule
    {
        public const int DefaultMoveTicksCardinal = 13;
        public const int DefaultMoveTicksDiagonal = 18;

        protected readonly int moveTicksCardinal;
        protected readonly int moveTicksDiagonal;

        public CostRule_MoveTicks(PathfindData pathfindData) : base(pathfindData)
        {
            GetMoveTicks(pathfindData, out moveTicksCardinal, out moveTicksDiagonal);
        }

        public override int GetCost(MoveData moveData)
        {
            return moveData.diagonal ? moveTicksDiagonal : moveTicksCardinal;
        }

        public static void GetMoveTicks(PathfindData pathfindData, out int moveTicksCardinal, out int moveTicksDiagonal)
        {
            if (pathfindData.traverseParms.pawn != null)
            {
                moveTicksCardinal = pathfindData.traverseParms.pawn.TicksPerMoveCardinal;
                moveTicksDiagonal = pathfindData.traverseParms.pawn.TicksPerMoveDiagonal;
            }
            else
            {
                moveTicksCardinal = DefaultMoveTicksCardinal;
                moveTicksDiagonal = DefaultMoveTicksDiagonal;
            }
        }
    }
}
