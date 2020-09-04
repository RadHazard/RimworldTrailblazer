﻿using Verse;
using Verse.AI;

namespace Trailblazer.Rules
{
    /// <summary>
    /// Cost penalty to a cell if it has a wall sitting on it
    /// Note - this rule only gets invoked if the PassabilityRule_PathGrid allows walls to be pathed through in the
    /// first place (which only happens if the traverse mode allows it)
    /// </summary>
    public class CostRule_Walls : CostRule
    {
        public const int Cost_BlockedWallBase = 70;
        public const float Cost_BlockedWallExtraPerHitPoint = 0.2f;

        private readonly EdificeGrid edificeGrid;
        private readonly PathGrid pathGrid;
        private readonly Pawn pawn;

        public CostRule_Walls(PathfindData pathfindData) : base(pathfindData)
        {
            edificeGrid = pathfindData.map.edificeGrid;
            pathGrid = pathfindData.map.pathGrid;
            pawn = pathfindData.traverseParms.pawn;
        }

        public override int GetCost(MoveData moveData)
        {
            if (!pathGrid.WalkableFast(moveData.cell.Index))
            {
                Building building = edificeGrid[moveData.cell.Index];
                if (building != null)
                {
                    return Cost_BlockedWallBase + (int)(building.HitPoints * Cost_BlockedWallExtraPerHitPoint);
                }
            }
            return 0;
        }
    }
}
