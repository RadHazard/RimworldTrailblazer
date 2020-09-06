using System;
using System.Collections.Generic;
using Trailblazer.Rules;
using Verse;
using Verse.AI;

namespace Trailblazer.Debug
{
    public static class PassRuleDebugActions
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

        private struct DebugCellPassRule
        {
            public readonly string name;
            public readonly Func<PathfindData, CellPassabilityRule> ruleFactory;

            public DebugCellPassRule(string name, Func<PathfindData, CellPassabilityRule> ruleFactory)
            {
                this.name = name;
                this.ruleFactory = ruleFactory;
            }
        }

        private static readonly DebugCellPassRule[] cellCostRules =
        {
            new DebugCellPassRule("DoorByPawn", pathfindData => new CellPassabilityRule_DoorByPawn(pathfindData)),
            new DebugCellPassRule("NoPassDoors", pathfindData => new CellPassabilityRule_NoPassDoors(pathfindData)),
            new DebugCellPassRule("PathGrid", pathfindData => new CellPassabilityRule_PathGrid(pathfindData)),
            new DebugCellPassRule("Water", pathfindData => new CellPassabilityRule_Water(pathfindData)),
        };

        public const string CATEGORY_NAME = "Trailblazer Rules";


        [DebugAction(CATEGORY_NAME, "Passability (ByPawn)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void FlashRuleByPawn()
        {
            List<DebugMenuOption> list = new List<DebugMenuOption>();
            foreach (DebugCellPassRule rule in cellCostRules)
            {
                list.Add(new DebugMenuOption(rule.name, DebugMenuOptionMode.Tool, delegate {
                    Pawn pawn = UI.MouseCell().GetFirstPawn(Find.CurrentMap);
                    if (pawn != null)
                    {
                        FlashRulePassability(rule, pawn, TraverseMode.ByPawn, false);
                    }
                }));
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
        }


        [DebugAction(CATEGORY_NAME, "Passability (ByPawn, canBash)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void FlashRuleByPawnCanBash()
        {
            List<DebugMenuOption> list = new List<DebugMenuOption>();
            foreach (DebugCellPassRule rule in cellCostRules)
            {
                list.Add(new DebugMenuOption(rule.name, DebugMenuOptionMode.Tool, delegate {
                    Pawn pawn = UI.MouseCell().GetFirstPawn(Find.CurrentMap);
                    if (pawn != null)
                    {
                        FlashRulePassability(rule, pawn, TraverseMode.ByPawn, true);
                    }
                }));
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
        }


        [DebugAction(CATEGORY_NAME, "Passability (NoPassClosedDoors)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void FlashRuleNoPassClosedDoors()
        {
            List<DebugMenuOption> list = new List<DebugMenuOption>();
            foreach (DebugCellPassRule rule in cellCostRules)
            {
                list.Add(new DebugMenuOption(rule.name, DebugMenuOptionMode.Tool, delegate {
                    Pawn pawn = UI.MouseCell().GetFirstPawn(Find.CurrentMap);
                    if (pawn != null)
                    {
                        FlashRulePassability(rule, pawn, TraverseMode.NoPassClosedDoors, false);
                    }
                }));
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
        }


        [DebugAction(CATEGORY_NAME, "Passability (NoPassClosedDoorsOrWater)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void FlashRuleNoPassClosedDoorsOrWater()
        {
            List<DebugMenuOption> list = new List<DebugMenuOption>();
            foreach (DebugCellPassRule rule in cellCostRules)
            {
                list.Add(new DebugMenuOption(rule.name, DebugMenuOptionMode.Tool, delegate {
                    Pawn pawn = UI.MouseCell().GetFirstPawn(Find.CurrentMap);
                    if (pawn != null)
                    {
                        FlashRulePassability(rule, pawn, TraverseMode.NoPassClosedDoorsOrWater, false);
                    }
                }));
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
        }


        [DebugAction(CATEGORY_NAME, "Passability (PassAllDestroyableThings)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void FlashRulePassAllDestroyableThings()
        {
            List<DebugMenuOption> list = new List<DebugMenuOption>();
            foreach (DebugCellPassRule rule in cellCostRules)
            {
                list.Add(new DebugMenuOption(rule.name, DebugMenuOptionMode.Tool, delegate {
                    Pawn pawn = UI.MouseCell().GetFirstPawn(Find.CurrentMap);
                    if (pawn != null)
                    {
                        FlashRulePassability(rule, pawn, TraverseMode.PassAllDestroyableThings, false);
                    }
                }));
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
        }


        [DebugAction(CATEGORY_NAME, "Passability (PassAllDestroyableThingsNotWater)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void FlashRulePassAllDestroyableThingsNotWater()
        {
            List<DebugMenuOption> list = new List<DebugMenuOption>();
            foreach (DebugCellPassRule rule in cellCostRules)
            {
                list.Add(new DebugMenuOption(rule.name, DebugMenuOptionMode.Tool, delegate {
                    Pawn pawn = UI.MouseCell().GetFirstPawn(Find.CurrentMap);
                    if (pawn != null)
                    {
                        FlashRulePassability(rule, pawn, TraverseMode.PassAllDestroyableThingsNotWater, false);
                    }
                }));
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
        }


        [DebugAction(CATEGORY_NAME, "Passability (PassDoors)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void FlashRulePassDoors()
        {
            List<DebugMenuOption> list = new List<DebugMenuOption>();
            foreach (DebugCellPassRule rule in cellCostRules)
            {
                list.Add(new DebugMenuOption(rule.name, DebugMenuOptionMode.Tool, delegate {
                    Pawn pawn = UI.MouseCell().GetFirstPawn(Find.CurrentMap);
                    if (pawn != null)
                    {
                        FlashRulePassability(rule, pawn, TraverseMode.PassDoors, false);
                    }
                }));
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
        }

        private static void FlashRulePassability(DebugCellPassRule rule, Pawn pawn, TraverseMode traverseMode, bool canBash)
        {
            Map map = pawn.Map;
            CellRef pawnPosition = map.GetCellRef(pawn.Position);
            TraverseParms traverseParms = TraverseParms.For(pawn, mode: traverseMode, canBash: canBash);
            LocalTargetInfo targetInfo = new LocalTargetInfo(pawnPosition);
            PathfindData pathfindData = new PathfindData(map, pawnPosition, targetInfo, traverseParms, PathEndMode.OnCell);

            CellPassabilityRule passRule = rule.ruleFactory.Invoke(pathfindData);
            if (passRule.Applies())
            {
                foreach (IntVec3 c in map.AllCells)
                {
                    float color = passRule.IsPassable(map.GetCellRef(c)) ? 0.5f : 0f;
                    map.debugDrawer.FlashCell(c, color);
                }
            }
            DebugActionsUtility.DustPuffFrom(pawn);
        }
    }
}
