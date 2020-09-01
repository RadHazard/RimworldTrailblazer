using System;
using System.Collections.Generic;
using Priority_Queue;
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

        private struct PathFinderNode
        {
            public int knownCost;
            public int heuristicCost;
            public int totalCost;
            public CellRef parent;
            public bool visited;
        }

        private readonly SimplePriorityQueue<CellRef> openSet;
        private readonly PathFinderNode[] closedSet;

        private readonly int moveTicksCardinal;
        private readonly int moveTicksDiagonal;

        private static ushort debugMat = 0;

        public TrailblazerPather_AStar(PathfindData pathfindData) : base(pathfindData)
        {
            closedSet = new PathFinderNode[pathfindData.map.Area];
            openSet = new SimplePriorityQueue<CellRef>();

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
        }

        public override PawnPath FindPath()
        {
            Map map = pathfindData.map;

            CellRef start = pathfindData.start;
            CellRef dest = map.GetCellRef(pathfindData.dest.Cell);

            closedSet[start].knownCost = 0;
            closedSet[start].heuristicCost = CalcHeuristicEstimate(start, dest);
            closedSet[start].totalCost = closedSet[start].heuristicCost;
            closedSet[start].parent = null;
            closedSet[start].visited = true;

            openSet.Enqueue(start, 0);

            int closedNodes = 0;
            while (openSet.Count != 0)
            {
                CellRef current = openSet.Dequeue();

                // Check if we've reached our goal
                if (pathfindData.CellIsInDestination(current))
                {
                    DebugDrawFinalPath();
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
                                closedSet[neighbor].heuristicCost = CalcHeuristicEstimate(neighbor, dest);
                                closedSet[neighbor].visited = true;
                            }

                            closedSet[neighbor].knownCost = neighborNewCost;
                            closedSet[neighbor].totalCost = neighborNewCost + closedSet[neighbor].heuristicCost;
                            closedSet[neighbor].parent = current;

                            if (!openSet.EnqueueWithoutDuplicates(neighbor, closedSet[neighbor].totalCost))
                            {
                                openSet.UpdatePriority(neighbor, closedSet[neighbor].totalCost);
                            }
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

        private int CalcHeuristicEstimate(CellRef start, CellRef end)
        {
            int dx = Math.Abs(start.Cell.x - end.Cell.x);
            int dz = Math.Abs(start.Cell.z - end.Cell.z);
            return GenMath.OctileDistance(dx, dz, moveTicksCardinal, moveTicksDiagonal);
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

        private void FlashCell(IntVec3 cell, string text, int duration, float offset = 0f)
        {
            pathfindData.map.debugDrawer.FlashCell(cell, (debugMat % 100 / 100f) + offset, text, duration);
        }

        private void DebugDrawFinalPath()
        {
            if (DebugViewSettings.drawPaths)
            {
                int mapCells = pathfindData.map.Area;
                for (int i = 0; i < mapCells; i++)
                {
                    if (closedSet[i].visited)
                    {
                        IntVec3 c = pathfindData.map.cellIndices.IndexToCell(i);
                        string costString = string.Format("{0} + {1} = {2}", closedSet[i].knownCost, closedSet[i].heuristicCost, closedSet[i].totalCost);
                        FlashCell(c, costString, 50);
                    }
                }

                foreach (CellRef cell in openSet)
                {
                    FlashCell(cell, "open", 50);
                }
            }
        }
    }
}
