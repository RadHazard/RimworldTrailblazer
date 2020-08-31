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
    /// Base class for Trailblazer pathers.  Implements any logic that should be independant of the pathfinding
    /// algorithm used.
    /// </summary>
    public abstract class TrailblazerPather
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

        protected TrailblazerPather(PathfindData pathfindData)
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
