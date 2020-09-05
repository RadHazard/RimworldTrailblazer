using System;
using System.Collections.Generic;

namespace Trailblazer
{
    /// <summary>
    /// A working grid for pathfinders.  The grid caches information such as pathfinding cost and reachable neighbors
    /// for visited cells.
    /// 
    /// Note:  As of now, only cell-based rules are cached.  Advanced rules that require the full move data are not.
    /// </summary>
    public class PathfinderGrid
    {
        private readonly Dictionary<CellRef, int> costCache;
        private readonly Dictionary<CellRef, bool> passabilityCache;

        public PathfinderGrid()
        {
        }
    }
}
