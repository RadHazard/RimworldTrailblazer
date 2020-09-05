namespace Trailblazer.Rules
{
    /// <summary>
    /// Cost rules for the Trailblazer pathfinding engine.  Cost rules define how costly the pathfinder considers a move
    /// to be.
    /// 
    /// All cost rules for a given move are calculated and added together.  Negative costs are allowed, but the final
    /// cost will never be below zero.
    /// </summary>
    public abstract class CostRule : Rule
    {
        protected CostRule(PathfindData pathfindData) : base(pathfindData) { }

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
