using RimWorld;
using Verse;

namespace Trailblazer.Rules
{
    /// <summary>
    /// Cost penalty to a cell if it's occupied by a pawn
    /// </summary>
    public class CostRule_Pawns : CostRule
    {
        public const int Cost_PawnCollision = 175;

        private readonly Pawn pawn;

        public CostRule_Pawns(PathfindData pathfindData) : base(pathfindData)
        {
            pawn = pathfindData.traverseParms.pawn;
        }

        public override bool Applies()
        {
            return pawn != null && PawnUtility.ShouldCollideWithPawns(pawn);
        }

        public override int GetCost(MoveData moveData)
        {
            if (PawnUtility.AnyPawnBlockingPathAt(moveData.cell, pawn, false, false, true))
            {
                return Cost_PawnCollision;
            }
            return 0;
        }
    }
}
