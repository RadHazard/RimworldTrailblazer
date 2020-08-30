using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace Trailblazer
{
    /// <summary>
    /// Base class for Trailblazer pathers.  Contains a bunch of default methods that should be useful independant
    /// of the pathfinding algorithm used
    /// </summary>
    public abstract class TrailblazerPather
    {
        // Map data
        protected readonly Map map;

        // Pathing cost constants
        protected const int SearchLimit = 160000;
        protected const int DefaultMoveTicksCardinal = 13;
        protected const int DefaultMoveTicksDiagonal = 18;
        protected const int Cost_BlockedWallBase = 70;
        protected const float Cost_BlockedWallExtraPerHitPoint = 0.2f;
        protected const int Cost_BlockedDoor = 50;
        protected const float Cost_BlockedDoorPerHitPoint = 0.2f;
        protected const int Cost_OutsideAllowedArea = 600;
        protected const int Cost_PawnCollision = 175;

        protected struct PathfindRequest
        {
            public readonly IntVec3 start;
            public readonly LocalTargetInfo dest;
            public readonly TraverseParms traverseParms;
            public readonly PathEndMode pathEndMode;

            public PathfindRequest(IntVec3 start, LocalTargetInfo dest, TraverseParms traverseParms, PathEndMode pathEndMode)
            {
                this.start = start;
                this.dest = dest;
                this.traverseParms = traverseParms;
                this.pathEndMode = pathEndMode;
            }
        }

        protected struct PathCostData
        {
            public readonly TraverseMode traverseMode;
            public readonly int moveTicksCardinal;
            public readonly int moveTicksDiagonal;
            public readonly Area allowedArea;
            public readonly ByteGrid avoidGrid;
            public readonly Pawn pawn;
            public readonly bool pawnDrafted;
            public readonly bool shouldCollideWithPawns;
        }

        protected TrailblazerPather(Map map)
        {
            this.map = map;
        }

        public PawnPath FindPath(IntVec3 start, LocalTargetInfo dest, TraverseParms traverseParms, PathEndMode pathEndMode)
        {
            //TODO add threading once this is all worked out
            PathfindRequest pathfindRequest = new PathfindRequest(start, dest, traverseParms, pathEndMode);
            return GetWorker(pathfindRequest).FindPath();
        }

        protected abstract TrailblazerPathWorker GetWorker(PathfindRequest pathfindRequest);


        /// <summary>
        /// Worker class that contains all the information needed for a single pathfinding operation.  Workers
        /// should be independant and capable of operating in their own threads
        /// </summary>
        protected abstract class TrailblazerPathWorker
        {
            protected readonly Map map;
            protected readonly IntVec3 start;
            protected readonly LocalTargetInfo dest;
            protected readonly TraverseParms traverseParms;
            protected readonly PathEndMode pathEndMode;

            protected TrailblazerPathWorker(Map map, PathfindRequest pathfindRequest)
            {
                this.map = map;
                start = pathfindRequest.start;
                dest = pathfindRequest.dest;
                traverseParms = pathfindRequest.traverseParms;
                pathEndMode = pathfindRequest.pathEndMode;
            }

            public abstract PawnPath FindPath();

            protected bool IsCellPassible(IntVec3 cell, Move move)
            {
                throw new NotImplementedException();
            }

            protected bool GetCellCost(IntVec3 cell, Move move)
            {
                throw new NotImplementedException();
            }

            //protected bool IsCellPassible(IntVec3 cell, Move move, TraverseMode traverseMode)
            //{
            //    // Check for water
            //    if (!traverseMode.CanPassWater() && cell.GetTerrain(map).HasTag("Water"))
            //    {
            //        return false;
            //    }

            //    // Check for walls
            //    Building building = map.edificeGrid[cellIndex];
            //    if (!map.pathGrid.WalkableFast(cellIndex))
            //    {
            //        if (!traverseMode.CanDestroy())
            //        {
            //            return null;
            //        }
            //        blockedByWall = true;
            //        cellCost += Cost_BlockedWallBase;
            //        if (building == null || !PathFinder.IsDestroyable(building))
            //        {
            //            return null;
            //        }
            //        cellCost += (int)(building.HitPoints * Cost_BlockedWallExtraPerHitPoint);
            //    }

            //    // Check if we're blocked from moving diagonally
            //    if (move.IsDiagonal())
            //    {
            //        Move sideA;
            //        Move sideB;
            //        switch (move)
            //        {
            //            case Move.NW:
            //                sideA = Move.S;
            //                sideB = Move.E;
            //                break;
            //            case Move.NE:
            //                sideA = Move.S;
            //                sideB = Move.W;
            //                break;
            //            case Move.SE:
            //                sideA = Move.N;
            //                sideB = Move.W;
            //                break;
            //            case Move.SW:
            //                sideA = Move.N;
            //                sideB = Move.E;
            //                break;
            //            default:
            //                throw new Exception("Invalid diagonal move");
            //        }

            //        if (BlocksDiagonalMovement(sideA.From(cell)) || BlocksDiagonalMovement(sideB.From(cell)))
            //        {
            //            // TODO -- there was a weird, always-true boolean here in vanilla that would have added 70 to
            //            // the cost instead of returning impassible were it ever false.  Not sure if that's important.
            //            return false;
            //        }
            //    }
            //}

            ///// <summary>
            ///// Calculates the cost of pathing through a cell
            ///// </summary>
            ///// <returns>The pathfinding cost, or null if the cell is not pathable.</returns>
            ///// <param name="cell">The cell we're pathing through.</param>
            ///// <param name="move">The direction we're moving into the cell.</param>
            ///// <param name="traverseMode">The traverse mode to use.</param>
            ///// <param name="pathCostData">Additional data used for pathing costs.</param>
            //protected int? CalcCellCost(IntVec3 cell, Move move, TraverseMode traverseMode, PathCostData pathCostData)
            //{
            //    int cellCost = 0;
            //    int cellIndex = map.cellIndices.CellToIndex(cell);
            //    bool blockedByWall = false;

            //    // Check for water
            //    if (!traverseMode.CanPassWater() && cell.GetTerrain(map).HasTag("Water"))
            //    {
            //        return null;
            //    }

            //    // Check for walls
            //    Building building = map.edificeGrid[cellIndex];
            //    if (!map.pathGrid.WalkableFast(cellIndex))
            //    {
            //        if (!traverseMode.CanDestroy())
            //        {
            //            return null;
            //        }
            //        blockedByWall = true;
            //        cellCost += Cost_BlockedWallBase;
            //        if (building == null || !PathFinder.IsDestroyable(building))
            //        {
            //            return null;
            //        }
            //        cellCost += (int)(building.HitPoints * Cost_BlockedWallExtraPerHitPoint);
            //    }

            //    // Check if we're blocked from moving diagonally
            //    if (move.IsDiagonal())
            //    {
            //        Move sideA;
            //        Move sideB;
            //        switch (move)
            //        {
            //            case Move.NW:
            //                sideA = Move.S;
            //                sideB = Move.E;
            //                break;
            //            case Move.NE:
            //                sideA = Move.S;
            //                sideB = Move.W;
            //                break;
            //            case Move.SE:
            //                sideA = Move.N;
            //                sideB = Move.W;
            //                break;
            //            case Move.SW:
            //                sideA = Move.N;
            //                sideB = Move.E;
            //                break;
            //            default:
            //                throw new Exception("Invalid diagonal move");
            //        }

            //        if (BlocksDiagonalMovement(sideA.From(cell)) || BlocksDiagonalMovement(sideB.From(cell)))
            //        {
            //            // TODO -- there was a weird, always-true boolean here in vanilla that would have added 70 to
            //            // the cost instead of returning impassible were it ever false.  Not sure if that's important.
            //            return null;
            //        }
            //    }

            //    cellCost += move.IsDiagonal() ? pathCostData.moveTicksDiagonal : pathCostData.moveTicksCardinal;
            //    if (!blockedByWall)
            //    {
            //        cellCost += pathCostData.pathGridArray[cellIndex];
            //        cellCost += pathCostData.pawnDrafted ? topGrid[cellIndex].extraDraftedPerceivedPathCost : topGrid[cellIndex].extraNonDraftedPerceivedPathCost;
            //    }
            //    if (pathCostData.avoidGrid != null)
            //    {
            //        cellCost += pathCostData.avoidGrid[cellIndex] * 8;
            //    }
            //    if (pathCostData.allowedArea != null && !pathCostData.allowedArea[cellIndex])
            //    {
            //        cellCost += Cost_OutsideAllowedArea;
            //    }
            //    if (pathCostData.shouldCollideWithPawns && PawnUtility.AnyPawnBlockingPathAt(cell, pathCostData.pawn, false, false, true))
            //    {
            //        cellCost += Cost_PawnCollision;
            //    }

            //    // Check for doors
            //    if (building != null)
            //    {
            //        int buildingCost = PathFinder.GetBuildingCost(building, pathCostData.traverseParms, pathCostData.pawn);
            //        if (buildingCost == int.MaxValue)
            //        {
            //            return null;
            //        }
            //        cellCost += buildingCost;
            //    }

            //    // Check for blueprints
            //    List<Blueprint> list = map.blueprintGrid.InnerArray[cellIndex];
            //    if (list != null)
            //    {
            //        int blueprintCost = 0;
            //        for (int j = 0; j < list.Count; j++)
            //        {
            //            blueprintCost = Math.Max(blueprintCost, PathFinder.GetBlueprintCost(list[j], pawn));
            //        }
            //        if (blueprintCost == int.MaxValue)
            //        {
            //            return null;
            //        }
            //        cellCost += blueprintCost;
            //    }

            //    return cellCost;
            //}

            /// <summary>
            /// Checks whether a cell blocks diagonal movement.
            /// </summary>
            /// <returns><c>true</c> if diagonal movement is blocked, <c>false</c> otherwise.</returns>
            /// <param name="cell">The cell to check.</param>
            private bool BlocksDiagonalMovement(IntVec3 cell)
            {
                return PathFinder.BlocksDiagonalMovement(cell.x, cell.z, map);
            }
        }
    }
}
