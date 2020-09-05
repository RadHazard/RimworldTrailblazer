using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Trailblazer.Rules
{
    /// <summary>
    /// Cost penalty to a cell for the blueprints sitting on it
    /// </summary>
    public class CellCostRule_Blueprints : CellCostRule
    {
        private readonly BlueprintGrid blueprintGrid;
        private readonly Pawn pawn;

        public CellCostRule_Blueprints(PathfindData pathfindData) : base(pathfindData)
        {
            blueprintGrid = pathfindData.map.blueprintGrid;
            pawn = pathfindData.traverseParms.pawn;
        }

        public override bool Applies()
        {
            return pawn != null;
        }

        public override int GetCost(CellRef cell)
        {
            List<Blueprint> list = blueprintGrid.InnerArray[cell.Index];
            if (list != null)
            {
                return (from blueprint in list
                        select blueprint.PathFindCostFor(pawn)).Max();
            }
            return 0;
        }
    }
}
