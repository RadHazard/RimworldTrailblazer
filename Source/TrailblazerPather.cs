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
        protected readonly PathfinderGrid pathfinderGrid;

        protected TrailblazerPather(PathfindData pathfindData)
        {
            this.pathfindData = pathfindData;
            var cellPassRules = ThatApply(CellPassabilityRules(pathfindData));
            var passRules = ThatApply(PassabilityRules(pathfindData));
            var cellCostRules = ThatApply(CellCostRules(pathfindData));
            var costRules = ThatApply(CostRules(pathfindData));

            pathfinderGrid = new PathfinderGrid(cellCostRules, costRules, cellPassRules, passRules);
        }

        public abstract PawnPath FindPath();

        protected bool MoveIsValid(MoveData moveData)
        {
            return pathfinderGrid.MoveIsValid(moveData);
        }

        protected int CalcMoveCost(MoveData moveData)
        {
            return pathfinderGrid.MoveCost(moveData);
        }


        /// <summary>
        /// Creates an enumerable of all possible cell passability rules (regardless of whether they apply)
        /// Extend this method via harmony patching to add additional passability rules.
        /// </summary>
        /// <returns>The passability rules.</returns>
        /// <param name="pathfindData">Pathfind data.</param>
        public static IEnumerable<CellPassabilityRule> CellPassabilityRules(PathfindData pathfindData)
        {
            yield return new CellPassabilityRule_PathGrid(pathfindData);
            yield return new CellPassabilityRule_DoorByPawn(pathfindData);
            yield return new CellPassabilityRule_NoPassDoors(pathfindData);
            yield return new CellPassabilityRule_Water(pathfindData);
        }

        /// <summary>
        /// Creates an enumerable of all possible passability rules (regardless of whether they apply)
        /// Extend this method via harmony patching to add additional passability rules.
        /// </summary>
        /// <returns>The passability rules.</returns>
        /// <param name="pathfindData">Pathfind data.</param>
        public static IEnumerable<PassabilityRule> PassabilityRules(PathfindData pathfindData)
        {
            yield return new PassabilityRule_Diagonals(pathfindData);
        }

        /// <summary>
        /// Creates an enumerable of all possible cell cost rules (regardless of whether they apply)
        /// Extend this method via harmony patching to add additional cost rules.
        /// </summary>
        /// <returns>The passability rules.</returns>
        /// <param name="pathfindData">Pathfind data.</param>
        public static IEnumerable<CellCostRule> CellCostRules(PathfindData pathfindData)
        {
            yield return new CellCostRule_AllowedArea(pathfindData);
            yield return new CellCostRule_AvoidGrid(pathfindData);
            yield return new CellCostRule_Blueprints(pathfindData);
            yield return new CellCostRule_Buildings(pathfindData);
            yield return new CellCostRule_Doors(pathfindData);
            yield return new CellCostRule_PathGrid(pathfindData);
            yield return new CellCostRule_Pawns(pathfindData);
            yield return new CellCostRule_Walls(pathfindData);
        }

        /// <summary>
        /// Creates an enumerable of all possible cost rules (regardless of whether they apply)
        /// Extend this method via harmony patching to add additional cost rules.
        /// </summary>
        /// <returns>The passability rules.</returns>
        /// <param name="pathfindData">Pathfind data.</param>
        public static IEnumerable<CostRule> CostRules(PathfindData pathfindData)
        {
            yield return new CostRule_MoveTicks(pathfindData);
        }

        public static IEnumerable<T> ThatApply<T>(IEnumerable<T> rules) where T : Rule
        {
            return from rule in rules
                   where rule.Applies()
                   select rule;
        }

        public static IEnumerable<T> ThatApply<T>(params T[] rules) where T : Rule
        {
            return from rule in rules
                   where rule.Applies()
                   select rule;
        }
    }
}
