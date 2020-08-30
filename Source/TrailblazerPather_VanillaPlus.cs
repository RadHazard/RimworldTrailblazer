using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace Trailblazer
{
    /// <summary>
    /// Trailblazer pather that closely replicates the vanilla pathfinding algorithm but using the Trailblazer
    /// cost code
    /// </summary>
    public class TrailblazerPather_VanillaPlus : TrailblazerPather
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

        private struct PathFinderNodeFast
        {
            public int knownCost;
            public int heuristicCost;
            public int parentIndex;
            public int costNodeCost;
            public ushort status;
        }

        private static ushort statusOpenValue = 1;
        private static ushort statusClosedValue = 2;

        private static readonly int[] Directions = {
             0,  1,  0, -1,  1,  1, -1, -1,
            -1,  0,  1,  0, -1,  1,  1, -1
        };

        private const int NodesToOpenBeforeRegionBasedPathing_NonColonist = 2000;
        private const int NodesToOpenBeforeRegionBasedPathing_Colonist = 100000;

        private const float NonRegionBasedHeuristicStrengthAnimal = 1.75f;

        private static readonly SimpleCurve NonRegionBasedHeuristicStrengthHuman_DistanceCurve = new SimpleCurve {
            {
                new CurvePoint (40f, 1f),
                true
            },
            {
                new CurvePoint (120f, 2.8f),
                true
            }
        };

        private static readonly SimpleCurve RegionHeuristicWeightByNodesOpened = new SimpleCurve {
            {
                new CurvePoint (0f, 1f),
                true
            },
            {
                new CurvePoint (3500f, 1f),
                true
            },
            {
                new CurvePoint (4500f, 5f),
                true
            },
            {
                new CurvePoint (30000f, 50f),
                true
            },
            {
                new CurvePoint (100000f, 500f),
                true
            }
        };


        public TrailblazerPather_VanillaPlus(Map map) : base(map) { }

        protected override TrailblazerPathWorker GetWorker(PathfindRequest pathfindRequest)
        {
            return new TrailblazerPathWorker_Vanilla(map, pathfindRequest);
        }

        protected class TrailblazerPathWorker_Vanilla : TrailblazerPathWorker
        {
            private readonly FastPriorityQueue<CostNode> openList;
            private readonly PathFinderNodeFast[] calcGrid; // TODO - this used to be static.  Performance implications?
            private readonly RegionCostCalculatorWrapper regionCostCalculator;

            public TrailblazerPathWorker_Vanilla(Map map, PathfindRequest pathfindRequest) : base(map, pathfindRequest)
            {
                //this.parent = parent;
                calcGrid = new PathFinderNodeFast[map.Size.x * map.Size.z];
                openList = new FastPriorityQueue<CostNode>(new CostNodeComparer());
                regionCostCalculator = new RegionCostCalculatorWrapper(map);
            }

            public override PawnPath FindPath()
            {
                //PfProfilerBeginSample("FindPath for " + pawn + " from " + start + " to " + dest + (dest.HasThing ? (" at " + dest.Cell) : "")); //TODO
                int mapSizeX = map.Size.x;
                int mapSizeZ = map.Size.z;
                CellIndices cellIndices = map.cellIndices;
                EdificeGrid edificeGrid = map.edificeGrid;
                PathGrid pathGrid = map.pathGrid;

                Pawn pawn = traverseParms.pawn;
                int destX = dest.Cell.x;
                int destZ = dest.Cell.z;
                int curIndex = cellIndices.CellToIndex(start);
                int num = cellIndices.CellToIndex(dest.Cell);

                bool passDestroyableThings = traverseParms.mode.CanDestroy();
                bool passWater = traverseParms.mode.CanPassWater();
                CellRect cellRect = CalculateDestinationRect();
                bool destinationIsSingleCell = cellRect.Width == 1 && cellRect.Height == 1;

                int closedNodes = 0;
                int openedNodes = 0;

                bool flag6 = (!passDestroyableThings && start.GetRegion(map, RegionType.Set_Passable) != null) && passWater;
                bool alwaysTrue = !passDestroyableThings || passDestroyableThings; // TODO I don't get what this is supposed to be, but in practice it's always true
                bool regionBasedPathing = false;
                int nodesToOpenBeforeRegionBasedPathing = (pawn?.IsColonist ?? false) ? NodesToOpenBeforeRegionBasedPathing_Colonist : NodesToOpenBeforeRegionBasedPathing_NonColonist;
                float heuristicStrength = DetermineHeuristicStrength(pawn);

                List<int> disallowedCornerIndices = CalculateDisallowedCorners(cellRect);
                InitStatusesAndPushStartNode(ref curIndex);
                while (true)
                {
                    //PfProfilerBeginSample("Open cell"); //TODO
                    if (openList.Count <= 0)
                    {
                        string currentJob = (pawn != null && pawn.CurJob != null) ? pawn.CurJob.ToString() : "null";
                        string faction = (pawn != null && pawn.Faction != null) ? pawn.Faction.ToString() : "null";
                        Log.Warning(pawn + " pathing from " + start + " to " + dest + " ran out of cells to process.\nJob:" + currentJob + "\nFaction: " + faction, false);
                        //TODO
                        //DebugDrawRichData();
                        //PfProfilerEndSample();
                        //PfProfilerEndSample();
                        return PawnPath.NotFound;
                    }
                    CostNode costNode = openList.Pop();
                    curIndex = costNode.index;
                    if (costNode.cost != calcGrid[curIndex].costNodeCost)
                    {
                        //PfProfilerEndSample(); //TODO
                    }
                    else if (calcGrid[curIndex].status == statusClosedValue)
                    {
                        //PfProfilerEndSample(); //TODO
                    }
                    else
                    {
                        IntVec3 intVec = cellIndices.IndexToCell(curIndex);
                        int currX = intVec.x;
                        int currZ = intVec.z;
                        if (destinationIsSingleCell)
                        {
                            if (curIndex == num)
                            {
                                //PfProfilerEndSample(); //TODO
                                PawnPath result = FinalizedPath(curIndex, regionBasedPathing);
                                //PfProfilerEndSample(); //TODO
                                return result;
                            }
                        }
                        else if (cellRect.Contains(intVec) && !disallowedCornerIndices.Contains(curIndex))
                        {
                            //PfProfilerEndSample(); //TODO
                            PawnPath result2 = FinalizedPath(curIndex, regionBasedPathing);
                            //PfProfilerEndSample(); //TODO
                            return result2;
                        }
                        if (closedNodes > SearchLimit)
                        {
                            break;
                        }
                        //TODO
                        //PfProfilerEndSample();
                        //PfProfilerBeginSample("Neighbor consideration");
                        for (int i = 0; i < 8; i++)
                        {
                            uint tentativeNeighborX = (uint)(currX + Directions[i]);
                            uint tentativeNeighborZ = (uint)(currZ + Directions[i + 8]);
                            if (tentativeNeighborX < mapSizeX && tentativeNeighborZ < mapSizeZ)
                            {
                                int neighborX = (int)tentativeNeighborX;
                                int neighborZ = (int)tentativeNeighborZ;
                                int neighborIndex = cellIndices.CellToIndex(neighborX, neighborZ);
                                if (calcGrid[neighborIndex].status != statusClosedValue || regionBasedPathing)
                                {
                                    int cellCost = 0;



                                    int neighborKnownCost = cellCost + calcGrid[curIndex].knownCost;
                                    ushort status = calcGrid[neighborIndex].status;
                                    if (status == statusClosedValue || status == statusOpenValue)
                                    {
                                        int num19 = 0;
                                        // TODO -- why does a closed neighbor get an extra cardinal tick cost?
                                        if (status == statusClosedValue)
                                        {
                                            num19 = moveTicksCardinal;
                                        }
                                        if (calcGrid[neighborIndex].knownCost <= neighborKnownCost + num19)
                                        {
                                            break;
                                        }
                                    }
                                    if (regionBasedPathing)
                                    {
                                        calcGrid[neighborIndex].heuristicCost = (int)Math.Round(regionCostCalculator.GetPathCostFromDestToRegion(neighborIndex) * RegionHeuristicWeightByNodesOpened.Evaluate(openedNodes));
                                        if (calcGrid[neighborIndex].heuristicCost < 0)
                                        {
                                            Log.ErrorOnce("Heuristic cost overflow for " + pawn.ToStringSafe() + " pathing from " + start + " to " + dest + ".", pawn.GetHashCode() ^ 0xB8DC389, false);
                                            calcGrid[neighborIndex].heuristicCost = 0;
                                        }
                                    }
                                    else if (status != statusClosedValue && status != statusOpenValue)
                                    {
                                        int dx = Math.Abs(neighborX - destX);
                                        int dz = Math.Abs(neighborZ - destZ);
                                        int octileDistance = GenMath.OctileDistance(dx, dz, moveTicksCardinal, moveTicksDiagonal);
                                        calcGrid[neighborIndex].heuristicCost = (int)Math.Round(octileDistance * heuristicStrength);
                                    }
                                    int estimatedTotalCost = neighborKnownCost + calcGrid[neighborIndex].heuristicCost;
                                    if (estimatedTotalCost < 0)
                                    {
                                        Log.ErrorOnce("Node cost overflow for " + pawn.ToStringSafe() + " pathing from " + start + " to " + dest + ".", pawn.GetHashCode() ^ 0x53CB9DE, false);
                                        estimatedTotalCost = 0;
                                    }
                                    calcGrid[neighborIndex].parentIndex = curIndex;
                                    calcGrid[neighborIndex].knownCost = neighborKnownCost;
                                    calcGrid[neighborIndex].status = statusOpenValue;
                                    calcGrid[neighborIndex].costNodeCost = estimatedTotalCost;
                                    openedNodes++;
                                    openList.Push(new CostNode(neighborIndex, estimatedTotalCost));
                                }
                            }
                        }
                        //PfProfilerEndSample(); //TODO
                        closedNodes++;
                        calcGrid[curIndex].status = statusClosedValue;
                        if (((openedNodes >= nodesToOpenBeforeRegionBasedPathing) & flag6) && !regionBasedPathing)
                        {
                            regionBasedPathing = true;
                            regionCostCalculator.Init(cellRect, traverseParms, moveTicksCardinal, moveTicksDiagonal, avoidGrid, allowedArea, pawnDrafted, disallowedCornerIndices);
                            InitStatusesAndPushStartNode(ref curIndex);
                            openedNodes = 0;
                            closedNodes = 0;
                        }
                    }
                }
                Log.Warning(pawn + " pathing from " + start + " to " + dest + " hit search limit of " + SearchLimit + " cells.", false);
                //TODO
                //DebugDrawRichData();
                //PfProfilerEndSample();
                //PfProfilerEndSample();
                return PawnPath.NotFound;
            }
            // TODO ======= duplicate methods ========

            private bool BlocksDiagonalMovement(int x, int z)
            {
                return PathFinder.BlocksDiagonalMovement(x, z, map);
            }

            private bool BlocksDiagonalMovement(int index)
            {
                return PathFinder.BlocksDiagonalMovement(index, map);
            }

            private PawnPath FinalizedPath(int finalIndex, bool usedRegionHeuristics)
            {
                PawnPath emptyPawnPath = map.pawnPathPool.GetEmptyPawnPath();
                int num = finalIndex;
                while (true)
                {
                    int parentIndex = calcGrid[num].parentIndex;
                    emptyPawnPath.AddNode(map.cellIndices.IndexToCell(num));
                    if (num == parentIndex)
                    {
                        break;
                    }
                    num = parentIndex;
                }
                emptyPawnPath.SetupFound((float)calcGrid[finalIndex].knownCost, usedRegionHeuristics);
                return emptyPawnPath;
            }

            private void InitStatusesAndPushStartNode(ref int curIndex)
            {
                statusOpenValue += 2;
                statusClosedValue += 2;
                if (statusClosedValue >= 65435)
                {
                    ResetStatuses();
                }
                curIndex = map.cellIndices.CellToIndex(start);
                calcGrid[curIndex].knownCost = 0;
                calcGrid[curIndex].heuristicCost = 0;
                calcGrid[curIndex].costNodeCost = 0;
                calcGrid[curIndex].parentIndex = curIndex;
                calcGrid[curIndex].status = statusOpenValue;
                openList.Clear();
                openList.Push(new CostNode(curIndex, 0));
            }

            private void ResetStatuses()
            {
                int num = calcGrid.Length;
                for (int i = 0; i < num; i++)
                {
                    calcGrid[i].status = 0;
                }
                statusOpenValue = 1;
                statusClosedValue = 2;
            }

            //TODO can I do something with these?
            //[Conditional("PFPROFILE")]
            //private void PfProfilerBeginSample(string s)
            //{
            //}

            //[Conditional("PFPROFILE")]
            //private void PfProfilerEndSample()
            //{
            //}

            //private void DebugDrawRichData()
            //{
            //}

            private float DetermineHeuristicStrength(Pawn pawn)
            {
                if (pawn != null && pawn.RaceProps.Animal)
                {
                    return NonRegionBasedHeuristicStrengthAnimal;
                }
                float lengthHorizontal = (start - dest.Cell).LengthHorizontal;
                return (float)Math.Round(NonRegionBasedHeuristicStrengthHuman_DistanceCurve.Evaluate(lengthHorizontal));
            }

            private CellRect CalculateDestinationRect()
            {
                CellRect result = (dest.HasThing && pathEndMode != PathEndMode.OnCell) ? dest.Thing.OccupiedRect() : CellRect.SingleCell(dest.Cell);
                if (pathEndMode == PathEndMode.Touch)
                {
                    result = result.ExpandedBy(1);
                }
                return result;
            }

            private List<int> CalculateDisallowedCorners(CellRect destinationRect)
            {
                List<int> disallowedCornerIndices = new List<int>(4);
                if (pathEndMode == PathEndMode.Touch)
                {
                    int minX = destinationRect.minX;
                    int minZ = destinationRect.minZ;
                    int maxX = destinationRect.maxX;
                    int maxZ = destinationRect.maxZ;
                    if (!IsCornerTouchAllowed(minX + 1, minZ + 1, minX + 1, minZ, minX, minZ + 1))
                    {
                        disallowedCornerIndices.Add(map.cellIndices.CellToIndex(minX, minZ));
                    }
                    if (!IsCornerTouchAllowed(minX + 1, maxZ - 1, minX + 1, maxZ, minX, maxZ - 1))
                    {
                        disallowedCornerIndices.Add(map.cellIndices.CellToIndex(minX, maxZ));
                    }
                    if (!IsCornerTouchAllowed(maxX - 1, maxZ - 1, maxX - 1, maxZ, maxX, maxZ - 1))
                    {
                        disallowedCornerIndices.Add(map.cellIndices.CellToIndex(maxX, maxZ));
                    }
                    if (!IsCornerTouchAllowed(maxX - 1, minZ + 1, maxX - 1, minZ, maxX, minZ + 1))
                    {
                        disallowedCornerIndices.Add(map.cellIndices.CellToIndex(maxX, minZ));
                    }
                }
                return disallowedCornerIndices;
            }

            private bool IsCornerTouchAllowed(int cornerX, int cornerZ, int adjCardinal1X, int adjCardinal1Z, int adjCardinal2X, int adjCardinal2Z)
            {
                return TouchPathEndModeUtility.IsCornerTouchAllowed(cornerX, cornerZ, adjCardinal1X, adjCardinal1Z, adjCardinal2X, adjCardinal2Z, map);
            }
        }
    }
}
