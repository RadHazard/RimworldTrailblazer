using Verse;
using Verse.AI;

namespace Trailblazer
{
    /// <summary>
    /// All the data associated with a pathfinding job
    /// </summary>
    public struct PathfindData
    {
        public readonly Map map;
        public readonly CellRef start;
        public readonly LocalTargetInfo dest;
        public readonly TraverseParms traverseParms;
        public readonly PathEndMode pathEndMode;

        public PathfindData(Map map, CellRef start, LocalTargetInfo dest, TraverseParms traverseParms, PathEndMode pathEndMode)
        {
            this.map = map;
            this.start = start;
            this.dest = dest;
            this.traverseParms = traverseParms;
            this.pathEndMode = pathEndMode;
        }
    }
}
