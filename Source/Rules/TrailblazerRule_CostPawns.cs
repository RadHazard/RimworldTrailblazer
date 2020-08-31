﻿using RimWorld;
using Verse;

namespace Trailblazer.Rules
{
    /// <summary>
    /// Cost penalty to a cell if it's occupied by a pawn
    /// </summary>
    public class TrailblazerRule_CostPawns : TrailblazerRule
    {
        protected const int Cost_PawnCollision = 175;

        protected readonly Pawn pawn;

        public TrailblazerRule_CostPawns(PathfindData pathData) : base(pathData)
        {
            pawn = pathData.traverseParms.pawn;
        }

        public override bool Applies()
        {
            return pawn != null && PawnUtility.ShouldCollideWithPawns(pawn);
        }

        public override int? GetConstantCost(MoveData moveData)
        {
            if (PawnUtility.AnyPawnBlockingPathAt(moveData.cell, pawn, false, false, true))
            {
                return Cost_PawnCollision;
            }
            return 0;
        }
    }
}
