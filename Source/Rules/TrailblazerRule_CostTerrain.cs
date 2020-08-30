﻿using Verse;
using Verse.AI;

namespace Trailblazer.Rules
{
    /// <summary>
    /// Cost penalty to a cell for the terrain
    /// </summary>
    public abstract class TrailblazerRule_CostTerrain : TrailblazerRule
    {
        protected const int Cost_BlockedWallBase = 70;
        protected const float Cost_BlockedWallExtraPerHitPoint = 0.2f;
        protected const int Cost_BlockedDoor = 50;
        protected const float Cost_BlockedDoorPerHitPoint = 0.2f;

        protected readonly PathGrid pathGrid;
        protected readonly TerrainGrid terrainGrid;
        protected readonly bool pawnDrafted;

        TrailblazerRule_CostTerrain(PathData pathData) : base(pathData)
        {
            pathGrid = pathData.map.pathGrid;
            terrainGrid = pathData.map.terrainGrid;

            pawnDrafted = pathData.traverseParms.pawn?.Drafted ?? false;
        }

        public override int? GetConstantCost(MoveData moveData)
        {
            if (!pathGrid.WalkableFast(moveData.cellIndex))
            {
                int cellCost = pathGrid.pathGrid[moveData.cellIndex];
                cellCost += pawnDrafted ? terrainGrid.topGrid[moveData.cellIndex].extraDraftedPerceivedPathCost
                        : terrainGrid.topGrid[moveData.cellIndex].extraNonDraftedPerceivedPathCost;

                return cellCost;
            }
            return 0;
        }
    }
}
