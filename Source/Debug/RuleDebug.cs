using System;
using System.Collections.Generic;
using Trailblazer.Rules;
using Verse;
using Verse.AI;

namespace Trailblazer.Debug
{
    public static class RuleDebugLogging
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

        public const string CATEGORY_NAME = "Trailblazer";

        [DebugAction(CATEGORY_NAME, "Flash Rule Cost (ByPawn)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void FlashRuleCostByPawn()
        {
            List<DebugMenuOption> list = new List<DebugMenuOption>();
            foreach (DebugCellCostRule rule in cellCostRules)
            {
                list.Add(new DebugMenuOption(rule.name, DebugMenuOptionMode.Tool, delegate {
                    Pawn pawn = UI.MouseCell().GetFirstPawn(Find.CurrentMap);
                    if (pawn != null)
                    {
                        FlashRuleCost(pawn, TraverseMode.ByPawn, false, rule);
                    }
                }));
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
        }

        [DebugAction(CATEGORY_NAME, "Flash Rule Cost (ByPawn, canBash)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void FlashRuleCostByPawnCanBash()
        {
            List<DebugMenuOption> list = new List<DebugMenuOption>();
            foreach (DebugCellCostRule rule in cellCostRules)
            {
                list.Add(new DebugMenuOption(rule.name, DebugMenuOptionMode.Tool, delegate {
                    Pawn pawn = UI.MouseCell().GetFirstPawn(Find.CurrentMap);
                    if (pawn != null)
                    {
                        FlashRuleCost(pawn, TraverseMode.ByPawn, true, rule);
                    }
                }));
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
        }

        private static void FlashRuleCost(Pawn pawn, TraverseMode traverseMode, bool canBash, DebugCellCostRule rule)
        {
            Map map = pawn.Map;
            CellRef pawnPosition = map.GetCellRef(pawn.Position);
            TraverseParms traverseParms = TraverseParms.For(pawn, mode: traverseMode);
            LocalTargetInfo targetInfo = new LocalTargetInfo(pawnPosition);
            PathfindData pathfindData = new PathfindData(map, pawnPosition, targetInfo, traverseParms, PathEndMode.OnCell);

            CellCostRule costRule = rule.ruleFactory.Invoke(pathfindData);
            foreach (IntVec3 c in map.AllCells)
            {
                int cost = costRule.GetCost(map.GetCellRef(c));
                map.debugDrawer.FlashCell(c, cost / 1000f, cost.ToString());
            }
        }
    }
}
