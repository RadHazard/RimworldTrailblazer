using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace Trailblazer
{
    /// <summary>
    /// Base class for Trailblazer pathers.  Contains a bunch of default methods that should be useful independant
    /// of the pathfinding algorithm used
    /// </summary>
    public abstract class TrailblazerPather
    {
        // Map data
        protected readonly Map map;

        // Pathing cost constants
        protected const int SearchLimit = 160000;
        protected const int DefaultMoveTicksCardinal = 13;
        protected const int DefaultMoveTicksDiagonal = 18;
        protected const int Cost_BlockedWallBase = 70;
        protected const float Cost_BlockedWallExtraPerHitPoint = 0.2f;
        protected const int Cost_BlockedDoor = 50;
        protected const float Cost_BlockedDoorPerHitPoint = 0.2f;
        protected const int Cost_OutsideAllowedArea = 600;
        protected const int Cost_PawnCollision = 175;

        protected struct PathfindRequest
        {
            public readonly IntVec3 start;
            public readonly LocalTargetInfo dest;
            public readonly TraverseParms traverseParms;
            public readonly PathEndMode pathEndMode;

            public PathfindRequest(IntVec3 start, LocalTargetInfo dest, TraverseParms traverseParms, PathEndMode pathEndMode)
            {
                this.start = start;
                this.dest = dest;
                this.traverseParms = traverseParms;
                this.pathEndMode = pathEndMode;
            }
        }

        protected TrailblazerPather(Map map)
        {
            this.map = map;
        }

        public PawnPath FindPath(IntVec3 start, LocalTargetInfo dest, TraverseParms traverseParms, PathEndMode pathEndMode)
        {
            //TODO add threading once this is all worked out
            PathfindRequest pathfindRequest = new PathfindRequest(start, dest, traverseParms, pathEndMode);
            return GetWorker(pathfindRequest).FindPath();
        }

        protected abstract TrailblazerPathWorker GetWorker(PathfindRequest pathfindRequest);


        /// <summary>
        /// Worker class that contains all the information needed for a single pathfinding operation.  Workers
        /// should be independant and capable of operating in their own threads
        /// </summary>
        protected abstract class TrailblazerPathWorker
        {
            // Pathfinding data
            protected readonly IntVec3 start;
            protected readonly LocalTargetInfo dest;
            protected readonly TraverseParms traverseParms;
            protected readonly PathEndMode pathEndMode;

            protected TrailblazerPathWorker(PathfindRequest pathfindRequest)
            {
                start = pathfindRequest.start;
                dest = pathfindRequest.dest;
                traverseParms = pathfindRequest.traverseParms;
                pathEndMode = pathfindRequest.pathEndMode;
            }

            public abstract PawnPath FindPath();
        }
    }
}
