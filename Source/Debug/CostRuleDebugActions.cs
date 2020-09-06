using System;
using System.Collections.Generic;
using Trailblazer.Rules;
using Verse;
using Verse.AI;

namespace Trailblazer.Debug
{
    public static class CostRuleDebugActions
    {
        private struct DebugCellCostRule
        {
            public readonly string name;
            public readonly Func<PathfindData, CellCostRule> ruleFactory;

            public DebugCellCostRule(string name, Func<PathfindData, CellCostRule> ruleFactory)
            {
                this.name = name;
                this.ruleFactory = ruleFactory;
            }
        }

        private static readonly DebugCellCostRule[] cellCostRules =
        {
            new DebugCellCostRule("AllowedArea", pathfindData => new CellCostRule_AllowedArea(pathfindData)),
            new DebugCellCostRule("AvoidGrid", pathfindData => new CellCostRule_AvoidGrid(pathfindData)),
            new DebugCellCostRule("Blueprints", pathfindData => new CellCostRule_Blueprints(pathfindData)),
            new DebugCellCostRule("Buildings", pathfindData => new CellCostRule_Buildings(pathfindData)),
            new DebugCellCostRule("Doors", pathfindData => new CellCostRule_Doors(pathfindData)),
            new DebugCellCostRule("PathGrid", pathfindData => new CellCostRule_PathGrid(pathfindData)),
            new DebugCellCostRule("Pawns", pathfindData => new CellCostRule_Pawns(pathfindData)),
            new DebugCellCostRule("Walls", pathfindData => new CellCostRule_Walls(pathfindData))
        };

        public const string CATEGORY_NAME = "Trailblazer Rules";


        [DebugAction(CATEGORY_NAME, "Cost (ByPawn)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void FlashRuleCostByPawn()
        {
            List<DebugMenuOption> list = new List<DebugMenuOption>();
            foreach (DebugCellCostRule rule in cellCostRules)
            {
                list.Add(new DebugMenuOption(rule.name, DebugMenuOptionMode.Tool, delegate {
                    Pawn pawn = UI.MouseCell().GetFirstPawn(Find.CurrentMap);
                    if (pawn != null)
                    {
                        FlashRuleCost(rule, pawn, TraverseMode.ByPawn, false);
                    }
                }));
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
        }


        [DebugAction(CATEGORY_NAME, "Cost (NoPassClosedDoors)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void FlashRuleCostNoPassClosedDoors()
        {
            List<DebugMenuOption> list = new List<DebugMenuOption>();
            foreach (DebugCellCostRule rule in cellCostRules)
            {
                list.Add(new DebugMenuOption(rule.name, DebugMenuOptionMode.Tool, delegate {
                    Pawn pawn = UI.MouseCell().GetFirstPawn(Find.CurrentMap);
                    if (pawn != null)
                    {
                        FlashRuleCost(rule, pawn, TraverseMode.NoPassClosedDoors, false);
                    }
                }));
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
        }


        [DebugAction(CATEGORY_NAME, "Cost (NoPassClosedDoorsOrWater)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void FlashRuleCostNoPassClosedDoorsOrWater()
        {
            List<DebugMenuOption> list = new List<DebugMenuOption>();
            foreach (DebugCellCostRule rule in cellCostRules)
            {
                list.Add(new DebugMenuOption(rule.name, DebugMenuOptionMode.Tool, delegate {
                    Pawn pawn = UI.MouseCell().GetFirstPawn(Find.CurrentMap);
                    if (pawn != null)
                    {
                        FlashRuleCost(rule, pawn, TraverseMode.NoPassClosedDoorsOrWater, false);
                    }
                }));
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
        }


        [DebugAction(CATEGORY_NAME, "Cost (PassAllDestroyableThings)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void FlashRuleCostPassAllDestroyableThings()
        {
            List<DebugMenuOption> list = new List<DebugMenuOption>();
            foreach (DebugCellCostRule rule in cellCostRules)
            {
                list.Add(new DebugMenuOption(rule.name, DebugMenuOptionMode.Tool, delegate {
                    Pawn pawn = UI.MouseCell().GetFirstPawn(Find.CurrentMap);
                    if (pawn != null)
                    {
                        FlashRuleCost(rule, pawn, TraverseMode.PassAllDestroyableThings, false);
                    }
                }));
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
        }


        [DebugAction(CATEGORY_NAME, "Cost (PassAllDestroyableThingsNotWater)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void FlashRuleCostPassAllDestroyableThingsNotWater()
        {
            List<DebugMenuOption> list = new List<DebugMenuOption>();
            foreach (DebugCellCostRule rule in cellCostRules)
            {
                list.Add(new DebugMenuOption(rule.name, DebugMenuOptionMode.Tool, delegate {
                    Pawn pawn = UI.MouseCell().GetFirstPawn(Find.CurrentMap);
                    if (pawn != null)
                    {
                        FlashRuleCost(rule, pawn, TraverseMode.PassAllDestroyableThingsNotWater, false);
                    }
                }));
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
        }


        [DebugAction(CATEGORY_NAME, "Cost (PassDoors)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void FlashRuleCostPassDoors()
        {
            List<DebugMenuOption> list = new List<DebugMenuOption>();
            foreach (DebugCellCostRule rule in cellCostRules)
            {
                list.Add(new DebugMenuOption(rule.name, DebugMenuOptionMode.Tool, delegate {
                    Pawn pawn = UI.MouseCell().GetFirstPawn(Find.CurrentMap);
                    if (pawn != null)
                    {
                        FlashRuleCost(rule, pawn, TraverseMode.PassDoors, false);
                    }
                }));
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
        }

        private static void FlashRuleCost(DebugCellCostRule rule, Pawn pawn, TraverseMode traverseMode, bool canBash)
        {
            Map map = pawn.Map;
            CellRef pawnPosition = map.GetCellRef(pawn.Position);
            TraverseParms traverseParms = TraverseParms.For(pawn, mode: traverseMode, canBash: canBash);
            LocalTargetInfo targetInfo = new LocalTargetInfo(pawnPosition);
            PathfindData pathfindData = new PathfindData(map, pawnPosition, targetInfo, traverseParms, PathEndMode.OnCell);

            CellCostRule costRule = rule.ruleFactory.Invoke(pathfindData);
            if (costRule.Applies())
            {
                foreach (IntVec3 c in map.AllCells)
                {
                    int cost = costRule.GetCost(map.GetCellRef(c));
                    map.debugDrawer.FlashCell(c, cost / 100f, cost.ToString());
                }
            }
            DebugActionsUtility.DustPuffFrom(pawn);
        }
    }
}
