namespace Trailblazer.Rules
{
    /// <summary>
    /// Cost rules for the Trailblazer pathfinding engine.  Cost rules define how costly the pathfinder considers a move
    /// to be.  Cell cost rules are a subset of cost rules that don't differ based on the direction one moves into the
    /// cell.  These rules can be more easily cached.
    /// 
    /// All cost rules for a given move are calculated and added together.  Negative costs are allowed, but the final
    /// cost will never be below zero.
    /// </summary>
    public abstract class CellCostRule : CostRule
    {
        /// <summary>
        /// Trailblazer constructs a new version of all rules for each given pathfinding request, then queries that rule
        /// object for the duration of the pathfinding job. The constructor should be used to perform all initialization
        /// that only needs to happen once per path to minimize the work done in GetConstantCost and GetCostMultiplier.
        /// </summary>
        /// <param name="pathfindData">Path data.</param>
        protected CellCostRule(PathfindData pathfindData) : base(pathfindData) { }

        /// <summary>
        /// A constant offset to the pathing cost of the move.  Negative offsets are fine, but the final pathing cost
        /// for a move will never be below 0.
        /// 
        /// Since this operation gets called many times during a pathfinding job, this method should be extremely fast
        /// at all costs. Whenever possible, one-time initialization should be performed in the constructor and cached.
        /// </summary>
        /// <returns>The constant cost.</returns>
        /// <param name="moveData">Move data.</param>
        public override int GetCost(MoveData moveData)
        {
            return GetCost(moveData.cell);
        }

        /// <summary>
        /// A constant offset to the pathing cost of the move.  Negative offsets are fine, but the final pathing cost
        /// for a move will never be below 0.
        /// 
        /// Since this operation gets called many times during a pathfinding job, this method should be extremely fast
        /// at all costs. Whenever possible, one-time initialization should be performed in the constructor and cached.
        /// </summary>
        /// <returns>The constant cost.</returns>
        /// <param name="cell">Cell.</param>
        public abstract int GetCost(CellRef cell);
    }
}
