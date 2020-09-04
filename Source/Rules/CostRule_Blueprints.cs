using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Trailblazer.Rules
{
    /// <summary>
    /// Cost penalty to a cell for the blueprints sitting on it
    /// </summary>
    public class CostRule_Blueprints : CostRule
    {
        private readonly BlueprintGrid blueprintGrid;
        private readonly Pawn pawn;

        public CostRule_Blueprints(PathfindData pathfindData) : base(pathfindData)
        {
            blueprintGrid = pathfindData.map.blueprintGrid;
            pawn = pathfindData.traverseParms.pawn;
        }

        public override bool Applies()
        {
            return pawn != null;
        }

        public override int GetCost(MoveData moveData)
        {
            List<Blueprint> list = blueprintGrid.InnerArray[moveData.cell.Index];
            if (list != null)
            {
                return (from blueprint in list
                        select blueprint.PathFindCostFor(pawn)).Max();
            }
            return 0;
        }
    }
}
