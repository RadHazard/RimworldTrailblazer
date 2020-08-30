using RimWorld;
using Verse;

namespace Trailblazer.Rules
{
    /// <summary>
    /// Cost penalty to a cell if it's outside the allowed area of the pawn
    /// </summary>
    public abstract class TrailblazerRule_CostAllowedArea : TrailblazerRule
    {
        protected const int Cost_OutsideAllowedArea = 600;

        protected readonly Area allowedArea;

        TrailblazerRule_CostAllowedArea(PathData pathData) : base(pathData)
        {
            Pawn pawn = pathData.traverseParms.pawn;

            Area area = null;
            if (pawn != null && pawn.playerSettings != null && !pawn.Drafted && ForbidUtility.CaresAboutForbidden(pawn, true))
            {
                area = pawn.playerSettings.EffectiveAreaRestrictionInPawnCurrentMap;
                if (area != null && area.TrueCount <= 0)
                {
                    area = null;
                }
            }

            allowedArea = area;
        }

        public override bool Applies()
        {
            return allowedArea != null;
        }

        public override int? GetConstantCost(MoveData moveData)
        {
            if (!allowedArea[moveData.cellIndex])
            {
                return Cost_OutsideAllowedArea;
            }
            return 0;
        }
    }
}
