using System;
using System.Collections.Generic;
using Trailblazer.Rules;
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
            public int index;
            public int cost;

            public CostNode(int index, int cost)
            {
                this.index = index;
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
            public int parentIndex;
            public int totalCost;
            public bool visited;
        }

        public TrailblazerPather_AStar(Map map) : base(map) { }

        protected override TrailblazerPathWorker GetWorker(PathfindRequest pathfindRequest)
        {
            return new TrailblazerPathWorker_Vanilla(map, pathfindRequest);
        }

        protected class TrailblazerPathWorker_Vanilla : TrailblazerPathWorker
        {
            private readonly FastPriorityQueue<CostNode> openSet;
            private readonly PathFinderNode[] calcGrid; // TODO - this used to be static.  Performance implications?
            private readonly RegionCostCalculatorWrapper regionCostCalculator;

            public TrailblazerPathWorker_Vanilla(Map map, PathfindRequest pathfindRequest) : base(map, pathfindRequest)
            {
                calcGrid = new PathFinderNode[map.Size.x * map.Size.z];
                openSet = new FastPriorityQueue<CostNode>(new CostNodeComparer());
                regionCostCalculator = new RegionCostCalculatorWrapper(map);
            }

            public override PawnPath FindPath()
            {
                Map map = pathData.map;

                int moveTicksCardinal;
                int moveTicksDiagonal;
                if (pathData.traverseParms.pawn != null)
                {
                    moveTicksCardinal = pathData.traverseParms.pawn.TicksPerMoveCardinal;
                    moveTicksDiagonal = pathData.traverseParms.pawn.TicksPerMoveDiagonal;
                }
                else
                {
                    moveTicksCardinal = DefaultMoveTicksCardinal;
                    moveTicksDiagonal = DefaultMoveTicksDiagonal;
                }

                IntVec3 dest = pathData.dest.Cell;
                int destIndex = CellToIndex(dest);
                CellRect destRect = CalculateDestinationRect();
                List<int> disallowedCornerIndices = CalculateDisallowedCorners(destRect);
                bool destinationIsSingleCell = destRect.Width == 1 && destRect.Height == 1;

                int startIndex = CellToIndex(pathData.start);
                calcGrid[startIndex].knownCost = 0;
                calcGrid[startIndex].heuristicCost = CalcHeuristicEstimate(pathData.start, dest, moveTicksCardinal, moveTicksDiagonal);
                calcGrid[startIndex].totalCost = calcGrid[startIndex].heuristicCost;
                calcGrid[startIndex].parentIndex = -1;
                calcGrid[startIndex].visited = true;

                openSet.Push(new CostNode(startIndex, 0));

                int closedNodes = 0;
                while (openSet.Count != 0)
                {
                    CostNode current = openSet.Pop();
                    int currentIndex = current.index;
                    IntVec3 currentCell = IndexToCell(currentIndex);

                    // We've already scanned this node and found a better path to it
                    if (current.cost > calcGrid[currentIndex].totalCost)
                    {
                        continue;
                    }

                    // Check if we've reached our goal
                    if (destinationIsSingleCell)
                    {
                        if (currentIndex == destIndex)
                        {
                            DebugDrawRichData(currentIndex);
                            return FinalizedPath(currentIndex);
                        }
                    }
                    else if (destRect.Contains(currentCell) && !disallowedCornerIndices.Contains(currentIndex))
                    {
                        DebugDrawRichData(currentIndex);
                        return FinalizedPath(currentIndex);
                    }

                    // Check if we hit the searchLimit
                    if (closedNodes > SearchLimit)
                    {
                        Log.Warning(pathData.traverseParms.pawn + " pathing from " + pathData.start + " to " + dest +
                        	" hit search limit of " + SearchLimit + " cells.", false);
                        DebugDrawRichData();
                        return PawnPath.NotFound;
                    }
                    if (openSet.Count > SearchLimit)
                    {
                        Log.Warning(pathData.traverseParms.pawn + " pathing from " + pathData.start + " to " + dest +
                            " hit search limit of " + SearchLimit + " cells in the open set.", false);
                        DebugDrawRichData();
                        return PawnPath.NotFound;
                    }

                    foreach (Direction direction in DirectionUtils.AllDirections)
                    {
                        int neighborIndex = IndexFrom(direction, current.index);
                        if (neighborIndex >= 0 && neighborIndex < calcGrid.Length)
                        {
                            IntVec3 neighborCell = IndexToCell(neighborIndex);
                            MoveData moveData = new MoveData(neighborCell, neighborIndex, direction);
                            int? moveCost = CalcMoveCost(moveData);
                            if (moveCost == null)
                            {
                                continue;
                            }

                            int neighborNewCost = calcGrid[currentIndex].knownCost + moveCost ?? 0;
                            if (!calcGrid[neighborIndex].visited || calcGrid[neighborIndex].knownCost > neighborNewCost)
                            {
                                if (!calcGrid[neighborIndex].visited)
                                {
                                    calcGrid[neighborIndex].heuristicCost = CalcHeuristicEstimate(neighborCell, dest, moveTicksCardinal, moveTicksDiagonal);
                                    calcGrid[neighborIndex].visited = true;
                                }

                                calcGrid[neighborIndex].knownCost = neighborNewCost;
                                calcGrid[neighborIndex].totalCost = neighborNewCost + calcGrid[neighborIndex].heuristicCost;
                                calcGrid[neighborIndex].parentIndex = currentIndex;

                                openSet.Push(new CostNode(neighborIndex, calcGrid[neighborIndex].knownCost + calcGrid[neighborIndex].heuristicCost));
                            }
                        }
                    }
                    closedNodes++;
                }

                Pawn pawn = pathData.traverseParms.pawn;
                string currentJob = pawn?.CurJob?.ToString() ?? "null";
                string faction = pawn?.Faction?.ToString() ?? "null";
                Log.Warning(pawn + " pathing from " + pathData.start + " to " + dest + " ran out of cells to process.\n" +
                	"Job:" + currentJob + "\nFaction: " + faction, false);
                DebugDrawRichData();
                return PawnPath.NotFound;
            }

            private IntVec3 IndexToCell(int index)
            {
                return pathData.map.cellIndices.IndexToCell(index);
            }

            private int CellToIndex(IntVec3 cell)
            {
                return pathData.map.cellIndices.CellToIndex(cell);
            }

            private int CalcHeuristicEstimate(IntVec3 start, IntVec3 end, int cardinal, int diagonal)
            {
                int dx = Math.Abs(start.x - end.x);
                int dz = Math.Abs(start.z - end.z);
                return GenMath.OctileDistance(dx, dz, cardinal, diagonal);
            }

            /// <summary>
            /// Faster implementation of Direction.From using index math (which is map-size dependant)
            /// </summary>
            /// <returns>The from.</returns>
            /// <param name="direction">Direction.</param>
            /// <param name="index">Index.</param>
            private int IndexFrom(Direction direction, int index)
            {
                int mapSizeX = pathData.map.Size.x;
                switch (direction)
                {                
                    case Direction.N:
                        return index + mapSizeX;
                    case Direction.NE:
                        return index + mapSizeX + 1;
                    case Direction.E:
                        return index + 1;
                    case Direction.SE:
                        return index - mapSizeX + 1;
                    case Direction.S:
                        return index - mapSizeX;
                    case Direction.SW:
                        return index - mapSizeX - 1;
                    case Direction.W:
                        return index - 1;
                    case Direction.NW:
                        return index + mapSizeX - 1;
                    default:
                        throw new Exception("Moved in an invalid direction");
                }
            }

            private void DebugDrawRichData(int destIndex = -1)
            {
                if (DebugViewSettings.drawPaths)
                {
                    if (destIndex != -1)
                    {
                        int pathIndex = destIndex;
                        while (pathIndex >= 0)
                        {
                            IntVec3 c = pathData.map.cellIndices.IndexToCell(pathIndex);
                            pathData.map.debugDrawer.FlashCell(c, 0f, calcGrid[pathIndex].totalCost.ToString(), 75);
                            pathIndex = calcGrid[pathIndex].parentIndex;
                        }
                    }

                    while (openSet.Count > 0)
                    {
                        CostNode costNode = openSet.Pop();
                        IntVec3 c = pathData.map.cellIndices.IndexToCell(costNode.index);
                        pathData.map.debugDrawer.FlashCell(c, 0f, "open", 50);
                    }
                }
            }

            // TODO ======= duplicate methods ========
            private PawnPath FinalizedPath(int finalIndex)
            {
                PawnPath emptyPawnPath = pathData.map.pawnPathPool.GetEmptyPawnPath();
                int index = finalIndex;
                while (index >= 0)
                {
                    emptyPawnPath.AddNode(pathData.map.cellIndices.IndexToCell(index));
                    index = calcGrid[index].parentIndex;
                }
                emptyPawnPath.SetupFound(calcGrid[finalIndex].knownCost, false);
                return emptyPawnPath;
            }

            private CellRect CalculateDestinationRect()
            {
                CellRect result;
                if (pathData.dest.HasThing && pathData.pathEndMode != PathEndMode.OnCell)
                {
                    result = pathData.dest.Thing.OccupiedRect();
                }
                else
                {
                    result = CellRect.SingleCell(pathData.dest.Cell);
                }

                if (pathData.pathEndMode == PathEndMode.Touch)
                {
                    result = result.ExpandedBy(1);
                }
                return result;
            }

            private List<int> CalculateDisallowedCorners(CellRect destinationRect)
            {
                List<int> disallowedCornerIndices = new List<int>(4);
                if (pathData.pathEndMode == PathEndMode.Touch)
                {
                    int minX = destinationRect.minX;
                    int minZ = destinationRect.minZ;
                    int maxX = destinationRect.maxX;
                    int maxZ = destinationRect.maxZ;
                    if (!IsCornerTouchAllowed(minX + 1, minZ + 1, minX + 1, minZ, minX, minZ + 1))
                    {
                        disallowedCornerIndices.Add(pathData.map.cellIndices.CellToIndex(minX, minZ));
                    }
                    if (!IsCornerTouchAllowed(minX + 1, maxZ - 1, minX + 1, maxZ, minX, maxZ - 1))
                    {
                        disallowedCornerIndices.Add(pathData.map.cellIndices.CellToIndex(minX, maxZ));
                    }
                    if (!IsCornerTouchAllowed(maxX - 1, maxZ - 1, maxX - 1, maxZ, maxX, maxZ - 1))
                    {
                        disallowedCornerIndices.Add(pathData.map.cellIndices.CellToIndex(maxX, maxZ));
                    }
                    if (!IsCornerTouchAllowed(maxX - 1, minZ + 1, maxX - 1, minZ, maxX, minZ + 1))
                    {
                        disallowedCornerIndices.Add(pathData.map.cellIndices.CellToIndex(maxX, minZ));
                    }
                }
                return disallowedCornerIndices;
            }

            private bool IsCornerTouchAllowed(int cornerX, int cornerZ, int adjCardinal1X, int adjCardinal1Z, int adjCardinal2X, int adjCardinal2Z)
            {
                return TouchPathEndModeUtility.IsCornerTouchAllowed(cornerX, cornerZ, adjCardinal1X, adjCardinal1Z, adjCardinal2X, adjCardinal2Z, pathData.map);
            }
        }
    }
}
