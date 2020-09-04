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
    public class TrailblazerPather_HAStar : TrailblazerPather_AStar
    {
        /// <summary>
        /// Represents a node on the region grid.  Each region link becomes two nodes joined by an implicit edge.
        /// </summary>
        private class LinkNode
        {
            public readonly Map map;
            public readonly RegionLink link;
            public readonly bool end;

            private static readonly Dictionary<LinkNode, List<LinkNode>> neighborCache = new Dictionary<LinkNode, List<LinkNode>>();

            public LinkNode(RegionLink link, bool end)
            {
                map = link.RegionA.Map;
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

            public CellRef GetCell()
            {
                return map.GetCellRef(end ? link.span.Cells.Last() : link.span.root);
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

        // RRA* params
        private SimplePriorityQueue<LinkNode, int> rraOpenSet;
        private readonly Dictionary<LinkNode, int> rraClosedSet;
        private readonly RegionGrid regionGrid;
        private readonly HashSet<Region> destRegions = new HashSet<Region>();

        public TrailblazerPather_HAStar(PathfindData pathfindData) : base(pathfindData)
        {
            rraOpenSet = new SimplePriorityQueue<LinkNode, int>();
            rraClosedSet = new Dictionary<LinkNode, int>();
            regionGrid = pathfindData.map.regionGrid;
            destRegions.AddRange(from cell in pathfindData.DestRect.Cells
                                 let region = regionGrid.GetValidRegionAt(cell)
                                 where region != null
                                 select region);


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
        }

        protected override int Heuristic(CellRef cell)
        {
            Region region = regionGrid.GetValidRegionAt_NoRebuild(cell);
            if (destRegions.Contains(region))
            {
                return DistanceBetween(cell, destCell);
            }

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
                debugReplay.DrawCell(currentNode.GetCell());

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
                    //DebugDrawRegionEdge(currentNode, neighbor);
                    debugReplay.DrawLine(currentNode.GetCell(), neighbor.GetCell());

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
                        //DebugDrawRegionNode(neighbor, string.Format("{0} ({1})", newCost, moveCost));
                    }
                }
                debugReplay.NextFrame();
                closedNodes++;
            }

            Log.Error("[Trailblazer] RRA heuristic failed to reach target region " + targetRegion);
            return null;
        }

        private int RRAHeuristic(LinkNode link, CellRef target)
        {
            return DistanceBetween(link.GetCell(), target);
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
    }
}
