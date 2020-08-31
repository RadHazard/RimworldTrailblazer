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
        /// RegionLink wrapper that implements GetHashCode and Equals
        /// </summary>
        private class LinkNode
        {
            public readonly RegionLink link;

            public LinkNode(RegionLink link)
            {
                this.link = link;
            }

            public static implicit operator RegionLink(LinkNode node)
            {
                return node.link;
            }

            public static implicit operator LinkNode(RegionLink link)
            {
                return new LinkNode(link);
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
                return link.UniqueHashCode() == linkNode.link.UniqueHashCode();
            }

            public override int GetHashCode()
            {
                return (int)link.UniqueHashCode();
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
            foreach (Region region in destRegions)
            {
                foreach (RegionLink link in region.links)
                {
                    rraClosedSet[link] = DistanceBetween(link, destCell);
                    rraOpenSet.Enqueue(link, 0);
                }
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
            IEnumerable<RegionLink> links = from link in region.links
                                            where rraClosedSet.ContainsKey(link)
                                            select link;

            if (links.EnumerableNullOrEmpty())
            {
                links = ReverseResumableAStar(cell).Yield();
            }

            return (from link in links
                    let totalCost = rraClosedSet[link] + RRAHeuristic(link, cell)
                    select totalCost).Min();
        }

        /// <summary>
        /// Initiates or resumes RRA* pathfinding on the region grid with the given target.
        /// 
        /// NOTE - This algorithm inverts regions and links.  The nodes are RegionLinks, and the edges are between every
        /// link of a region.  Cost is the octaline distance between the closest respective cell of each link.
        /// (The goal cell is also considered a node. It shares edges with every RegionLink belonging to its region)
        /// 
        /// </summary>
        /// <returns>The region link closest to the target cell</returns>
        /// <param name="targetCell">Target cell.</param>
        private RegionLink ReverseResumableAStar(CellRef targetCell)
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
                RegionLink currentLink = rraOpenSet.Dequeue();

                // Check if we've reached our goal
                if (currentLink.regions.Contains(targetRegion))
                {
                    return currentLink;
                }

                if (closedNodes > SearchLimit)
                {
                    Log.Error("[Trailblazer] RRA* Heuristic closed too many cells, aborting");
                    return null;
                }

                // Get the list of neighboring links.  Links are considered to share an edge if they share a region.
                // Ignore the links that would lead to impassible regions
                IEnumerable<RegionLink> neighbors = from region in currentLink.regions
                                                    from link in region.links
                                                    let otherRegion = link.GetOtherRegion(region)
                                                    where otherRegion.Allows(pathfindData.traverseParms, false) || otherRegion == targetRegion
                                                    where link != currentLink
                                                    select link;

                foreach (RegionLink neighbor in neighbors)
                {
                    int moveCost = DistanceBetween(currentLink, neighbor);
                    if (moveCost < 0)
                    {
                        Log.ErrorOnce("[Trailblazer] RRA* heuristic had negative cost!", pathfindData.GetHashCode() ^ 0x98C45AB);//TODO
                        moveCost = 0;
                    }
                    DebugDrawRegionEdge(currentLink, neighbor);

                    int newCost = rraClosedSet[currentLink] + moveCost;
                    if (!rraClosedSet.ContainsKey(neighbor) || newCost < rraClosedSet[neighbor])
                    {
                        rraClosedSet[neighbor] = newCost;
                        int estimatedCost = newCost + RRAHeuristic(neighbor, targetCell);
                        if (!rraOpenSet.EnqueueWithoutDuplicates(neighbor, estimatedCost))
                        {
                            rraOpenSet.UpdatePriority(neighbor, estimatedCost);
                        }
                        DebugDrawRegionNode(neighbor);
                    }
                }
                closedNodes++;
            }

            Log.Error("[Trailblazer] RRA heuristic failed to reach target region " + targetRegion);
            return null;
        }

        private int RRAHeuristic(RegionLink link, CellRef target)
        {
            return DistanceBetween(link, target);
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

        private CellRect LinkToRect(RegionLink link)
        {
            IntVec3 root = link.span.root;
            int maxX = root.x;
            int maxZ = root.z;
            if (link.span.dir == SpanDirection.North)
            {
                maxZ += link.span.length;
            }
            else
            {
                maxX += link.span.length;
            }
            return CellRect.FromLimits(root.x, root.z, maxX, maxZ);
        }

        /// <summary>
        /// Calculates the shortest octile distance between the region link and the cell
        /// </summary>
        /// <returns>The distance.</returns>
        /// <param name="link">Link.</param>
        /// <param name="cell">Cell.</param>
        private int DistanceBetween(RegionLink link, CellRef cell)
        {
            IntVec3 closestCell = LinkToRect(link).ClosestCellTo(cell);

            int dx = Math.Abs(closestCell.x - cell.Cell.x);
            int dz = Math.Abs(closestCell.z - cell.Cell.z);
            return GenMath.OctileDistance(dx, dz, moveTicksCardinal, moveTicksDiagonal);
        }

        /// <summary>
        /// Calculates the shortest octile distance between region links A and B
        /// </summary>
        /// <returns>The distance.</returns>
        /// <param name="linkA">Link a.</param>
        /// <param name="linkB">Link b.</param>
        private int DistanceBetween(RegionLink linkA, RegionLink linkB)
        {
            EdgeSpan spanA = linkA.span;
            EdgeSpan spanB = linkB.span;

            int dx;
            int dz;
            if (spanA.dir == spanB.dir)
            {
                // Both spans are parallel
                if (spanA.dir == SpanDirection.North)
                {
                    // dX is constant, check for dZ spacing
                    dx = Math.Abs(spanA.root.x - spanB.root.x);
                    int aMinZ = spanA.root.z;
                    int aMaxZ = aMinZ + spanA.length - 1;
                    int bMinZ = spanB.root.z;
                    int bMaxZ = bMinZ + spanB.length - 1;
                    if (aMaxZ < bMinZ)
                    {
                        // a is strictly less than b in the Z direction
                        dz = bMinZ - aMaxZ;
                    }
                    else if (aMinZ > bMaxZ)
                    {
                        // a is strictly greater than b in the Z direction
                        dz = aMinZ - bMaxZ;
                    }
                    else
                    {
                        // spans overlap in the Z direction
                        dz = 0;
                    }
                }
                else
                {
                    // dZ is constant, check for dX spacing
                    dz = Math.Abs(spanA.root.z - spanB.root.z);
                    int aMinX = spanA.root.x;
                    int aMaxX = aMinX + spanA.length - 1;
                    int bMinX = spanB.root.x;
                    int bMaxX = bMinX + spanB.length - 1;
                    if (aMaxX < bMinX)
                    {
                        // a is strictly less than b in the X direction
                        dx = bMinX - aMaxX;
                    }
                    else if (aMinX > bMaxX)
                    {
                        // a is strictly greater than b in the X direction
                        dx = aMinX - bMaxX;
                    }
                    else
                    {
                        // spans overlap in the X direction
                        dx = 0;
                    }
                }
            }
            else
            {
                // Spans are perpendicular
                EdgeSpan northSpan = spanA;
                EdgeSpan eastSpan = spanB;
                if (spanB.dir == SpanDirection.North)
                {
                    northSpan = spanB;
                    eastSpan = spanA;
                }

                int northX = northSpan.root.x;
                int eastMinX = eastSpan.root.x;
                int eastMaxX = eastMinX + eastSpan.length - 1;
                if (northX < eastMinX)
                {
                    // north span is below the east span in the X direction
                    dx = eastMinX - northX;
                }
                else if (northX > eastMaxX)
                {
                    // north span is above the east span in the X direction
                    dx = northX - eastMaxX;
                }
                else
                {
                    // spans overlap in the X direction
                    dx = 0;
                }

                int eastZ = eastSpan.root.z;
                int northMinZ = northSpan.root.z;
                int northMaxZ = northMinZ + eastSpan.length - 1;
                if (eastZ < northMinZ)
                {
                    // east span is below the north span in the Z direction
                    dz = northMinZ - eastZ;
                }
                else if (eastZ > northMaxZ)
                {
                    // east span is above the north span in the Z direction
                    dz = eastZ - northMaxZ;
                }
                else
                {
                    // spans overlap in the Z direction
                    dz = 0;
                }
            }

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

        private void DebugDrawRegionNode(RegionLink node)
        {
            if (DebugViewSettings.drawPaths)
            {
                foreach (IntVec3 cell in node.span.Cells)
                {
                    FlashCell(cell, rraClosedSet[node].ToString(), 50, 0.05f);
                }
            }
        }

        private void DebugDrawRegionEdge(RegionLink nodeA, RegionLink nodeB)
        {
            if (DebugViewSettings.drawPaths)
            {
                pathfindData.map.debugDrawer.FlashLine(DebugFindLinkCenter(nodeA), DebugFindLinkCenter(nodeB));
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
                        string costString = string.Format("{0} / {1}", closedSet[i].knownCost, closedSet[i].heuristicCost);
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
