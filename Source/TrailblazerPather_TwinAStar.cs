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

        private readonly PathfinderGrid rraPathfinderGrid;

        public TrailblazerPather_TwinAStar(PathfindData pathfindData) : base(pathfindData)
        {
            rraOpenSet = new Priority_Queue.FastPriorityQueue<CellRefNode>(map.Area);
            rraClosedSet = new Dictionary<CellRef, int>();

            cellRefNodeCache = new Dictionary<CellRef, CellRefNode>();

            var cellPassRules = ThatApply<CellPassabilityRule>(
                new CellPassabilityRule_PathGrid(pathfindData),
                new CellPassabilityRule_DoorByPawn(pathfindData),
                new CellPassabilityRule_NoPassDoors(pathfindData)
            );

            var passRules = Enumerable.Empty<PassabilityRule>();

            var cellCostRules = ThatApply<CellCostRule>(
                new CellCostRule_PathGrid(pathfindData),
                new CellCostRule_Walls(pathfindData)
            );

            var costRules = ThatApply<CostRule>(new CostRule_MoveTicks(pathfindData));
            rraPathfinderGrid = new PathfinderGrid(cellCostRules, costRules, cellPassRules, passRules);

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
            ProfilerStart("RRA");
            ProfilerStart("RRA Reprioritize");
            // Rebuild the open set based on the new target
            CellRefNode[] cachedNodes = rraOpenSet.ToArray(); // Cache the nodes because we'll be messing with the queue
            foreach (CellRefNode cell in cachedNodes)
            {
                rraOpenSet.UpdatePriority(cell, rraClosedSet[cell] + RRAHeuristic(cell, targetCell));
            }
            ProfilerEnd("RRA Reprioritize");

            int closedNodes = 0;
            while (rraOpenSet.Count > 0)
            {
                CellRef current = rraOpenSet.Dequeue();
                debugReplay.DrawCell(current);
                ProfilerCount("RRA Closed");

                // Check if we've reached our goal
                if (current.Equals(targetCell))
                {
                    ProfilerEnd("RRA");
                    return;
                }

                if (closedNodes > SearchLimit)
                {
                    Log.Error("[Trailblazer] RRA* Heuristic closed too many cells, aborting");
                    ProfilerEnd("RRA");
                    return;
                }

                foreach (Direction direction in DirectionUtils.AllDirections)
                {
                    IntVec3 neighborCell = direction.From(current);
                    CellRef neighbor = map.GetCellRef(neighborCell);

                    ProfilerStart("RRA Bounds Check");
                    bool inBounds = neighborCell.InBounds(map);
                    ProfilerEnd("RRA Bounds Check");
                    if (inBounds)
                    {
                        MoveData moveData = new MoveData(neighbor, direction);

                        ProfilerStart("RRA Move Check");
                        bool passable = rraPathfinderGrid.MoveIsValid(moveData);
                        ProfilerEnd("RRA Move Check");
                        if (!passable)
                            continue;

                        ProfilerStart("RRA Move Cost");
                        int newCost = rraClosedSet[current] + rraPathfinderGrid.MoveCost(moveData);
                        ProfilerEnd("RRA Move Cost");
                        if (!rraClosedSet.ContainsKey(neighbor) || newCost < rraClosedSet[neighbor])
                        {
                            if (rraClosedSet.ContainsKey(neighbor))
                            {
                                ProfilerCount("RRA Reopened");
                            }
                            else
                            {
                                ProfilerCount("RRA New Open");
                            }

                            rraClosedSet[neighbor] = newCost;
                            int estimatedCost = newCost + RRAHeuristic(neighbor, targetCell);

                            ProfilerStart("RRA Enqueue");
                            CellRefNode neighborNode = GetNode(neighbor);
                            if (rraOpenSet.Contains(neighborNode))
                            {
                                rraOpenSet.UpdatePriority(neighborNode, estimatedCost);
                            }
                            else
                            {
                                rraOpenSet.Enqueue(neighborNode, estimatedCost);
                            }
                            ProfilerEnd("RRA Enqueue");
                        }
                        else
                        {
                            ProfilerEnd("RRA Rescanned");
                        }
                    }
                }
                debugReplay.NextFrame();
                closedNodes++;
            }

            Log.Error("[Trailblazer] RRA heuristic failed to reach target cell " + targetCell);
            ProfilerEnd("RRA");
        }

        private int RRAHeuristic(CellRef start, CellRef target)
        {
            ProfilerStart("RRA Heuristic");
            int dist = DistanceBetween(start, target);
            ProfilerEnd("RRA Heuristic");
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
