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
    /// Trailblazer pather that uses a heirarchical A* implementation.  The outer A* implementation uses an inner
    /// Reverse Resumable A* pathing on the region grid to get more optimal heuristics.
    /// See https://factorio.com/blog/post/fff-317 for where the inspiration came from
    /// </summary>
    public class TrailblazerPather_HAStar : TrailblazerPather
    {
        // Pathing cost constants
        private const int SearchLimit = 160000;

        private struct CellNode
        {
            public int knownCost;
            public int heuristicCost;
            public int totalCost;
            public CellRef parent;
            public bool visited;
        }

        /// <summary>
        /// Represents a node on the region grid.  Each region link becomes two nodes joined by an implicit edge.
        /// </summary>
        private class LinkNode
        {
            public readonly RegionLink link;
            public readonly bool end;

            private static readonly Dictionary<LinkNode, List<LinkNode>> neighborCache = new Dictionary<LinkNode, List<LinkNode>>();

            public LinkNode(RegionLink link, bool end)
            {
                this.link = link;
                this.end = end;
            }

            public static LinkNode Top(RegionLink link)
            {
                return new LinkNode(link, true);
            }

            public static LinkNode Bottom(RegionLink link)
            {
                return new LinkNode(link, false);
            }

            public static IEnumerable<LinkNode> Both(RegionLink link)
            {
                yield return Bottom(link);
                yield return Top(link);
            }

            public IntVec3 GetCell()
            {
                return end ? link.span.Cells.Last() : link.span.root;
            }

            public LinkNode PairedNode()
            {
                return new LinkNode(link, !end);
            }

            public bool IsPairedNode(LinkNode linkNode)
            {
                return end != linkNode.end && link.UniqueHashCode() == linkNode.link.UniqueHashCode();
            }

            /// <summary>
            /// Returns the list of neighboring links.  Links are considered to share an edge if they share a region.
            /// </summary>
            /// <returns>The neighbors.</returns>
            public IEnumerable<LinkNode> Neighbors()
            {
                if (!neighborCache.ContainsKey(this))
                {
                    neighborCache[this] = (from region in link.regions
                                           from link in region.links
                                           where link != this.link
                                           from node in Both(link) //TODO closest?
                                           select node)
                                           .Distinct()
                                           .Concat(PairedNode()) // Don't forget our pair
                                           .ToList();
                }
                return neighborCache[this];
            }

            public IEnumerable<Region> CommonRegions(LinkNode other)
            {
                return from region in link.regions
                       where other.link.regions.Contains(region)
                       select region;
            }


            public static implicit operator RegionLink(LinkNode node)
            {
                return node.link;
            }

            public override bool Equals(object obj)
            {
                if (obj is LinkNode)
                {
                    return Equals((LinkNode)obj);
                }
                return false;
            }

            public bool Equals(LinkNode linkNode)
            {
                return end == linkNode.end && link.UniqueHashCode() == linkNode.link.UniqueHashCode();
            }

            public override int GetHashCode()
            {
                // Arbitrary numbers chosen by fair dice roll ;)
                return Gen.HashCombineInt((int)link.UniqueHashCode(), end ? 2940 : 6003);
            }
        }

        // Main A* params
        private readonly SimplePriorityQueue<CellRef, int> openSet;
        private readonly CellNode[] closedSet;
        private readonly CellRef startCell;
        private readonly CellRef destCell;

        // RRA* params
        private SimplePriorityQueue<LinkNode, int> rraOpenSet;
        private readonly Dictionary<LinkNode, int> rraClosedSet;
        private readonly RegionGrid regionGrid;
        private readonly List<Region> destRegions;

        // Shared params
        private readonly int moveTicksCardinal;
        private readonly int moveTicksDiagonal;

        // Debug
        private static ushort debugMat = 0;

        public TrailblazerPather_HAStar(PathfindData pathfindData) : base(pathfindData)
        {
            openSet = new SimplePriorityQueue<CellRef, int>();
            closedSet = new CellNode[pathfindData.map.Area];
            startCell = pathfindData.start;
            destCell = pathfindData.map.GetCellRef(pathfindData.dest.Cell);

            rraOpenSet = new SimplePriorityQueue<LinkNode, int>();
            rraClosedSet = new Dictionary<LinkNode, int>();
            regionGrid = pathfindData.map.regionGrid;
            destRegions = (from cell in pathfindData.DestRect.Cells
                           let region = regionGrid.GetValidRegionAt_NoRebuild(cell)
                           select region).ToList();

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
            // Initialize the RRA* algorithm
            IEnumerable<LinkNode> initialNodes = (from region in destRegions
                                                  from link in region.links
                                                  from node in LinkNode.Both(link)
                                                  select node).Distinct();
            foreach (LinkNode node in initialNodes)
            {
                rraClosedSet[node] = RRAHeuristic(node, destCell);
                rraOpenSet.Enqueue(node, rraClosedSet[node]);
            }

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

                // Check if we've reached our goal
                if (pathfindData.CellIsInDestination(current))
                {
                    DebugDrawFinalPath();
                    return FinalizedPath(current);
                }

                // Check if we hit the searchLimit
                if (closedNodes > SearchLimit)
                {
                    Log.Warning("[Trailblazer] " + pathfindData.traverseParms.pawn + " pathing from " + startCell +
                        " to " + destCell + " hit search limit of " + SearchLimit + " cells.", false);
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
            DebugDrawFinalPath();
            return PawnPath.NotFound;
        }

        private int Heuristic(CellRef cell)
        {
            Region region = regionGrid.GetValidRegionAt_NoRebuild(cell);
            IEnumerable<LinkNode> nodes = from link in region.links
                                          from node in LinkNode.Both(link)
                                          where rraClosedSet.ContainsKey(node)
                                          select node;

            if (nodes.EnumerableNullOrEmpty())
            {
                nodes = ReverseResumableAStar(cell).Yield();
            }

            return (from node in nodes
                    let totalCost = rraClosedSet[node] + RRAHeuristic(node, cell)
                    select totalCost).Min();
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
        private LinkNode ReverseResumableAStar(CellRef targetCell)
        {
            Region targetRegion = regionGrid.GetValidRegionAt_NoRebuild(targetCell);

            // Rebuild the open set based on the new target
            var oldSet = rraOpenSet;
            rraOpenSet = new SimplePriorityQueue<LinkNode, int>();
            foreach (LinkNode link in oldSet)
            {
                rraOpenSet.Enqueue(link, rraClosedSet[link] + RRAHeuristic(link, targetCell));
            }

            int closedNodes = 0;
            while (rraOpenSet.Count > 0)
            {
                LinkNode currentNode = rraOpenSet.Dequeue();

                // Check if we've reached our goal
                if (currentNode.link.regions.Contains(targetRegion))
                {
                    return currentNode;
                }

                if (closedNodes > SearchLimit)
                {
                    Log.Error("[Trailblazer] RRA* Heuristic closed too many cells, aborting");
                    return null;
                }

                // TODO - maybe this shouldn't be filtered here?
                // We could just add a heavy fixed cost to untraversable regions
                //where region.Allows(pathfindData.traverseParms, false)

                foreach (LinkNode neighbor in currentNode.Neighbors())
                {
                    DebugDrawRegionEdge(currentNode, neighbor);

                    int moveCost = DistanceBetween(currentNode, neighbor);
                    // Penalize the edge if the two links don't share a pathable region
                    // TODO should we just totally ignore the edge instead?
                    if (!currentNode.CommonRegions(neighbor).Any(r => r.Allows(pathfindData.traverseParms, false)))
                    {
                        moveCost *= 50;
                    }

                    int newCost = rraClosedSet[currentNode] + moveCost;
                    if (!rraClosedSet.ContainsKey(neighbor) || newCost < rraClosedSet[neighbor])
                    {
                        rraClosedSet[neighbor] = newCost;
                        int estimatedCost = newCost + RRAHeuristic(neighbor, targetCell);
                        if (!rraOpenSet.EnqueueWithoutDuplicates(neighbor, estimatedCost))
                        {
                            rraOpenSet.UpdatePriority(neighbor, estimatedCost);
                        }
                        DebugDrawRegionNode(neighbor, string.Format("{0} ({1})", newCost, moveCost));
                    }
                }
                closedNodes++;
            }

            Log.Error("[Trailblazer] RRA heuristic failed to reach target region " + targetRegion);
            return null;
        }

        private int RRAHeuristic(LinkNode link, CellRef target)
        {
            return DistanceBetween(link.GetCell(), target);
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

        /// <summary>
        /// Calculates the shortest octile distance between two LinkNodes
        /// </summary>
        /// <returns>The distance between the nodes.</returns>
        /// <param name="nodeA">First node.</param>
        /// <param name="nodeB">Second node.</param>
        private int DistanceBetween(LinkNode nodeA, LinkNode nodeB)
        {
            return DistanceBetween(nodeA.GetCell(), nodeB.GetCell());
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

        private void DebugDrawRegionNode(LinkNode node, string text)
        {
            if (DebugViewSettings.drawPaths)
            {
                FlashCell(node.GetCell(), text, 50, 0.05f);
            }
        }

        private void DebugDrawRegionEdge(LinkNode nodeA, LinkNode nodeB)
        {
            if (DebugViewSettings.drawPaths)
            {
                pathfindData.map.debugDrawer.FlashLine(nodeA.GetCell(), nodeB.GetCell());
            }
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
                        string costString = null;//string.Format("{0} / {1}", closedSet[i].knownCost, closedSet[i].heuristicCost);
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
