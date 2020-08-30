using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Trailblazer.Rules
{
    /// <summary>
    /// Cost penalty to a cell for the blueprints sitting on it
    /// </summary>
    public abstract class TrailblazerRule_CostBlueprints : TrailblazerRule
    {
        protected const int Cost_OutsideAllowedArea = 600;

        protected readonly BlueprintGrid blueprintGrid;
        protected readonly Pawn pawn;

        TrailblazerRule_CostBlueprints(PathData pathData) : base(pathData)
        {
            blueprintGrid = pathData.map.blueprintGrid;

        }

        public override bool Applies()
        {
            return pawn != null;
        }

        public override int? GetConstantCost(MoveData moveData)
        {
            List<Blueprint> list = blueprintGrid.InnerArray[moveData.cellIndex];
            if (list != null)
            {
                return (from blueprint in list
                        select blueprint.PathFindCostFor(pawn)).Max();
            }
            return 0;
        }
    }
}
