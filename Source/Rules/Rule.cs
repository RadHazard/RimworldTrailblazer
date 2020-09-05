namespace Trailblazer.Rules
{
    /// <summary>
    /// Rules allow the Trailblazer pathfinding engine to be customized.  Passability rules determine whether a
    /// pathfinder considers a cell at all, and cost rules adjust how "expensive" the pathfinder considers each cell.
    /// 
    /// Rules have an Applies() method that determines whether or not a rule should be applied to the pathfinder.  This
    /// can be used to toggle rules on or off or to have expensive rules disable themselves when they don't need to
    /// be run for a particular pathfinding request.
    /// </summary>
    public abstract class Rule
    {
        protected readonly PathfindData pathfindData;

        /// <summary>
        /// Trailblazer constructs a new version of all rules for each given pathfinding request, then queries that rule
        /// object for the duration of the pathfinding job. The constructor should initialize and cache as much info as
        /// possible to minimize the work done during pathfinding.  (Only if such caching would be less work than
        /// calculating the data on the fly, of course)
        /// </summary>
        /// <param name="pathfindData">Path data.</param>
        protected Rule(PathfindData pathfindData)
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
    }
}
