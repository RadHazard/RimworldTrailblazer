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
    public abstract class PassabilityRule : Rule
    {
        protected PassabilityRule(PathfindData pathfindData) : base(pathfindData) { }

        /// <summary>
        /// Checks if the given move would be valid or not
        /// </summary>
        /// <returns><c>true</c> if the move is valid, <c>false</c> otherwise.</returns>
        /// <param name="moveData">Move data to check.</param>
        public abstract bool IsPassable(MoveData moveData);
    }
}
