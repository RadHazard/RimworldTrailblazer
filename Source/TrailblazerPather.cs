using System;
using System.Collections.Generic;
using System.Linq;
using Trailblazer.Rules;
using Verse.AI;

namespace Trailblazer
{
    /// <summary>
    /// Base class for Trailblazer pathers.  Implements any logic that should be independant of the pathfinding
    /// algorithm used.
    /// </summary>
    public abstract class TrailblazerPather
    {
        protected readonly PathfindData pathfindData;
        protected readonly List<PassabilityRule> passabilityRules;
        protected readonly List<CostRule> costRules;

        protected TrailblazerPather(PathfindData pathfindData)
        {
            this.pathfindData = pathfindData;
            passabilityRules = PassabilityRules_ThatApply(pathfindData).ToList();
            costRules = CostRules_ThatApply(pathfindData).ToList();
        }

        public abstract PawnPath FindPath();

        protected bool MoveIsValid(MoveData moveData)
        {
            //TODO caching
            return passabilityRules.All(r => r.IsPassable(moveData));
        }

        protected int CalcMoveCost(MoveData moveData)
        {
            //TODO caching
            int cost = costRules.Sum(r => r.GetCost(moveData));
            // Ensure cost is never less than zero
            return Math.Max(0, cost);
        }

        /// <summary>
        /// Creates an enumerable of all possible passability rules (regardless of whether they apply)
        /// Extend this method via harmony patching to add additional passability rules.
        /// </summary>
        /// <returns>The passability rules.</returns>
        /// <param name="pathfindData">Pathfind data.</param>
        public static IEnumerable<PassabilityRule> PassabilityRules(PathfindData pathfindData)
        {
            yield return new CellPassabilityRule_PathGrid(pathfindData);
            yield return new PassabilityRule_Diagonals(pathfindData);
            yield return new CellPassabilityRule_DoorByPawn(pathfindData);
            yield return new CellPassabilityRule_NoPassDoors(pathfindData);
            yield return new CellPassabilityRule_Water(pathfindData);
        }

        /// <summary>
        /// Calculates and returns the list of passability rules that apply to this pathfinding request
        /// </summary>
        /// <returns>The rules that apply.</returns>
        /// <param name="pathfindData">Pathfind data.</param>
        public static IEnumerable<PassabilityRule> PassabilityRules_ThatApply(PathfindData pathfindData)
        {
            return from rule in PassabilityRules(pathfindData)
                   where rule.Applies()
                   select rule;
        }

        /// <summary>
        /// Creates an enumerable of all possible cost rules (regardless of whether they apply)
        /// Extend this method via harmony patching to add additional cost rules.
        /// </summary>
        /// <returns>The passability rules.</returns>
        /// <param name="pathfindData">Pathfind data.</param>
        public static IEnumerable<CostRule> CostRules(PathfindData pathfindData)
        {
            yield return new CellCostRule_AllowedArea(pathfindData);
            yield return new CellCostRule_AvoidGrid(pathfindData);
            yield return new CellCostRule_Blueprints(pathfindData);
            yield return new CellCostRule_Buildings(pathfindData);
            yield return new CellCostRule_Doors(pathfindData);
            yield return new CostRule_MoveTicks(pathfindData);
            yield return new CellCostRule_Pawns(pathfindData);
            yield return new CellCostRule_PathGrid(pathfindData);
        }

        /// <summary>
        /// Calculates and returns the list of cost rules that apply to this pathfinding request
        /// </summary>
        /// <returns>The rules that apply.</returns>
        /// <param name="pathfindData">Pathfind data.</param>
        public static IEnumerable<CostRule> CostRules_ThatApply(PathfindData pathfindData)
        {
            return from rule in CostRules(pathfindData)
                   where rule.Applies()
                   select rule;
        }
    }
}
