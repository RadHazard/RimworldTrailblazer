using System;
using System.Collections.Generic;
using System.Linq;
using Trailblazer.Rules;
using Verse;

namespace Trailblazer
{
    /// <summary>
    /// Trailblazer pather that uses a RRA* implementation to guide the forward A* implementation.  Unlike HAStar, this
    /// one paths on the cell grid for both directions, but the RRA* only uses a subset of the passability and cost
    /// rules.
    /// </summary>
    public class TrailblazerPather_TwinAStar : TrailblazerPather_AStar
    {
        // RRA* params
        private Priority_Queue.FastPriorityQueue<CellRefNode> rraOpenSet;
        private readonly Dictionary<CellRef, int> rraClosedSet;

        private readonly Dictionary<CellRef, CellRefNode> cellRefNodeCache;

        private readonly List<PassabilityRule> rraPassRules;
        private readonly List<CostRule> rraCostRules;

        public TrailblazerPather_TwinAStar(PathfindData pathfindData) : base(pathfindData)
        {
            rraOpenSet = new Priority_Queue.FastPriorityQueue<CellRefNode>(map.Area);
            rraClosedSet = new Dictionary<CellRef, int>();

            cellRefNodeCache = new Dictionary<CellRef, CellRefNode>();

            rraPassRules = new PassabilityRule[]
            {
                new PassabilityRule_PathGrid(pathfindData),
                new PassabilityRule_DoorByPawn(pathfindData),
                new PassabilityRule_NoPassDoors(pathfindData)
            }.Where(r => r.Applies()).ToList();
            rraCostRules = new CostRule[]
            {
                new CostRule_PathGrid(pathfindData),
                new CostRule_Walls(pathfindData),
                new CostRule_MoveTicks(pathfindData)
            }.Where(r => r.Applies()).ToList();

            // Initialize the RRA* algorithm
            foreach (IntVec3 cell in pathfindData.DestRect)
            {
                CellRef cellRef = pathfindData.map.GetCellRef(cell);
                rraClosedSet[cellRef] = 0;
                rraOpenSet.Enqueue(GetNode(cellRef), 0);
            }
        }

        protected override int Heuristic(CellRef cell)
        {
            if (!rraClosedSet.ContainsKey(cell))
            {
                ReverseResumableAStar(cell);
            }
            return rraClosedSet[cell];
        }

        /// <summary>
        /// Initiates or resumes RRA* pathfinding to the given target.
        /// This variant of RRA* paths on the same grid as the main A* pather but only uses a subset of rules
        /// </summary>
        /// <returns>The region link closest to the target cell</returns>
        /// <param name="targetCell">Target cell.</param>
        private void ReverseResumableAStar(CellRef targetCell)
        {
            performanceTracker.StartInvocation("RRA");
            performanceTracker.StartInvocation("RRA Reprioritize");
            // Rebuild the open set based on the new target
            CellRefNode[] cachedNodes = rraOpenSet.ToArray(); // Cache the nodes because we'll be messing with the queue
            foreach (CellRefNode cell in cachedNodes)
            {
                rraOpenSet.UpdatePriority(cell, rraClosedSet[cell] + RRAHeuristic(cell, targetCell));
            }
            performanceTracker.EndInvocation("RRA Reprioritize");

            int closedNodes = 0;
            while (rraOpenSet.Count > 0)
            {
                CellRef current = rraOpenSet.Dequeue();
                debugReplay.DrawCell(current);
                performanceTracker.Count("RRA Closed");

                // Check if we've reached our goal
                if (current.Equals(targetCell))
                {
                    performanceTracker.EndInvocation("RRA");
                    return;
                }

                if (closedNodes > SearchLimit)
                {
                    Log.Error("[Trailblazer] RRA* Heuristic closed too many cells, aborting");
                    performanceTracker.EndInvocation("RRA");
                    return;
                }

                foreach (Direction direction in DirectionUtils.AllDirections)
                {
                    IntVec3 neighborCell = direction.From(current);
                    CellRef neighbor = map.GetCellRef(neighborCell);

                    performanceTracker.StartInvocation("RRA Bounds Check");
                    bool inBounds = neighborCell.InBounds(map);
                    performanceTracker.EndInvocation("RRA Bounds Check");
                    if (inBounds)
                    {
                        MoveData moveData = new MoveData(neighbor, direction);

                        performanceTracker.StartInvocation("RRA Move Check");
                        bool passable = rraPassRules.All(r => r.IsPassable(moveData));
                        performanceTracker.EndInvocation("RRA Move Check");
                        if (!passable)
                            continue;

                        performanceTracker.StartInvocation("RRA Move Cost");
                        int newCost = rraClosedSet[current] + costRules.Sum(r => r.GetCost(moveData));
                        performanceTracker.EndInvocation("RRA Move Cost");
                        if (!rraClosedSet.ContainsKey(neighbor) || newCost < rraClosedSet[neighbor])
                        {
                            if (rraClosedSet.ContainsKey(neighbor))
                            {
                                performanceTracker.Count("RRA Reopened");
                            }
                            else
                            {
                                performanceTracker.Count("RRA New Open");
                            }

                            rraClosedSet[neighbor] = newCost;
                            int estimatedCost = newCost + RRAHeuristic(neighbor, targetCell);

                            performanceTracker.StartInvocation("RRA Enqueue");
                            CellRefNode neighborNode = GetNode(neighbor);
                            if (rraOpenSet.Contains(neighborNode))
                            {
                                rraOpenSet.UpdatePriority(neighborNode, estimatedCost);
                            }
                            else
                            {
                                rraOpenSet.Enqueue(neighborNode, estimatedCost);
                            }
                            performanceTracker.EndInvocation("RRA Enqueue");
                        }
                        else
                        {
                            performanceTracker.Count("RRA Rescanned");
                        }
                    }
                }
                debugReplay.NextFrame();
                closedNodes++;
            }

            Log.Error("[Trailblazer] RRA heuristic failed to reach target cell " + targetCell);
            performanceTracker.EndInvocation("RRA");
        }

        private int RRAHeuristic(CellRef start, CellRef target)
        {
            performanceTracker.StartInvocation("RRA Heuristic");
            int dist = DistanceBetween(start, target);
            performanceTracker.EndInvocation("RRA Heuristic");
            return dist;
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

        // === Utility methods ===

        /// <summary>
        /// Calculates the shortest octile distance between two cells
        /// </summary>
        /// <returns>The distance between the cells.</returns>
        /// <param name="cellA">First cell.</param>
        /// <param name="cellB">Second cell.</param>
        private int DistanceBetween(IntVec3 cellA, IntVec3 cellB)
        {
            int dx = Math.Abs(cellA.x - cellB.x);
            int dz = Math.Abs(cellA.z - cellB.z);
            return GenMath.OctileDistance(dx, dz, moveTicksCardinal, moveTicksDiagonal);
        }
    }
}
