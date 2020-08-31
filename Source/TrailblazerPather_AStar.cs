using System;
using System.Collections.Generic;
using Trailblazer.Rules;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Trailblazer
{
    /// <summary>
    /// Trailblazer pather that simply uses A* with the octaline distance heuristic
    /// This will perform worse than vanilla, particularly with long paths!
    /// </summary>
    public class TrailblazerPather_AStar : TrailblazerPather
    {
        private struct CostNode
        {
            public CellRef cellRef;
            public int cost;

            public CostNode(CellRef cellRef, int cost)
            {
                this.cellRef = cellRef;
                this.cost = cost;
            }
        }

        private class CostNodeComparer : IComparer<CostNode>
        {
            public int Compare(CostNode a, CostNode b)
            {
                return a.cost.CompareTo(b.cost);
            }
        }

        private struct PathFinderNode
        {
            public int knownCost;
            public int heuristicCost;
            public int totalCost;
            public CellRef parent;
            public bool visited;
        }

        public TrailblazerPather_AStar(Map map) : base(map) { }

        protected override TrailblazerPathWorker GetWorker(PathfindData pathfindData)
        {
            return new TrailblazerPathWorker_AStar(pathfindData);
        }

        protected class TrailblazerPathWorker_AStar : TrailblazerPathWorker
        {
            private readonly FastPriorityQueue<CostNode> openSet;
            private readonly PathFinderNode[] closedSet; // TODO - this used to be static.  Performance implications?

            public TrailblazerPathWorker_AStar(PathfindData pathfindData) : base(pathfindData)
            {
                closedSet = new PathFinderNode[pathfindData.map.Area];
                openSet = new FastPriorityQueue<CostNode>(new CostNodeComparer());
            }

            public override PawnPath FindPath()
            {
                Map map = pathfindData.map;

                int moveTicksCardinal;
                int moveTicksDiagonal;
                if (pathfindData.traverseParms.pawn != null)
                {
                    moveTicksCardinal = pathfindData.traverseParms.pawn.TicksPerMoveCardinal;
                    moveTicksDiagonal = pathfindData.traverseParms.pawn.TicksPerMoveDiagonal;
                }
                else
                {
                    moveTicksCardinal = DefaultMoveTicksCardinal;
                    moveTicksDiagonal = DefaultMoveTicksDiagonal;
                }

                CellRef start = pathfindData.start;
                CellRef dest = map.GetCellRef(pathfindData.dest.Cell);

                CellRect destRect = CalculateDestinationRect();
                List<int> disallowedCornerIndices = CalculateDisallowedCorners(destRect);
                bool destinationIsSingleCell = destRect.Width == 1 && destRect.Height == 1;

                closedSet[start].knownCost = 0;
                closedSet[start].heuristicCost = CalcHeuristicEstimate(start, dest, moveTicksCardinal, moveTicksDiagonal);
                closedSet[start].totalCost = closedSet[start].heuristicCost;
                closedSet[start].parent = null;
                closedSet[start].visited = true;

                openSet.Push(new CostNode(start, 0));

                int closedNodes = 0;
                while (openSet.Count != 0)
                {
                    CostNode currentNode = openSet.Pop();
                    CellRef current = currentNode.cellRef;

                    // We've already scanned this node and found a better path to it
                    if (currentNode.cost > closedSet[current].totalCost)
                    {
                        continue;
                    }

                    // Check if we've reached our goal
                    if (destinationIsSingleCell)
                    {
                        if (current == dest)
                        {
                            DebugDrawFinalPath(current);
                            return FinalizedPath(current);
                        }
                    }
                    else if (destRect.Contains(current.Cell) && !disallowedCornerIndices.Contains(current.Index))
                    {
                        DebugDrawFinalPath(current);
                        return FinalizedPath(current);
                    }

                    // Check if we hit the searchLimit
                    if (closedNodes > SearchLimit)
                    {
                        Log.Warning(pathfindData.traverseParms.pawn + " pathing from " + pathfindData.start + " to " +
                            pathfindData.dest + " hit search limit of " + SearchLimit + " cells.", false);
                        DebugDrawFinalPath();
                        return PawnPath.NotFound;
                    }
                    if (openSet.Count > SearchLimit)
                    {
                        Log.Warning(pathfindData.traverseParms.pawn + " pathing from " + pathfindData.start + " to " +
                            pathfindData.dest + " hit search limit of " + SearchLimit + " cells in the open set.", false);
                        DebugDrawFinalPath();
                        return PawnPath.NotFound;
                    }

                    foreach (Direction direction in DirectionUtils.AllDirections)
                    {
                        IntVec3 neighborCell = direction.From(current);
                        if (neighborCell.InBounds(map))
                        {
                            CellRef neighbor = map.GetCellRef(neighborCell);
                            MoveData moveData = new MoveData(neighbor, direction);
                            int? moveCost = CalcMoveCost(moveData);
                            if (moveCost == null)
                            {
                                continue;
                            }

                            int neighborNewCost = closedSet[current].knownCost + moveCost ?? 0;
                            if (!closedSet[neighbor].visited || closedSet[neighbor].knownCost > neighborNewCost)
                            {
                                if (!closedSet[neighbor].visited)
                                {
                                    closedSet[neighbor].heuristicCost = CalcHeuristicEstimate(neighbor, dest, moveTicksCardinal, moveTicksDiagonal);
                                    closedSet[neighbor].visited = true;
                                }

                                closedSet[neighbor].knownCost = neighborNewCost;
                                closedSet[neighbor].totalCost = neighborNewCost + closedSet[neighbor].heuristicCost;
                                closedSet[neighbor].parent = current;

                                openSet.Push(new CostNode(neighbor, closedSet[neighbor].knownCost + closedSet[neighbor].heuristicCost));
                            }
                        }
                    }
                    closedNodes++;
                }

                Pawn pawn = pathfindData.traverseParms.pawn;
                string currentJob = pawn?.CurJob?.ToString() ?? "null";
                string faction = pawn?.Faction?.ToString() ?? "null";
                Log.Warning(pawn + " pathing from " + pathfindData.start + " to " + dest + " ran out of cells to process.\n" +
                	"Job:" + currentJob + "\nFaction: " + faction, false);
                DebugDrawFinalPath();
                return PawnPath.NotFound;
            }

            private int CalcHeuristicEstimate(CellRef start, CellRef end, int cardinal, int diagonal)
            {
                int dx = Math.Abs(start.Cell.x - end.Cell.x);
                int dz = Math.Abs(start.Cell.z - end.Cell.z);
                return GenMath.OctileDistance(dx, dz, cardinal, diagonal);
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

            static ushort debugMat = 0;
            private void DebugDrawFinalPath(CellRef dest = null)
            {
                if (DebugViewSettings.drawPaths)
                {
                    float debugColor = (debugMat % 100) / 100f;
                    debugMat++;
                    //if (destIndex != -1)
                    //{
                    //    int pathIndex = destIndex;
                    //    while (pathIndex >= 0)
                    //    {
                    //        IntVec3 c = pathData.map.cellIndices.IndexToCell(pathIndex);
                    //        pathData.map.debugDrawer.FlashCell(c, debugColor, calcGrid[pathIndex].knownCost.ToString(), 50);
                    //        pathIndex = calcGrid[pathIndex].parentIndex;
                    //    }
                    //}
                    int mapCells = pathfindData.map.Area;
                    for (int i = 0; i < mapCells; i++)
                    {
                        if (closedSet[i].visited)
                        {
                            IntVec3 c = pathfindData.map.cellIndices.IndexToCell(i);
                            string costString = "{} / {}".Formatted(closedSet[i].knownCost, closedSet[i].totalCost);
                            pathfindData.map.debugDrawer.FlashCell(c, debugColor, costString, 50);
                        }
                    }

                    while (openSet.Count > 0)
                    {
                        CostNode costNode = openSet.Pop();
                        pathfindData.map.debugDrawer.FlashCell(costNode.cellRef, debugColor, "open", 50);
                    }
                }
            }

            // TODO move these to TrailblazerPather
            private CellRect CalculateDestinationRect()
            {
                CellRect result;
                if (pathfindData.dest.HasThing && pathfindData.pathEndMode != PathEndMode.OnCell)
                {
                    result = pathfindData.dest.Thing.OccupiedRect();
                }
                else
                {
                    result = CellRect.SingleCell(pathfindData.dest.Cell);
                }

                if (pathfindData.pathEndMode == PathEndMode.Touch)
                {
                    result = result.ExpandedBy(1);
                }
                return result;
            }

            private List<int> CalculateDisallowedCorners(CellRect destinationRect)
            {
                List<int> disallowedCornerIndices = new List<int>(4);
                if (pathfindData.pathEndMode == PathEndMode.Touch)
                {
                    int minX = destinationRect.minX;
                    int minZ = destinationRect.minZ;
                    int maxX = destinationRect.maxX;
                    int maxZ = destinationRect.maxZ;
                    if (!IsCornerTouchAllowed(minX + 1, minZ + 1, minX + 1, minZ, minX, minZ + 1))
                    {
                        disallowedCornerIndices.Add(pathfindData.map.cellIndices.CellToIndex(minX, minZ));
                    }
                    if (!IsCornerTouchAllowed(minX + 1, maxZ - 1, minX + 1, maxZ, minX, maxZ - 1))
                    {
                        disallowedCornerIndices.Add(pathfindData.map.cellIndices.CellToIndex(minX, maxZ));
                    }
                    if (!IsCornerTouchAllowed(maxX - 1, maxZ - 1, maxX - 1, maxZ, maxX, maxZ - 1))
                    {
                        disallowedCornerIndices.Add(pathfindData.map.cellIndices.CellToIndex(maxX, maxZ));
                    }
                    if (!IsCornerTouchAllowed(maxX - 1, minZ + 1, maxX - 1, minZ, maxX, minZ + 1))
                    {
                        disallowedCornerIndices.Add(pathfindData.map.cellIndices.CellToIndex(maxX, minZ));
                    }
                }
                return disallowedCornerIndices;
            }

            private bool IsCornerTouchAllowed(int cornerX, int cornerZ, int adjCardinal1X, int adjCardinal1Z, int adjCardinal2X, int adjCardinal2Z)
            {
                return TouchPathEndModeUtility.IsCornerTouchAllowed(cornerX, cornerZ, adjCardinal1X, adjCardinal1Z, adjCardinal2X, adjCardinal2Z, pathfindData.map);
            }
        }
    }
}
