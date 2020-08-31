namespace Trailblazer.Rules
{
    /// <summary>
    /// A rule for the Trailblazer pathfinding engine.  Rules define what cells are considered passible and how
    /// costly the pathfinder considers them to be.
    /// 
    /// Rules have both constant costs and cost multipliers (similar to stats in-game).  When Trailblazer evaluates a
    /// cell, first all constant costs are evaluated together, then all cost multipliers are multiplied together and
    /// with the constant sum.  The final value will be clamped to be no less than zero, then fed to the pathfinding
    /// engine.
    /// 
    /// For convenience, the cell will be considered impassible if either the constant cost or the multiplier returns
    /// null. For implementing rules that are simple passability checks, using GetConstantCost is preferred because it
    /// gets called first.
    /// </summary>
    public abstract class TrailblazerRule
    {
        protected readonly PathfindData pathData;

        /// <summary>
        /// Trailblazer constructs a new version of all rules for each given pathfinding request, then queries that rule
        /// object for the duration of the pathfinding job. The constructor should be used to perform all initialization
        /// that only needs to happen once per path to minimize the work done in GetConstantCost and GetCostMultiplier.
        /// </summary>
        /// <param name="pathData">Path data.</param>
        protected TrailblazerRule(PathfindData pathData)
        {
            this.pathData = pathData;
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
        /// A constant offset to the pathing cost of the move.  Return 0 if your rule does not apply to the given move,
        /// and return null if the move is not possible. Negative offsets are fine, but the final pathing cost for a
        /// move will never be below 0.  (Equivalent to +XX% on stats)
        /// 
        /// Since this operation gets called many times during a pathfinding job, this method should be extremely fast
        /// at all costs. Whenever possible, one-time initialization should be performed in the constructor and cached.
        /// </summary>
        /// <returns>The constant cost.</returns>
        /// <param name="moveData">Move data.</param>
        public virtual int? GetConstantCost(MoveData moveData)
        {
            return 0;
        }

        /// <summary>
        /// A multiplier to the pathing cost of the cell.  Return 1 if your rule does not apply to the given move,
        /// and return null if the move is not possible. A multiplier of 0 is fine (and effectively ignores all rules
        /// for a cell), but a negative multiplier is not. (Equivalent to xXX% on stats)
        /// 
        /// Since this operation gets called many times during a pathfinding job, this method should be extremely fast
        /// at all costs. Whenever possible, one-time initialization should be performed in the constructor and cached.
        /// </summary>
        /// <returns>The cost multiplier.</returns>
        /// <param name="moveData">Move data.</param>
        public virtual float? GetCostMultiplier(MoveData moveData)
        {
            return 1f;
        }
    }
}
