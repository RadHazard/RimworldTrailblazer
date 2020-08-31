using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RimWorld;
using Trailblazer.Rules;
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
            PathfindData pathfindData = new PathfindData(map, map.GetCellRef(start), dest, traverseParms, pathEndMode);
            return GetWorker(pathfindData).FindPath();
        }

        //TODO - do we need the second layer of abstraction of workers?  It seems like it might be overengineered
        protected abstract TrailblazerPathWorker GetWorker(PathfindData pathfindData);


        /// <summary>
        /// Worker class that contains all the information needed for a single pathfinding operation.  Workers
        /// should be independant and capable of operating in their own threads
        /// </summary>
        protected abstract class TrailblazerPathWorker
        {
            //TODO - this list needs to be extensible
            protected static readonly List<Func<PathfindData, TrailblazerRule>> ruleFactories = new List<Func<PathfindData, TrailblazerRule>>
            {
                r => new TrailblazerRule_PassabilityWater(r),
                r => new TrailblazerRule_PassabilityDiagonal(r),
                r => new TrailblazerRule_TestBuildings(r),
                r => new TrailblazerRule_CostAllowedArea(r),
                r => new TrailblazerRule_CostAvoidGrid(r),
                r => new TrailblazerRule_CostBlueprints(r),
                r => new TrailblazerRule_CostMoveTicks(r),
                r => new TrailblazerRule_CostPawns(r),
                r => new TrailblazerRule_CostTerrain(r)
            };

            protected readonly PathfindData pathfindData;
            protected readonly List<TrailblazerRule> rules;

            protected TrailblazerPathWorker(PathfindData pathfindData)
            {
                this.pathfindData = pathfindData;
                rules = (from factory in ruleFactories
                         let rule = factory.Invoke(this.pathfindData)
                         where rule.Applies()
                         select rule).ToList();
            }

            public abstract PawnPath FindPath();

            protected int? CalcMoveCost(MoveData moveData)
            {
                int cost = 0;
                foreach (TrailblazerRule rule in rules)
                {
                    int? ruleCost = rule.GetConstantCost(moveData);
                    if (ruleCost == null)
                    {
                        return null;
                    }
                    cost += ruleCost ?? 0;
                }

                // TODO - evaluate: do we need a multiplier?  None of the vanilla rules use them
                float multiplier = 1f;
                foreach (TrailblazerRule rule in rules)
                {
                    float? ruleMultiplier = rule.GetCostMultiplier(moveData);
                    if (ruleMultiplier == null)
                    {
                        return null;
                    }
                    multiplier *= ruleMultiplier ?? 1;
                }

                // Ensure cost is never less than zero
                return Math.Max(0, (int)Math.Round(cost * multiplier));
            }
        }
    }
}
