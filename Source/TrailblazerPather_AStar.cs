using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Priority_Queue;
using Trailblazer.Debug;
using Trailblazer.Rules;
using Verse;
using Verse.AI;

namespace Trailblazer
{
    /// <summary>
    /// Trailblazer pather that uses A*
    /// The basic implimentation uses a simple Octaline heuristic, but can be overridden
    /// </summary>
    public class TrailblazerPather_AStar : TrailblazerPather
    {
        // Pathing cost constants
        protected const int SearchLimit = 160000;

        protected class CellRefNode : FastPriorityQueueNode
        {
            public readonly CellRef cell;

            public CellRefNode(CellRef cell)
            {
                this.cell = cell;
            }

            public static implicit operator CellRef(CellRefNode cellRefNode)
            {
                return cellRefNode.cell;
            }
        }

        private class CellNode
        {
            public int knownCost;
            public int heuristicCost;
            public int TotalCost => knownCost + heuristicCost;
            public CellRef parent;
        }

        // Main A* params
        private readonly Priority_Queue.FastPriorityQueue<CellRefNode> openSet;
        private readonly Dictionary<CellRef, CellNode> closedSet;
        private readonly Dictionary<CellRef, CellRefNode> cellRefNodeCache;

        protected readonly Map map;
        protected readonly CellRef startCell;
        protected readonly CellRef destCell;

        protected readonly int moveTicksCardinal;
        protected readonly int moveTicksDiagonal;

        // Debug
        private static ushort debugMat = 0;
        protected readonly TrailblazerDebugVisualizer debugVisualizer;
        protected readonly TrailblazerDebugVisualizer.InstantReplay debugReplay;
#if PROFILE
        protected readonly PerformanceTracker performanceTracker;
#endif

        public TrailblazerPather_AStar(PathfindData pathfindData) : base(pathfindData)
        {
            map = pathfindData.map;

            openSet = new Priority_Queue.FastPriorityQueue<CellRefNode>(map.Area);
            closedSet = new Dictionary<CellRef, CellNode>();

            cellRefNodeCache = new Dictionary<CellRef, CellRefNode>();

            startCell = pathfindData.start;
            destCell = pathfindData.map.GetCellRef(pathfindData.dest.Cell);

            CostRule_MoveTicks.GetMoveTicks(pathfindData, out moveTicksCardinal, out moveTicksDiagonal);

            debugMat++;
            debugVisualizer = pathfindData.map.GetComponent<TrailblazerDebugVisualizer>();
            debugReplay = debugVisualizer.CreateNewReplay();
#if PROFILE
            performanceTracker = new PerformanceTracker();
#endif
        }

        public override PawnPath FindPath()
        {
            ProfilerStart("Total Time");
            ProfilerStart("Heuristic");
            int h = Heuristic(startCell);
            ProfilerEnd("Heuristic");

            closedSet[startCell] = new CellNode
            {
                knownCost = 0,
                heuristicCost = h,
                parent = null
            };
            openSet.Enqueue(GetNode(startCell), 0);

            int closedNodes = 0;
            while (openSet.Count > 0)
            {
                //TODO
                CellRefNode current = Dequeue(openSet);
                //CellRef current = openSet.Dequeue();
                debugReplay.DrawCell(current);
                debugReplay.NextFrame();

                ProfilerCount("Closed Nodes");

                // Check if we've reached our goal
                if (pathfindData.CellIsInDestination(current))
                {
                    //TODO
                    ProfilerListStats();
                    DebugDrawFinalPath();
                    //debugVisualizer.RegisterReplay(debugReplay);
                    return FinalizedPath(current);
                }

                // Check if we hit the searchLimit
                if (closedNodes > SearchLimit)
                {
                    Log.Warning("[Trailblazer] " + pathfindData.traverseParms.pawn + " pathing from " + startCell +
                        " to " + destCell + " hit search limit of " + SearchLimit + " cells.", false);
                    //TODO
                    ProfilerListStats();
                    DebugDrawFinalPath();
                    //debugVisualizer.RegisterReplay(debugReplay);
                    return PawnPath.NotFound;
                }

                foreach (Direction direction in DirectionUtils.AllDirections)
                {
                    IntVec3 neighborCell = direction.From(current.cell);
                    if (neighborCell.InBounds(map))
                    {
                        CellRef neighbor = map.GetCellRef(neighborCell);
                        //debugReplay.DrawLine(current, neighbor);
                        //debugReplay.NextFrame();

                        ProfilerStart("Calc Valid Move");
                        MoveData moveData = new MoveData(neighbor, direction);
                        if (!MoveIsValid(moveData))
                        {
                            ProfilerCount("Invalid moves");
                            continue;
                        }
                        ProfilerEnd("Calc Valid Move");

                        ProfilerStart("Calc Move Cost");
                        int neighborNewCost = closedSet[current].knownCost + CalcMoveCost(moveData);
                        ProfilerStart("Calc Move Cost");

                        if (!closedSet.ContainsKey(neighbor) || closedSet[neighbor].knownCost > neighborNewCost)
                        {
                            if (!closedSet.ContainsKey(neighbor))
                            {
                                ProfilerStart("Heuristic");
                                int heuristic = Heuristic(neighbor);
                                ProfilerEnd("Heuristic");

                                closedSet[neighbor] = new CellNode
                                {
                                    heuristicCost = heuristic
                                };
                                ProfilerCount("New Open Nodes");
                            }
                            else
                            {
                                ProfilerCount("Reopened Nodes");
                            }
                            closedSet[neighbor].knownCost = neighborNewCost;
                            closedSet[neighbor].parent = current;

                            ProfilerCount("Opened Nodes");

                            //TODO
                            Enqueue(openSet, GetNode(neighbor), closedSet[neighbor].TotalCost);
                            //if (!openSet.EnqueueWithoutDuplicates(neighbor, closedSet[neighbor].TotalCost))
                            //{
                            //    openSet.UpdatePriority(neighbor, closedSet[neighbor].TotalCost);
                            //}
                            ProfilerMax("Max Open Set", openSet.Count);
                        }
                        else
                        {
                            ProfilerCount("Rescanned Nodes");
                        }
                    }
                }
                closedNodes++;
            }

            Pawn pawn = pathfindData.traverseParms.pawn;
            string currentJob = pawn?.CurJob?.ToString() ?? "null";
            string faction = pawn?.Faction?.ToString() ?? "null";
            Log.Warning("[Trailblazer] " + pawn + " pathing from " + startCell + " to " + destCell +
                " ran out of cells to process.\n" + "Job:" + currentJob + "\nFaction: " + faction, false);
            //TODO
            ProfilerListStats();
            DebugDrawFinalPath();
            //debugVisualizer.RegisterReplay(debugReplay);
            return PawnPath.NotFound;
        }

        protected virtual int Heuristic(CellRef cell)
        {
            int dx = Math.Abs(cell.Cell.x - destCell.Cell.x);
            int dz = Math.Abs(cell.Cell.z - destCell.Cell.z);
            return GenMath.OctileDistance(dx, dz, moveTicksCardinal, moveTicksDiagonal);
        }

        /// <summary>
        /// Converts a CellRef to a CellRefNode.  Caches nodes to ensure Contains() and similar methods function
        /// properly on the priority queue.
        /// </summary>
        /// <returns>The node.</returns>
        /// <param name="cellRef">Cell reference.</param>
        private CellRefNode GetNode(CellRef cellRef)
        {
            if (!cellRefNodeCache.ContainsKey(cellRef))
            {
                cellRefNodeCache[cellRef] = new CellRefNode(cellRef);
            }
            return cellRefNodeCache[cellRef];
        }

        private PawnPath FinalizedPath(CellRef final)
        {
            PawnPath emptyPawnPath = pathfindData.map.pawnPathPool.GetEmptyPawnPath();
            CellRef cell = final;
            while (cell != null)
            {
                emptyPawnPath.AddNode(cell);
                cell = closedSet[cell].parent;
            }
            emptyPawnPath.SetupFound(closedSet[final].knownCost, false);
            return emptyPawnPath;
        }

        // === Debug methods ===
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void ProfilerStart(string key)
        {
#if PROFILE
            performanceTracker.StartInvocation(key);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void ProfilerEnd(string key)
        {
#if PROFILE
            performanceTracker.EndInvocation(key);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void ProfilerCount(string key, int count = 1)
        {
#if PROFILE
            performanceTracker.Count(key, count);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void ProfilerMax(string key, int count = 1)
        {
#if PROFILE
            performanceTracker.Max(key, count);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProfilerListStats()
        {
#if PROFILE
            performanceTracker.EndInvocation("Total Time");
            Log.Message(performanceTracker.GetSummary());
#endif
        }

        // Used as hooks for Dubs Performance Analyzer because it can't see the Priority_Queue assembly
        private CellRefNode Dequeue(Priority_Queue.FastPriorityQueue<CellRefNode> queue)
        {
            ProfilerStart("Dequeue");
            CellRefNode node = queue.Dequeue();
            ProfilerEnd("Dequeue");
            return node;
        }

        private void Enqueue(Priority_Queue.FastPriorityQueue<CellRefNode> queue, CellRefNode node, int priority)
        {
            ProfilerStart("Enqueue");
            if (queue.Contains(node))
            {
                queue.UpdatePriority(node, priority);
            }
            else
            {
                queue.Enqueue(node, priority);
            }
            ProfilerEnd("Enqueue");
        }

        protected void FlashCell(IntVec3 cell, string text, int duration, float offset = 0f)
        {
            pathfindData.map.debugDrawer.FlashCell(cell, (debugMat % 100 / 100f) + offset, text, duration);
        }

        private void DebugDrawFinalPath()
        {
            if (DebugViewSettings.drawPaths)
            {
                int mapCells = pathfindData.map.Area;
                foreach (KeyValuePair<CellRef, CellNode> pair in closedSet)
                {
                    string costString = string.Format("{0} + {1} = {2}", pair.Value.knownCost, pair.Value.heuristicCost, pair.Value.TotalCost);
                    FlashCell(pair.Key, costString, 50);
                }

                foreach (CellRef cell in openSet)
                {
                    FlashCell(cell, null, 50);
                }
            }
        }
    }
}
