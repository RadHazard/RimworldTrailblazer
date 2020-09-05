namespace Trailblazer.Rules
{
    /// <summary>
    /// Passibilty rule for the Trailblazer pathfinding engine.  Passibility rules determine whether or not moving from
    /// one cell to another is even possible.
    /// 
    /// The pathfinder will never consider an impassible move, so passability checks should only be used when Pawns must
    /// never use a given path even if they have no other options.  Be sure to update the reachability checks if it is
    /// possible for portions of the map to be cut off by your passability rule.
    /// </summary>
    public abstract class PassabilityRule
    {
        protected readonly PathfindData pathfindData;

        /// <summary>
        /// Trailblazer constructs a new version of all rules for each given pathfinding request, then queries that rule
        /// object for the duration of the pathfinding job. The constructor should be used to perform all initialization
        /// that only needs to happen once per path to minimize the work done in GetConstantCost and GetCostMultiplier.
        /// </summary>
        /// <param name="pathfindData">Path data.</param>
        protected PassabilityRule(PathfindData pathfindData)
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
        /// Checks if the given move would be valid or not
        /// </summary>
        /// <returns><c>true</c> if the move is valid, <c>false</c> otherwise.</returns>
        /// <param name="moveData">Move data to check.</param>
        public abstract bool IsPassable(MoveData moveData);
    }
}
