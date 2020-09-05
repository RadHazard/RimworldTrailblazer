namespace Trailblazer.Rules
{
    /// <summary>
    /// Passibilty rule for the Trailblazer pathfinding engine.  Passibility rules determine whether or not moving from
    /// one cell to another is even possible.  Cell passability rules are a subset of passability rules that don't
    /// differ based on the direction one moves into the cell.  These rules can be more easily cached.
    /// 
    /// The pathfinder will never consider an impassible move, so passability checks should only be used when Pawns must
    /// never use a given path even if they have no other options.  Be sure to update the reachability checks if it is
    /// possible for portions of the map to be cut off by your passability rule.
    /// </summary>
    public abstract class CellPassabilityRule : PassabilityRule
    {
        /// <summary>
        /// Trailblazer constructs a new version of all rules for each given pathfinding request, then queries that rule
        /// object for the duration of the pathfinding job. The constructor should be used to perform all initialization
        /// that only needs to happen once per path to minimize the work done in GetConstantCost and GetCostMultiplier.
        /// </summary>
        /// <param name="pathfindData">Path data.</param>
        protected CellPassabilityRule(PathfindData pathfindData) : base(pathfindData) { }

        /// <summary>
        /// Checks if the given move would be valid or not
        /// </summary>
        /// <returns><c>true</c> if the move is valid, <c>false</c> otherwise.</returns>
        /// <param name="moveData">Move data to check.</param>
        public override bool IsPassable(MoveData moveData)
        {
            return IsPassable(moveData.cell);
        }

        /// <summary>
        /// Checks if the given cell is passable or not
        /// </summary>
        /// <returns><c>true</c> if the cell is passable, <c>false</c> otherwise.</returns>
        /// <param name="cell">Cell to check.</param>
        public abstract bool IsPassable(CellRef cell);
    }
}
