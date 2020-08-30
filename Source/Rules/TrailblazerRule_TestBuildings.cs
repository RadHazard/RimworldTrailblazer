using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace Trailblazer.Rules
{
    /// <summary>
    /// Test for a cell for the buildings sitting on it.  Depending on the traverse mode, impassible buildings
    /// may be considered impassible or they may just add a cost to the cell.
    /// </summary>
    public abstract class TrailblazerRule_TestBuildings : TrailblazerRule
    {
        protected const int Cost_BlockedWallBase = 70;
        protected const float Cost_BlockedWallExtraPerHitPoint = 0.2f;
        protected const int Cost_BlockedDoorBase = 50;
        protected const float Cost_BlockedDoorPerHitPoint = 0.2f;

        protected readonly PathGrid pathGrid;
        protected readonly EdificeGrid edificeGrid;
        protected readonly bool passDestroyableThings;
        protected readonly Pawn pawn;
        protected Func<Building_Door, int> doorHandler;

        TrailblazerRule_TestBuildings(PathData pathData) : base(pathData)
        {
            pathGrid = pathData.map.pathGrid;
            edificeGrid = pathData.map.edificeGrid;
            passDestroyableThings = pathData.traverseParms.mode.CanDestroy();

            switch (pathData.traverseParms.mode)
            {
                case TraverseMode.NoPassClosedDoors:
                case TraverseMode.NoPassClosedDoorsOrWater:
                    doorHandler = door =>
                    {
                        if (door.FreePassage)
                        {
                            return 0;
                        }
                        return int.MaxValue;
                    };
                    break;
                case TraverseMode.PassAllDestroyableThings:
                case TraverseMode.PassAllDestroyableThingsNotWater:
                    doorHandler = door =>
                    {
                        if (pawn != null && door.PawnCanOpen(pawn) && !door.IsForbiddenToPass(pawn) && !door.FreePassage)
                        {
                            return door.TicksToOpenNow;
                        }
                        if ((pawn != null && door.CanPhysicallyPass(pawn)) || door.FreePassage)
                        {
                            return 0;
                        }
                        return Cost_BlockedDoorBase + (int)(door.HitPoints * Cost_BlockedDoorPerHitPoint);
                    };
                    break;
                case TraverseMode.PassDoors:
                    doorHandler = door =>
                    {
                        if (pawn != null && door.PawnCanOpen(pawn) && !door.IsForbiddenToPass(pawn) && !door.FreePassage)
                        {
                            return door.TicksToOpenNow;
                        }
                        if ((pawn != null && door.CanPhysicallyPass(pawn)) || door.FreePassage)
                        {
                            return 0;
                        }
                        return 150;
                    };
                    break;
                case TraverseMode.ByPawn:
                    doorHandler = door =>
                    {
                        if (!pathData.traverseParms.canBash && door.IsForbiddenToPass(pawn))
                        {
                            return int.MaxValue;
                        }
                        if (door.PawnCanOpen(pawn) && !door.FreePassage)
                        {
                            return door.TicksToOpenNow;
                        }
                        if (door.CanPhysicallyPass(pawn))
                        {
                            return 0;
                        }
                        if (pathData.traverseParms.canBash)
                        {
                            return 300;
                        }
                        return int.MaxValue;
                    };
                    break;
            }

        }

        public override int? GetConstantCost(MoveData moveData)
        {
            int moveCost = 0;
            Building building = edificeGrid[moveData.cellIndex];

            if (!pathGrid.WalkableFast(moveData.cellIndex))
            {
                if (!passDestroyableThings || building == null || !PathFinder.IsDestroyable(building))
                {
                    return null;
                }

                moveCost += Cost_BlockedWallBase;
                moveCost += (int)(building.HitPoints * Cost_BlockedWallExtraPerHitPoint);
            }

            // NOTE
            // Vanilla does indeed lack an else before this if statement.  I'm not sure if walls don't return a
            // pathfinding cost via Building.PathFindCostFor(Pawn) or what, but it seems possible that they end up
            // getting double counted
            if (building != null)
            {
                int buildingCost = 0;
                if (building is Building_Door door)
                {
                    buildingCost = doorHandler.Invoke(door);
                }
                else if (pawn != null)
                {
                    buildingCost = building.PathFindCostFor(pawn);
                }

                if (buildingCost == int.MaxValue)
                {
                    return null;
                }
                moveCost += buildingCost;
            }
            return moveCost;
        }
    }
}
