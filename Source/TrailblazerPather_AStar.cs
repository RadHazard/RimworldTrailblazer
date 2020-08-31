using System;
using System.Collections.Generic;
using Trailblazer.Rules;
using Verse;
using Verse.AI;

namespace Trailblazer
{
    /// <summary>
    /// Trailblazer pather that simply uses A* with a simple octaline distance heuristic
    /// This will perform worse than vanilla, particularly with long paths!
    /// </summary>
    public class TrailblazerPather_AStar : TrailblazerPather
    {
        // Pathing cost constants
        private const int SearchLimit = 160000;

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

        private readonly FastPriorityQueue<CostNode> openSet;
        private readonly PathFinderNode[] closedSet;

        public TrailblazerPather_AStar(PathfindData pathfindData) : base(pathfindData)
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
                moveTicksCardinal = TrailblazerRule_CostMoveTicks.DefaultMoveTicksCardinal;
                moveTicksDiagonal = TrailblazerRule_CostMoveTicks.DefaultMoveTicksDiagonal;
            }

            CellRef start = pathfindData.start;
            CellRef dest = map.GetCellRef(pathfindData.dest.Cell);

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
                else if (pathfindData.CellIsInDestination(current))
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

                            openSet.Push(new CostNode(neighbor, closedSet[neighbor].totalCost));
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
                        string costString = string.Format("{0} / {1}", closedSet[i].knownCost, closedSet[i].totalCost);
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
    }
}
