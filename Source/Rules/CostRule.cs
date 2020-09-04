namespace Trailblazer.Rules
{
    /// <summary>
    /// Cost rules for the Trailblazer pathfinding engine.  Cost rules define how costly the pathfinder considers a move
    /// to be.
    /// 
    /// All cost rules for a given move are calculated and added together.  Negative costs are allowed, but the final
    /// cost will never be below zero.
    /// </summary>
    public abstract class CostRule
    {
        protected readonly PathfindData pathfindData;

        /// <summary>
        /// Trailblazer constructs a new version of all rules for each given pathfinding request, then queries that rule
        /// object for the duration of the pathfinding job. The constructor should be used to perform all initialization
        /// that only needs to happen once per path to minimize the work done in GetConstantCost and GetCostMultiplier.
        /// </summary>
        /// <param name="pathfindData">Path data.</param>
        protected CostRule(PathfindData pathfindData)
        {
            this.pathfindData = pathfindData;
        }

        /// <summary>
        /// A check to whether the rule should be applied to the given pathfinding request at all.  Trailblazer will
        /// call this once at the beginning of the pathfinding request, and rules that return false here will not be
        /// queried at all during that specific pathfinding job. Rules should implement this where possible to avoid
        /// unnecessarily being included in a pathfinding request that will never apply to them.
        /// </summary>
        /// <returns><c>true</c> if this rule should be applied, <c>false</c> otherwise.</returns>
        public virtual bool Applies()
        {
            return true;
        }

        /// <summary>
        /// A constant offset to the pathing cost of the move.  Negative offsets are fine, but the final pathing cost
        /// for a move will never be below 0.
        /// 
        /// Since this operation gets called many times during a pathfinding job, this method should be extremely fast
        /// at all costs. Whenever possible, one-time initialization should be performed in the constructor and cached.
        /// </summary>
        /// <returns>The constant cost.</returns>
        /// <param name="moveData">Move data.</param>
        public abstract int GetCost(MoveData moveData);
    }
}
