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
    /// one paths on the cell grid for both directions, but the RRA* only counts terrain and not the full ruleset.
    /// </summary>
    public class TrailblazerPather_TwinAStar : TrailblazerPather
    {
        // Pathing cost constants
        private const int SearchLimit = 160000;

        private class CellNode
        {
            public int knownCost;
            public int heuristicCost;
            public int TotalCost => knownCost + heuristicCost;
            public CellRef parent;
        }

        // Main A* params
        private readonly SimplePriorityQueue<CellRef, int> openSet;
        private readonly Dictionary<CellRef, CellNode> closedSet;

        // RRA* params
        private SimplePriorityQueue<CellRef, int> rraOpenSet;
        private readonly Dictionary<CellRef, int> rraClosedSet;
        private readonly bool passDestroyableThings;

        // Shared params
        private readonly Map map;
        private readonly CellRef startCell;
        private readonly CellRef destCell;
        private readonly int moveTicksCardinal;
        private readonly int moveTicksDiagonal;

        // Debug
        private static ushort debugMat = 0;
        private readonly TrailblazerDebugVisualizer debugVisualizer;
        private readonly TrailblazerDebugVisualizer.InstantReplay debugReplay;

        public TrailblazerPather_TwinAStar(PathfindData pathfindData) : base(pathfindData)
        {
            openSet = new SimplePriorityQueue<CellRef, int>();
            closedSet = new Dictionary<CellRef, CellNode>();

            rraOpenSet = new SimplePriorityQueue<CellRef, int>();
            rraClosedSet = new Dictionary<CellRef, int>();
            passDestroyableThings = pathfindData.traverseParms.mode.CanDestroy();

            map = pathfindData.map;
            startCell = pathfindData.start;
            destCell = pathfindData.map.GetCellRef(pathfindData.dest.Cell);

            if (pathfindData.traverseParms.pawn != null)
            {
                moveTicksCardinal = pathfindData.traverseParms.pawn.TicksPerMoveCardinal;
                moveTicksDiagonal = pathfindData.traverseParms.pawn.TicksPerMoveDiagonal;
            }
            else
            {
                moveTicksCardinal = TrailblazerRule_CostMoveTicks.DefaultMoveTicksCardinal;
                moveTicksDiagonal = TrailblazerRule_CostMoveTicks.DefaultMoveTicksDiagonal;
            }

            debugMat++;
            debugVisualizer = pathfindData.map.GetComponent<TrailblazerDebugVisualizer>();
            debugReplay = debugVisualizer.CreateNewReplay();
        }

        public override PawnPath FindPath()
        {
            // Initialize the RRA* algorithm
            foreach (IntVec3 cell in pathfindData.DestRect)
            {
                CellRef cellRef = pathfindData.map.GetCellRef(cell);
                rraClosedSet[cellRef] = 0;
                rraOpenSet.Enqueue(cellRef, 0);
            }

            // Initialize the main A* algorithm
            closedSet[startCell] = new CellNode
            {
                knownCost = 0,
                heuristicCost = Heuristic(startCell),
                parent = null
            };
            openSet.Enqueue(startCell, 0);

            int closedNodes = 0;
            while (openSet.Count > 0)
            {
                CellRef current = openSet.Dequeue();
                debugReplay.DrawCell(current);
                debugReplay.NextFrame();

                // Check if we've reached our goal
                if (pathfindData.CellIsInDestination(current))
                {
                    //TODO
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
                    DebugDrawFinalPath();
                    //debugVisualizer.RegisterReplay(debugReplay);
                    return PawnPath.NotFound;
                }

                foreach (Direction direction in DirectionUtils.AllDirections)
                {
                    IntVec3 neighborCell = direction.From(current);
                    if (neighborCell.InBounds(map))
                    {
                        CellRef neighbor = map.GetCellRef(neighborCell);
                        //debugReplay.DrawLine(current, neighbor);
                        //debugReplay.NextFrame();

                        MoveData moveData = new MoveData(neighbor, direction);
                        int? moveCost = CalcMoveCost(moveData);
                        if (moveCost == null)
                        {
                            continue;
                        }

                        int neighborNewCost = closedSet[current].knownCost + moveCost ?? 0;
                        if (!closedSet.ContainsKey(neighbor) || closedSet[neighbor].knownCost > neighborNewCost)
                        {
                            if (!closedSet.ContainsKey(neighbor))
                            {
                                closedSet[neighbor] = new CellNode
                                {
                                    heuristicCost = Heuristic(neighbor)
                                };
                            }
                            closedSet[neighbor].knownCost = neighborNewCost;
                            closedSet[neighbor].parent = current;

                            if (!openSet.EnqueueWithoutDuplicates(neighbor, closedSet[neighbor].TotalCost))
                            {
                                openSet.UpdatePriority(neighbor, closedSet[neighbor].TotalCost);
                            }
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
            DebugDrawFinalPath();
            //debugVisualizer.RegisterReplay(debugReplay);
            return PawnPath.NotFound;
        }

        private int Heuristic(CellRef cell)
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

                //foreach (CellRef neighbor in current.Neighbors())
                foreach (Direction direction in DirectionUtils.AllDirections)
                {
                    IntVec3 neighborCell = direction.From(current);
                    CellRef neighbor = map.GetCellRef(neighborCell);
                    if (neighborCell.InBounds(map))
                    {
                        int moveCost = direction.IsDiagonal() ? moveTicksDiagonal : moveTicksCardinal;
                        if (!map.pathGrid.WalkableFast(neighbor.Index))
                        {
                            Building building = map.edificeGrid[neighbor.Index];
                            if (!passDestroyableThings || building == null || !PathFinder.IsDestroyable(building))
                            {
                                continue;
                            }

                            moveCost += TrailblazerRule_TestBuildings.Cost_BlockedWallBase;
                            moveCost += (int)(building.HitPoints * TrailblazerRule_TestBuildings.Cost_BlockedWallExtraPerHitPoint);
                        }
                        else
                        {
                            moveCost += map.pathGrid.pathGrid[neighbor];
                        }

                        int newCost = rraClosedSet[current] + moveCost;
                        if (!rraClosedSet.ContainsKey(neighbor) || newCost < rraClosedSet[neighbor])
                        {
                            rraClosedSet[neighbor] = newCost;
                            int estimatedCost = newCost + RRAHeuristic(neighbor, targetCell);
                            if (!rraOpenSet.EnqueueWithoutDuplicates(neighbor, estimatedCost))
                            {
                                rraOpenSet.UpdatePriority(neighbor, estimatedCost);
                            }
                            //DebugDrawRegionNode(neighbor, string.Format("{0} ({1})", newCost, moveCost));
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

        // === Debug methods ===

        private void FlashCell(IntVec3 cell, string text, int duration, float offset = 0f)
        {
            pathfindData.map.debugDrawer.FlashCell(cell, (debugMat % 100 / 100f) + offset, text, duration);
        }

        private IntVec3 DebugFindLinkCenter(RegionLink link)
        {
            if (link.span.dir == SpanDirection.North)
            {
                return link.span.root + new IntVec3(0, 0, link.span.length / 2);
            }
            return link.span.root + new IntVec3(link.span.length / 2, 0, 0);
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

                foreach (CellRef cell in rraClosedSet.Keys)
                {
                    if (!closedSet.ContainsKey(cell))
                    {
                        FlashCell(cell, null, 50, 0.05f);
                    }
                }
            }
        }
    }
}
