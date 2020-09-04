using System;
using System.Linq;
using System.Collections.Generic;
using Priority_Queue;
using Trailblazer.Rules;
using Verse;
using Verse.AI;

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
        private SimplePriorityQueue<CellRef, int> rraOpenSet;
        private readonly Dictionary<CellRef, int> rraClosedSet;
        private readonly List<PassabilityRule> rraPassRules;
        private readonly List<CostRule> rraCostRules;

        public TrailblazerPather_TwinAStar(PathfindData pathfindData) : base(pathfindData)
        {
            rraOpenSet = new SimplePriorityQueue<CellRef, int>();
            rraClosedSet = new Dictionary<CellRef, int>();
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
                rraOpenSet.Enqueue(cellRef, 0);
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
        /// Initiates or resumes RRA* pathfinding on the region grid with the given target.
        /// 
        /// TODO rewrite to explain changes
        /// NOTE - This algorithm inverts regions and links.  The nodes are RegionLinks, and the edges are between every
        /// link of a region.  Cost is the octaline distance between the closest respective cell of each link.
        /// (The goal cell is also considered a node. It shares edges with every RegionLink belonging to its region)
        /// 
        /// </summary>
        /// <returns>The region link closest to the target cell</returns>
        /// <param name="targetCell">Target cell.</param>
        private void ReverseResumableAStar(CellRef targetCell)
        {
            // Rebuild the open set based on the new target
            var oldSet = rraOpenSet;
            rraOpenSet = new SimplePriorityQueue<CellRef, int>();
            foreach (CellRef cell in oldSet)
            {
                rraOpenSet.Enqueue(cell, rraClosedSet[cell] + RRAHeuristic(cell, targetCell));
            }

            int closedNodes = 0;
            while (rraOpenSet.Count > 0)
            {
                CellRef current = rraOpenSet.Dequeue();
                debugReplay.DrawCell(current);

                // Check if we've reached our goal
                if (current.Equals(targetCell))
                {
                    return;
                }

                if (closedNodes > SearchLimit)
                {
                    Log.Error("[Trailblazer] RRA* Heuristic closed too many cells, aborting");
                    return;
                }

                foreach (Direction direction in DirectionUtils.AllDirections)
                {
                    IntVec3 neighborCell = direction.From(current);
                    CellRef neighbor = map.GetCellRef(neighborCell);
                    if (neighborCell.InBounds(map))
                    {
                        MoveData moveData = new MoveData(neighbor, direction);

                        if (!rraPassRules.All(r => r.IsPassable(moveData)))
                            continue;

                        int newCost = rraClosedSet[current] + costRules.Sum(r => r.GetCost(moveData));
                        if (!rraClosedSet.ContainsKey(neighbor) || newCost < rraClosedSet[neighbor])
                        {
                            rraClosedSet[neighbor] = newCost;
                            int estimatedCost = newCost + RRAHeuristic(neighbor, targetCell);
                            if (!rraOpenSet.EnqueueWithoutDuplicates(neighbor, estimatedCost))
                            {
                                rraOpenSet.UpdatePriority(neighbor, estimatedCost);
                            }
                        }
                    }
                }
                debugReplay.NextFrame();
                closedNodes++;
            }

            Log.Error("[Trailblazer] RRA heuristic failed to reach target cell " + targetCell);
        }

        private int RRAHeuristic(CellRef start, CellRef target)
        {
            return DistanceBetween(start, target);
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
