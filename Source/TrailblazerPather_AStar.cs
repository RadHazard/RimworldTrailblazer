using System;
using System.Collections.Generic;
using Priority_Queue;
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

        private struct PathFinderNode
        {
            public int knownCost;
            public int heuristicCost;
            public int totalCost;
            public CellRef parent;
            public bool visited;
        }

        // A* Params
        private readonly SimplePriorityQueue<CellRef, int> openSet;
        private readonly PathFinderNode[] closedSet;

        protected readonly CellRef startCell;
        protected readonly CellRef destCell;

        protected readonly int moveTicksCardinal;
        protected readonly int moveTicksDiagonal;

        // Debug
        private static ushort debugMat = 0;
        protected readonly TrailblazerDebugVisualizer debugVisualizer;
        protected readonly TrailblazerDebugVisualizer.InstantReplay debugReplay;

        public TrailblazerPather_AStar(PathfindData pathfindData) : base(pathfindData)
        {
            closedSet = new PathFinderNode[pathfindData.map.Area];
            openSet = new SimplePriorityQueue<CellRef, int>();

            startCell = pathfindData.start;
            destCell = pathfindData.map.GetCellRef(pathfindData.dest.Cell);

            CostRule_MoveTicks.GetMoveTicks(pathfindData, out moveTicksCardinal, out moveTicksDiagonal);

            debugMat++;
            debugVisualizer = pathfindData.map.GetComponent<TrailblazerDebugVisualizer>();
            debugReplay = debugVisualizer.CreateNewReplay();
        }

        public override PawnPath FindPath()
        {
            // Initialize the main A* algorithm
            Map map = pathfindData.map;

            closedSet[startCell].knownCost = 0;
            closedSet[startCell].heuristicCost = Heuristic(startCell);
            closedSet[startCell].totalCost = closedSet[startCell].heuristicCost;
            closedSet[startCell].parent = null;
            closedSet[startCell].visited = true;

            openSet.Enqueue(startCell, 0);

            int closedNodes = 0;
            while (openSet.Count > 0)
            {
                CellRef current = openSet.Dequeue();
                //debugReplay.DrawCell(current);
                //debugReplay.NextFrame();

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

                        if (!MoveIsValid(moveData))
                            continue;

                        int neighborNewCost = closedSet[current].knownCost + CalcMoveCost(moveData);
                        if (!closedSet[neighbor].visited || closedSet[neighbor].knownCost > neighborNewCost)
                        {
                            if (!closedSet[neighbor].visited)
                            {
                                closedSet[neighbor].heuristicCost = Heuristic(neighbor);
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
            Log.Warning("[Trailblazer] " + pawn + " pathing from " + startCell + " to " + destCell +
                " ran out of cells to process.\n" + "Job:" + currentJob + "\nFaction: " + faction, false);
            //TODO
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

        protected void FlashCell(IntVec3 cell, string text, int duration, float offset = 0f)
        {
            pathfindData.map.debugDrawer.FlashCell(cell, (debugMat % 100 / 100f) + offset, text, duration);
        }

        protected void DebugDrawFinalPath()
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
