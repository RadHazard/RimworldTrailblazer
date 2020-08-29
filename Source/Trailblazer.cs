using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace Trailblazer
{
    public class Trailblazer : PathFinder
    {
        internal struct CostNode
        {
            public int index;
            public int cost;

            public CostNode(int index, int cost)
            {
                this.index = index;
                this.cost = cost;
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

        internal class CostNodeComparer : IComparer<CostNode>
        {
            public int Compare(CostNode a, CostNode b)
            {
                return a.cost.CompareTo(b.cost);
            }
        }

        private readonly Map map;

        private readonly int mapSizeX;
        private readonly int mapSizeZ;

        private readonly CellIndices cellIndices;
        private readonly EdificeGrid edificeGrid;
        private readonly BlueprintGrid blueprintGrid;
        private readonly PathGrid pathGrid;

        private FastPriorityQueue<CostNode> openList;

        private static PathFinderNodeFast[] calcGrid;

        private static ushort statusOpenValue = 1;

        private static ushort statusClosedValue = 2;

        private RegionCostCalculatorWrapper regionCostCalculator;

        private static readonly int[] Directions = {
            0,
            1,
            0,
            -1,
            1,
            1,
            -1,
            -1,
            -1,
            0,
            1,
            0,
            -1,
            1,
            1,
            -1
        };

        private const int SearchLimit = 160000;
        private const int DefaultMoveTicksCardinal = 13;
        private const int DefaultMoveTicksDiagonal = 18;
        private const int Cost_BlockedWallBase = 70;
        private const float Cost_BlockedWallExtraPerHitPoint = 0.2f;
        private const int Cost_OutsideAllowedArea = 600;
        private const int Cost_PawnCollision = 175;
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

        public Trailblazer(Map map) : base(map)
        {
            this.map = map;
            mapSizeX = map.Size.x;
            mapSizeZ = map.Size.z;
            int mapSquares = mapSizeX * mapSizeZ;
            if (calcGrid == null || calcGrid.Length < mapSquares)
            {
                calcGrid = new PathFinderNodeFast[mapSquares];
            }
            openList = new FastPriorityQueue<CostNode>(new CostNodeComparer());
            regionCostCalculator = new RegionCostCalculatorWrapper(map);

            cellIndices = map.cellIndices;
            edificeGrid = map.edificeGrid;
            blueprintGrid = map.blueprintGrid;
            pathGrid = map.pathGrid;
        }

        /// <summary>
        /// Stub that replaces the vanilla pathfind method Performs error checking, then calls the implementation in
        /// PathFind_Internal
        /// </summary>
        /// <returns>The path.</returns>
        /// <param name="start">Start.</param>
        /// <param name="dest">Destination.</param>
        /// <param name="traverseParms">Traverse parms.</param>
        /// <param name="peMode">Path end mode.</param>
        public new PawnPath FindPath(IntVec3 start, LocalTargetInfo dest, TraverseParms traverseParms, PathEndMode peMode = PathEndMode.OnCell)
        {
            if (DebugSettings.pathThroughWalls)
            {
                traverseParms.mode = TraverseMode.PassAllDestroyableThings;
            }
            Pawn pawn = traverseParms.pawn;
            if (pawn != null && pawn.Map != map)
            {
                Log.Error("Tried to FindPath for pawn which is spawned in another map. His map PathFinder should have been used, not this one. pawn=" + pawn + " pawn.Map=" + pawn.Map + " map=" + map, false);
                return PawnPath.NotFound;
            }
            if (!start.IsValid)
            {
                Log.Error("Tried to FindPath with invalid start " + start + ", pawn= " + pawn, false);
                return PawnPath.NotFound;
            }
            if (!dest.IsValid)
            {
                Log.Error("Tried to FindPath with invalid dest " + dest + ", pawn= " + pawn, false);
                return PawnPath.NotFound;
            }
            if (traverseParms.mode == TraverseMode.ByPawn)
            {
                if (!pawn.CanReach(dest, peMode, Danger.Deadly, traverseParms.canBash, traverseParms.mode))
                {
                    return PawnPath.NotFound;
                }
            }
            else if (!map.reachability.CanReach(start, dest, peMode, traverseParms))
            {
                return PawnPath.NotFound;
            }

            return FindPath_Internal(start, dest, traverseParms, peMode);
        }

        /// <summary>
        /// Actual implementation of the pathfinder
        /// TODO
        /// </summary>
        /// <returns>The path.</returns>
        /// <param name="start">Start.</param>
        /// <param name="dest">Destination.</param>
        /// <param name="traverseParms">Traverse parms.</param>
        /// <param name="peMode">Path end mode.</param>
        protected PawnPath FindPath_Internal(IntVec3 start, LocalTargetInfo dest, TraverseParms traverseParms, PathEndMode peMode)
        {
            //PfProfilerBeginSample("FindPath for " + pawn + " from " + start + " to " + dest + (dest.HasThing ? (" at " + dest.Cell) : "")); //TODO
            Pawn pawn = traverseParms.pawn;
            int destX = dest.Cell.x;
            int destZ = dest.Cell.z;
            int curIndex = cellIndices.CellToIndex(start);
            int num = cellIndices.CellToIndex(dest.Cell);
            ByteGrid avoidGrid = pawn?.GetAvoidGrid(true);

            bool passDestroyableThings = traverseParms.mode == TraverseMode.PassAllDestroyableThings || traverseParms.mode == TraverseMode.PassAllDestroyableThingsNotWater;
            bool passWater = traverseParms.mode != TraverseMode.NoPassClosedDoorsOrWater && traverseParms.mode != TraverseMode.PassAllDestroyableThingsNotWater;
            bool dontPassDestroyableThings = !passDestroyableThings;
            CellRect cellRect = CalculateDestinationRect(dest, peMode);
            bool destinationIsSingleCell = cellRect.Width == 1 && cellRect.Height == 1;

            int[] pathGridArray = map.pathGrid.pathGrid;
            TerrainDef[] topGrid = map.terrainGrid.topGrid;
            int closedNodes = 0;
            int openedNodes = 0;
            Area allowedArea = GetAllowedArea(pawn);

            bool shouldCollideWithPawns = pawn != null && PawnUtility.ShouldCollideWithPawns(pawn);
            bool flag6 = (!passDestroyableThings && start.GetRegion(map, RegionType.Set_Passable) != null) && passWater;
            bool alwaysTrue = !passDestroyableThings || !dontPassDestroyableThings; // TODO I don't get what this is supposed to be, but in practice it's always true
            bool regionBasedPathing = false;
            bool pawnDrafted = pawn?.Drafted ?? false;
            int nodesToOpenBeforeRegionBasedPathing = (pawn?.IsColonist ?? false) ? NodesToOpenBeforeRegionBasedPathing_Colonist : NodesToOpenBeforeRegionBasedPathing_NonColonist;
            float heuristicStrength = DetermineHeuristicStrength(pawn, start, dest);
            int moveTicksCardinal;
            int moveTicksDiagonal;
            if (pawn != null)
            {
                moveTicksCardinal = pawn.TicksPerMoveCardinal;
                moveTicksDiagonal = pawn.TicksPerMoveDiagonal;
            }
            else
            {
                moveTicksCardinal = DefaultMoveTicksCardinal;
                moveTicksDiagonal = DefaultMoveTicksDiagonal;
            }
            List<int> disallowedCornerIndices = CalculateDisallowedCorners(traverseParms, peMode, cellRect);
            InitStatusesAndPushStartNode(ref curIndex, start);
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
                                bool blockedByWall = false;
                                if (passWater || !new IntVec3(neighborX, 0, neighborZ).GetTerrain(map).HasTag("Water"))
                                {
                                    // TODO -- This seems to check for walls.  Later on a second check is done
                                    // that only accounts for doors.
                                    if (!pathGrid.WalkableFast(neighborIndex))
                                    {
                                        if (!passDestroyableThings)
                                        {
                                            continue;
                                        }
                                        blockedByWall = true;
                                        cellCost += Cost_BlockedWallBase;
                                        Building building = edificeGrid[neighborIndex];
                                        if (building == null || !IsDestroyable(building))
                                        {
                                            continue;
                                        }
                                        cellCost += (int)(building.HitPoints * Cost_BlockedWallExtraPerHitPoint);
                                    }
                                    switch (i)
                                    {
                                        case 4:
                                            if (BlocksDiagonalMovement(curIndex - mapSizeX))
                                            {
                                                if (alwaysTrue)
                                                {
                                                    break;
                                                }
                                                cellCost += 70;
                                            }
                                            if (BlocksDiagonalMovement(curIndex + 1))
                                            {
                                                if (alwaysTrue)
                                                {
                                                    break;
                                                }
                                                cellCost += 70;
                                            }
                                            goto default;
                                        case 5:
                                            if (BlocksDiagonalMovement(curIndex + mapSizeX))
                                            {
                                                if (alwaysTrue)
                                                {
                                                    break;
                                                }
                                                cellCost += 70;
                                            }
                                            if (BlocksDiagonalMovement(curIndex + 1))
                                            {
                                                if (alwaysTrue)
                                                {
                                                    break;
                                                }
                                                cellCost += 70;
                                            }
                                            goto default;
                                        case 6:
                                            if (BlocksDiagonalMovement(curIndex + mapSizeX))
                                            {
                                                if (alwaysTrue)
                                                {
                                                    break;
                                                }
                                                cellCost += 70;
                                            }
                                            if (BlocksDiagonalMovement(curIndex - 1))
                                            {
                                                if (alwaysTrue)
                                                {
                                                    break;
                                                }
                                                cellCost += 70;
                                            }
                                            goto default;
                                        case 7:
                                            if (BlocksDiagonalMovement(curIndex - mapSizeX))
                                            {
                                                if (alwaysTrue)
                                                {
                                                    break;
                                                }
                                                cellCost += 70;
                                            }
                                            if (BlocksDiagonalMovement(curIndex - 1))
                                            {
                                                if (alwaysTrue)
                                                {
                                                    break;
                                                }
                                                cellCost += 70;
                                            }
                                            goto default;
                                        default:
                                            {
                                                cellCost += (i > 3) ? moveTicksDiagonal : moveTicksCardinal;
                                                if (!blockedByWall)
                                                {
                                                    cellCost += pathGridArray[neighborIndex];
                                                    cellCost += pawnDrafted ? topGrid[neighborIndex].extraDraftedPerceivedPathCost : topGrid[neighborIndex].extraNonDraftedPerceivedPathCost;
                                                }
                                                if (avoidGrid != null)
                                                {
                                                    cellCost += avoidGrid[neighborIndex] * 8;
                                                }
                                                if (allowedArea != null && !allowedArea[neighborIndex])
                                                {
                                                    cellCost += Cost_OutsideAllowedArea;
                                                }
                                                if (shouldCollideWithPawns && PawnUtility.AnyPawnBlockingPathAt(new IntVec3(neighborX, 0, neighborZ), pawn, false, false, true))
                                                {
                                                    cellCost += Cost_PawnCollision;
                                                }
                                                Building building2 = edificeGrid[neighborIndex];
                                                if (building2 != null)
                                                {
                                                    //PfProfilerBeginSample("Edifices"); //TODO
                                                    int buildingCost = GetBuildingCost(building2, traverseParms, pawn);
                                                    if (buildingCost == int.MaxValue)
                                                    {
                                                        //PfProfilerEndSample(); //TODO
                                                        break;
                                                    }
                                                    cellCost += buildingCost;
                                                    //PfProfilerEndSample(); //TODO
                                                }
                                                List<Blueprint> list = blueprintGrid.InnerArray[neighborIndex];
                                                if (list != null)
                                                {
                                                    //PfProfilerBeginSample("Blueprints"); //TODO
                                                    int blueprintCost = 0;
                                                    for (int j = 0; j < list.Count; j++)
                                                    {
                                                        blueprintCost = Math.Max(blueprintCost, GetBlueprintCost(list[j], pawn));
                                                    }
                                                    if (blueprintCost == int.MaxValue)
                                                    {
                                                        //PfProfilerEndSample(); //TODO
                                                        break;
                                                    }
                                                    cellCost += blueprintCost;
                                                    //PfProfilerEndSample(); //TODO
                                                }
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
                                                break;
                                            }
                                    }
                                }
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
                        InitStatusesAndPushStartNode(ref curIndex, start);
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
            return BlocksDiagonalMovement(x, z, map);
        }

        private bool BlocksDiagonalMovement(int index)
        {
            return BlocksDiagonalMovement(index, map);
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

        private void InitStatusesAndPushStartNode(ref int curIndex, IntVec3 start)
        {
            statusOpenValue += 2;
            statusClosedValue += 2;
            if (statusClosedValue >= 65435)
            {
                ResetStatuses();
            }
            curIndex = cellIndices.CellToIndex(start);
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

        private float DetermineHeuristicStrength(Pawn pawn, IntVec3 start, LocalTargetInfo dest)
        {
            if (pawn != null && pawn.RaceProps.Animal)
            {
                return NonRegionBasedHeuristicStrengthAnimal;
            }
            float lengthHorizontal = (start - dest.Cell).LengthHorizontal;
            return (float)Math.Round(NonRegionBasedHeuristicStrengthHuman_DistanceCurve.Evaluate(lengthHorizontal));
        }

        private CellRect CalculateDestinationRect(LocalTargetInfo dest, PathEndMode peMode)
        {
            CellRect result = (dest.HasThing && peMode != PathEndMode.OnCell) ? dest.Thing.OccupiedRect() : CellRect.SingleCell(dest.Cell);
            if (peMode == PathEndMode.Touch)
            {
                result = result.ExpandedBy(1);
            }
            return result;
        }

        private Area GetAllowedArea(Pawn pawn)
        {
            if (pawn != null && pawn.playerSettings != null && !pawn.Drafted && ForbidUtility.CaresAboutForbidden(pawn, true))
            {
                Area area = pawn.playerSettings.EffectiveAreaRestrictionInPawnCurrentMap;
                if (area != null && area.TrueCount <= 0)
                {
                    area = null;
                }
                return area;
            }
            return null;
        }

        private List<int> CalculateDisallowedCorners(TraverseParms traverseParms, PathEndMode peMode, CellRect destinationRect)
        {
            List<int> disallowedCornerIndices = new List<int>(4);
            if (peMode == PathEndMode.Touch)
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
