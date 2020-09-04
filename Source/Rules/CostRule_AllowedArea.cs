using RimWorld;
using Verse;

namespace Trailblazer.Rules
{
    /// <summary>
    /// Cost penalty to a cell if it's outside the allowed area of the pawn
    /// </summary>
    public class CostRule_AllowedArea : CostRule
    {
        public const int Cost_OutsideAllowedArea = 600;

        protected readonly Area allowedArea;

        public CostRule_AllowedArea(PathfindData pathfindData) : base(pathfindData)
        {
            allowedArea = GetAllowedArea(pathfindData.traverseParms.pawn);
        }

        public override bool Applies()
        {
            return allowedArea != null;
        }

        public override int GetCost(MoveData moveData)
        {
            if (!allowedArea[moveData.cell.Index])
            {
                return Cost_OutsideAllowedArea;
            }
            return 0;
        }

        public static Area GetAllowedArea(Pawn pawn)
        {
            Area area = null;
            if (pawn != null && pawn.playerSettings != null && !pawn.Drafted && ForbidUtility.CaresAboutForbidden(pawn, true))
            {
                area = pawn.playerSettings.EffectiveAreaRestrictionInPawnCurrentMap;
                if (area != null && area.TrueCount <= 0)
                {
                    area = null;
                }
            }
            return area;
        }
    }
}
