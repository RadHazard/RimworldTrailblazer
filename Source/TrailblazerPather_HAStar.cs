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
        private class LinkNode : FastPriorityQueueNode
        {
            public readonly Map map;
            public readonly RegionLink link;
            public readonly bool top;

            private static readonly Dictionary<LinkNode, List<LinkNode>> neighborCache = new Dictionary<LinkNode, List<LinkNode>>();
            private static readonly Dictionary<RegionLink, LinkNode> topCache = new Dictionary<RegionLink, LinkNode>();
            private static readonly Dictionary<RegionLink, LinkNode> bottomCache = new Dictionary<RegionLink, LinkNode>();

            private LinkNode(RegionLink link, bool top)
            {
                map = link.RegionA.Map;
                this.link = link;
                this.top = top;
            }

            public static LinkNode Top(RegionLink link)
            {
                if (!topCache.ContainsKey(link))
                {
                    topCache[link] = new LinkNode(link, true);
                }
                return topCache[link];
            }

            public static LinkNode Bottom(RegionLink link)
            {
                if (!bottomCache.ContainsKey(link))
                {
                    bottomCache[link] = new LinkNode(link, false);
                }
                return bottomCache[link];
            }

            public static IEnumerable<LinkNode> Both(RegionLink link)
            {
                yield return Bottom(link);
                yield return Top(link);
            }

            public CellRef GetCell()
            {
                return map.GetCellRef(top ? link.span.Cells.Last() : link.span.root);
            }

            public LinkNode PairedNode()
            {
                if (top)
                {
                    return Bottom(link);
                }
                return Top(link);
            }

            /// <summary>
            /// Returns an enumerable of neighboring links.  Links are considered to share an edge if they share a region.
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

            /// <summary>
            /// Returns an enumerable of the region(s) that this LinkNode and the other LinkNode have in common.
            /// </summary>
            /// <returns>The regions.</returns>
            /// <param name="other">Other.</param>
            public IEnumerable<Region> CommonRegions(LinkNode other)
            {
                return from region in link.regions
                       where other.link.regions.Contains(region)
                       select region;
            }

            /// <summary>
            /// Clears the various caches.  Must be called at the beginning of every pathfind operation to ensure
            /// bad link data doesn't get reused.
            /// </summary>
            public static void ClearCaches()
            {
                neighborCache.Clear();
                topCache.Clear();
                bottomCache.Clear();
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
                return top == linkNode.top && link.UniqueHashCode() == linkNode.link.UniqueHashCode();
            }

            public override int GetHashCode()
            {
                // Arbitrary numbers chosen by fair dice roll ;)
                return Gen.HashCombineInt((int)link.UniqueHashCode(), top ? 2940 : 6003);
            }
        }

        // RRA* params
        private Priority_Queue.FastPriorityQueue<LinkNode> rraOpenSet;
        private readonly Dictionary<LinkNode, int> rraClosedSet;
        private readonly RegionGrid regionGrid;
        private readonly HashSet<Region> destRegions = new HashSet<Region>();
        private readonly int maxLinks;

        public TrailblazerPather_HAStar(PathfindData pathfindData) : base(pathfindData)
        {
            regionGrid = pathfindData.map.regionGrid;
            int regions = regionGrid.AllRegions.Count();
            // Worst case scenario - a large region where every single edge cell links to a different neighboring region
            maxLinks = Region.GridSize * 4 * regions;

            rraOpenSet = new Priority_Queue.FastPriorityQueue<LinkNode>(maxLinks);
            rraClosedSet = new Dictionary<LinkNode, int>();

            destRegions.AddRange(from cell in pathfindData.DestRect.Cells
                                 let region = regionGrid.GetValidRegionAt(cell)
                                 where region != null
                                 select region);


            // Initialize the RRA* algorithm
            LinkNode.ClearCaches();
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
        /// The grid is made up of nodes, where each region link is represented by two nodes (either end of the link).
        /// All nodes of all links that share a region are considered to share edges, with the cost of the edge
        /// being the octaline distance between the two cells.
        /// </summary>
        /// <returns>The region link closest to the target cell</returns>
        /// <param name="targetCell">Target cell.</param>
        private LinkNode ReverseResumableAStar(CellRef targetCell)
        {
            Region targetRegion = regionGrid.GetValidRegionAt_NoRebuild(targetCell);

            // Rebuild the open set based on the new target
            LinkNode[] cachedNodes = rraOpenSet.ToArray(); // Cache the nodes because we'll be messing with the queue
            foreach (LinkNode link in cachedNodes)
            {
                rraOpenSet.UpdatePriority(link, rraClosedSet[link] + RRAHeuristic(link, targetCell));
            }

            int closedNodes = 0;
            while (rraOpenSet.Count > 0)
            {
                LinkNode currentNode = rraOpenSet.Dequeue();
                //debugReplay.DrawCell(currentNode.GetCell());

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

                foreach (LinkNode neighbor in currentNode.Neighbors())
                {
                    //DebugDrawRegionEdge(currentNode, neighbor);
                    //debugReplay.DrawLine(currentNode.GetCell(), neighbor.GetCell());

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

                        if (rraOpenSet.Contains(neighbor))
                        {
                            rraOpenSet.UpdatePriority(neighbor, estimatedCost);
                        }
                        else
                        {
                            rraOpenSet.Enqueue(neighbor, estimatedCost);
                        }
                        //DebugDrawRegionNode(neighbor, string.Format("{0} ({1})", newCost, moveCost));
                    }
                }
                //debugReplay.NextFrame();
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
