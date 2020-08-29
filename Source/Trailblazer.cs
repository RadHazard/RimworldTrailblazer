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

        private Map map;

        private FastPriorityQueue<CostNode> openList;

        private static PathFinderNodeFast[] calcGrid;

        private static ushort statusOpenValue = 1;

        private static ushort statusClosedValue = 2;

        private RegionCostCalculatorWrapper regionCostCalculator;

        private int mapSizeX;

        private int mapSizeZ;

        private PathGrid pathGrid;

        private Building[] edificeGrid;

        private List<Blueprint>[] blueprintGrid;

        private CellIndices cellIndices;

        private List<int> disallowedCornerIndices = new List<int>(4);

        public const int DefaultMoveTicksCardinal = 13;

        private const int DefaultMoveTicksDiagonal = 18;

        private const int SearchLimit = 160000;

        private static readonly int[] Directions = new int[16] {
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

        private const int Cost_DoorToBash = 300;

        private const int Cost_BlockedWallBase = 70;

        private const float Cost_BlockedWallExtraPerHitPoint = 0.2f;

        private const int Cost_BlockedDoor = 50;

        private const float Cost_BlockedDoorPerHitPoint = 0.2f;

        public const int Cost_OutsideAllowedArea = 600;

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
            int num = mapSizeX * mapSizeZ;
            if (calcGrid == null || calcGrid.Length < num)
            {
                calcGrid = new PathFinderNodeFast[num];
            }
            openList = new FastPriorityQueue<CostNode>(new CostNodeComparer());
            regionCostCalculator = new RegionCostCalculatorWrapper(map);
        }

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
            //PfProfilerBeginSample("FindPath for " + pawn + " from " + start + " to " + dest + (dest.HasThing ? (" at " + dest.Cell) : "")); //TODO
            cellIndices = map.cellIndices;
            pathGrid = map.pathGrid;
            this.edificeGrid = map.edificeGrid.InnerArray;
            blueprintGrid = map.blueprintGrid.InnerArray;
            int x = dest.Cell.x;
            int z = dest.Cell.z;
            int curIndex = cellIndices.CellToIndex(start);
            int num = cellIndices.CellToIndex(dest.Cell);
            ByteGrid byteGrid = (pawn != null) ? pawn.GetAvoidGrid(true) : null;
            bool flag = traverseParms.mode == TraverseMode.PassAllDestroyableThings || traverseParms.mode == TraverseMode.PassAllDestroyableThingsNotWater;
            bool flag2 = traverseParms.mode != TraverseMode.NoPassClosedDoorsOrWater && traverseParms.mode != TraverseMode.PassAllDestroyableThingsNotWater;
            bool flag3 = !flag;
            CellRect cellRect = CalculateDestinationRect(dest, peMode);
            bool flag4 = cellRect.Width == 1 && cellRect.Height == 1;
            int[] array = map.pathGrid.pathGrid;
            TerrainDef[] topGrid = map.terrainGrid.topGrid;
            EdificeGrid edificeGrid = map.edificeGrid;
            int num2 = 0;
            int num3 = 0;
            Area allowedArea = GetAllowedArea(pawn);
            bool flag5 = pawn != null && PawnUtility.ShouldCollideWithPawns(pawn);
            bool flag6 = (!flag && start.GetRegion(map, RegionType.Set_Passable) != null) & flag2;
            bool flag7 = !flag || !flag3;
            bool flag8 = false;
            bool flag9 = pawn?.Drafted ?? false;
            int num4 = (pawn?.IsColonist ?? false) ? 100000 : 2000;
            int num5 = 0;
            int num6 = 0;
            float num7 = DetermineHeuristicStrength(pawn, start, dest);
            int num8;
            int num9;
            if (pawn != null)
            {
                num8 = pawn.TicksPerMoveCardinal;
                num9 = pawn.TicksPerMoveDiagonal;
            }
            else
            {
                num8 = 13;
                num9 = 18;
            }
            CalculateAndAddDisallowedCorners(traverseParms, peMode, cellRect);
            InitStatusesAndPushStartNode(ref curIndex, start);
            while (true)
            {
                //PfProfilerBeginSample("Open cell"); //TODO
                if (openList.Count <= 0)
                {
                    string text = (pawn != null && pawn.CurJob != null) ? pawn.CurJob.ToString() : "null";
                    string text2 = (pawn != null && pawn.Faction != null) ? pawn.Faction.ToString() : "null";
                    Log.Warning(pawn + " pathing from " + start + " to " + dest + " ran out of cells to process.\nJob:" + text + "\nFaction: " + text2, false);
                    //TODO
                    //DebugDrawRichData();
                    //PfProfilerEndSample();
                    //PfProfilerEndSample();
                    return PawnPath.NotFound;
                }
                num5 += openList.Count;
                num6++;
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
                    int x2 = intVec.x;
                    int z2 = intVec.z;
                    if (flag4)
                    {
                        if (curIndex == num)
                        {
                            //PfProfilerEndSample(); //TODO
                            PawnPath result = FinalizedPath(curIndex, flag8);
                            //PfProfilerEndSample(); //TODO
                            return result;
                        }
                    }
                    else if (cellRect.Contains(intVec) && !disallowedCornerIndices.Contains(curIndex))
                    {
                        //PfProfilerEndSample(); //TODO
                        PawnPath result2 = FinalizedPath(curIndex, flag8);
                        //PfProfilerEndSample(); //TODO
                        return result2;
                    }
                    if (num2 > 160000)
                    {
                        break;
                    }
                    //TODO
                    //PfProfilerEndSample();
                    //PfProfilerBeginSample("Neighbor consideration");
                    for (int i = 0; i < 8; i++)
                    {
                        uint num10 = (uint)(x2 + Directions[i]);
                        uint num11 = (uint)(z2 + Directions[i + 8]);
                        if (num10 < mapSizeX && num11 < mapSizeZ)
                        {
                            int num12 = (int)num10;
                            int num13 = (int)num11;
                            int num14 = cellIndices.CellToIndex(num12, num13);
                            if (calcGrid[num14].status != statusClosedValue || flag8)
                            {
                                int num15 = 0;
                                bool flag10 = false;
                                if (flag2 || !new IntVec3(num12, 0, num13).GetTerrain(map).HasTag("Water"))
                                {
                                    if (!pathGrid.WalkableFast(num14))
                                    {
                                        if (!flag)
                                        {
                                            continue;
                                        }
                                        flag10 = true;
                                        num15 += 70;
                                        Building building = edificeGrid[num14];
                                        if (building == null || !IsDestroyable(building))
                                        {
                                            continue;
                                        }
                                        num15 += (int)((float)building.HitPoints * 0.2f);
                                    }
                                    switch (i)
                                    {
                                        case 4:
                                            if (BlocksDiagonalMovement(curIndex - mapSizeX))
                                            {
                                                if (flag7)
                                                {
                                                    break;
                                                }
                                                num15 += 70;
                                            }
                                            if (BlocksDiagonalMovement(curIndex + 1))
                                            {
                                                if (flag7)
                                                {
                                                    break;
                                                }
                                                num15 += 70;
                                            }
                                            goto default;
                                        case 5:
                                            if (BlocksDiagonalMovement(curIndex + mapSizeX))
                                            {
                                                if (flag7)
                                                {
                                                    break;
                                                }
                                                num15 += 70;
                                            }
                                            if (BlocksDiagonalMovement(curIndex + 1))
                                            {
                                                if (flag7)
                                                {
                                                    break;
                                                }
                                                num15 += 70;
                                            }
                                            goto default;
                                        case 6:
                                            if (BlocksDiagonalMovement(curIndex + mapSizeX))
                                            {
                                                if (flag7)
                                                {
                                                    break;
                                                }
                                                num15 += 70;
                                            }
                                            if (BlocksDiagonalMovement(curIndex - 1))
                                            {
                                                if (flag7)
                                                {
                                                    break;
                                                }
                                                num15 += 70;
                                            }
                                            goto default;
                                        case 7:
                                            if (BlocksDiagonalMovement(curIndex - mapSizeX))
                                            {
                                                if (flag7)
                                                {
                                                    break;
                                                }
                                                num15 += 70;
                                            }
                                            if (BlocksDiagonalMovement(curIndex - 1))
                                            {
                                                if (flag7)
                                                {
                                                    break;
                                                }
                                                num15 += 70;
                                            }
                                            goto default;
                                        default:
                                            {
                                                int num16 = (i > 3) ? num9 : num8;
                                                num16 += num15;
                                                if (!flag10)
                                                {
                                                    num16 += array[num14];
                                                    num16 = ((!flag9) ? (num16 + topGrid[num14].extraNonDraftedPerceivedPathCost) : (num16 + topGrid[num14].extraDraftedPerceivedPathCost));
                                                }
                                                if (byteGrid != null)
                                                {
                                                    num16 += byteGrid[num14] * 8;
                                                }
                                                if (allowedArea != null && !allowedArea[num14])
                                                {
                                                    num16 += 600;
                                                }
                                                if (flag5 && PawnUtility.AnyPawnBlockingPathAt(new IntVec3(num12, 0, num13), pawn, false, false, true))
                                                {
                                                    num16 += 175;
                                                }
                                                Building building2 = this.edificeGrid[num14];
                                                if (building2 != null)
                                                {
                                                    //PfProfilerBeginSample("Edifices"); //TODO
                                                    int buildingCost = GetBuildingCost(building2, traverseParms, pawn);
                                                    if (buildingCost == 2147483647)
                                                    {
                                                        //PfProfilerEndSample(); //TODO
                                                        break;
                                                    }
                                                    num16 += buildingCost;
                                                    //PfProfilerEndSample(); //TODO
                                                }
                                                List<Blueprint> list = blueprintGrid[num14];
                                                if (list != null)
                                                {
                                                    //PfProfilerBeginSample("Blueprints"); //TODO
                                                    int num17 = 0;
                                                    for (int j = 0; j < list.Count; j++)
                                                    {
                                                        num17 = Math.Max(num17, GetBlueprintCost(list[j], pawn));
                                                    }
                                                    if (num17 == 2147483647)
                                                    {
                                                        //PfProfilerEndSample(); //TODO
                                                        break;
                                                    }
                                                    num16 += num17;
                                                    //PfProfilerEndSample(); //TODO
                                                }
                                                int num18 = num16 + calcGrid[curIndex].knownCost;
                                                ushort status = calcGrid[num14].status;
                                                if (status == statusClosedValue || status == statusOpenValue)
                                                {
                                                    int num19 = 0;
                                                    if (status == statusClosedValue)
                                                    {
                                                        num19 = num8;
                                                    }
                                                    if (calcGrid[num14].knownCost <= num18 + num19)
                                                    {
                                                        break;
                                                    }
                                                }
                                                if (flag8)
                                                {
                                                    calcGrid[num14].heuristicCost = (int)Math.Round(regionCostCalculator.GetPathCostFromDestToRegion(num14) * RegionHeuristicWeightByNodesOpened.Evaluate(num3));
                                                    if (calcGrid[num14].heuristicCost < 0)
                                                    {
                                                        Log.ErrorOnce("Heuristic cost overflow for " + pawn.ToStringSafe() + " pathing from " + start + " to " + dest + ".", pawn.GetHashCode() ^ 0xB8DC389, false);
                                                        calcGrid[num14].heuristicCost = 0;
                                                    }
                                                }
                                                else if (status != statusClosedValue && status != statusOpenValue)
                                                {
                                                    int dx = Math.Abs(num12 - x);
                                                    int dz = Math.Abs(num13 - z);
                                                    int num20 = GenMath.OctileDistance(dx, dz, num8, num9);
                                                    calcGrid[num14].heuristicCost = (int)Math.Round(num20 * num7);
                                                }
                                                int num21 = num18 + calcGrid[num14].heuristicCost;
                                                if (num21 < 0)
                                                {
                                                    Log.ErrorOnce("Node cost overflow for " + pawn.ToStringSafe() + " pathing from " + start + " to " + dest + ".", pawn.GetHashCode() ^ 0x53CB9DE, false);
                                                    num21 = 0;
                                                }
                                                calcGrid[num14].parentIndex = curIndex;
                                                calcGrid[num14].knownCost = num18;
                                                calcGrid[num14].status = statusOpenValue;
                                                calcGrid[num14].costNodeCost = num21;
                                                num3++;
                                                openList.Push(new CostNode(num14, num21));
                                                break;
                                            }
                                    }
                                }
                            }
                        }
                    }
                    //PfProfilerEndSample(); //TODO
                    num2++;
                    calcGrid[curIndex].status = statusClosedValue;
                    if (((num3 >= num4) & flag6) && !flag8)
                    {
                        flag8 = true;
                        regionCostCalculator.Init(cellRect, traverseParms, num8, num9, byteGrid, allowedArea, flag9, disallowedCornerIndices);
                        InitStatusesAndPushStartNode(ref curIndex, start);
                        num3 = 0;
                        num2 = 0;
                    }
                }
            }
            Log.Warning(pawn + " pathing from " + start + " to " + dest + " hit search limit of " + 160000 + " cells.", false);
            //TODO
            //DebugDrawRichData();
            //PfProfilerEndSample();
            //PfProfilerEndSample();
            return PawnPath.NotFound;
        }

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
                return 1.75f;
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

        private void CalculateAndAddDisallowedCorners(TraverseParms traverseParms, PathEndMode peMode, CellRect destinationRect)
        {
            disallowedCornerIndices.Clear();
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
        }

        private bool IsCornerTouchAllowed(int cornerX, int cornerZ, int adjCardinal1X, int adjCardinal1Z, int adjCardinal2X, int adjCardinal2Z)
        {
            return TouchPathEndModeUtility.IsCornerTouchAllowed(cornerX, cornerZ, adjCardinal1X, adjCardinal1Z, adjCardinal2X, adjCardinal2Z, map);
        }
    }
}
