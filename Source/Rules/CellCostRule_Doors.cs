using RimWorld;
using Verse;

namespace Trailblazer.Rules
{
    /// <summary>
    /// Cost for moving through doors
    /// </summary>
    public class CellCostRule_Doors : CellCostRule
    {
        public const int Cost_BlockedDoorBase = 50;
        public const float Cost_BlockedDoorPerHitPoint = 0.2f;

        private readonly EdificeGrid edificeGrid;
        private readonly Pawn pawn;
        private readonly TraverseMode mode;

        public CellCostRule_Doors(PathfindData pathfindData) : base(pathfindData)
        {
            edificeGrid = pathfindData.map.edificeGrid;
            pawn = pathfindData.traverseParms.pawn;
            mode = pathfindData.traverseParms.mode;
        }

        public override bool Applies()
        {
            return mode == TraverseMode.PassAllDestroyableThings ||
                mode == TraverseMode.PassAllDestroyableThingsNotWater ||
                mode == TraverseMode.PassDoors ||
                mode == TraverseMode.ByPawn;
        }

        public override int GetCost(CellRef cell)
        {
            Building building = edificeGrid[cell.Index];
            if (building is Building_Door door)
            {
                if (pawn != null && door.PawnCanOpen(pawn) && !door.IsForbiddenToPass(pawn) && !door.FreePassage)
                {
                    return door.TicksToOpenNow;
                }
                if ((pawn != null && door.CanPhysicallyPass(pawn)) || door.FreePassage)
                {
                    return 0;
                }
                // NOTE: I don't know why all three of these cases have different costs in vanilla
                if (mode == TraverseMode.PassDoors)
                {
                    return 150;
                }
                if (mode == TraverseMode.ByPawn)
                {
                    // NOTE: Vanilla has a can-bash check here but that gets taken care of in the Passability rule
                    // ...no, I don't know why it checks if the pawn can bash but doesn't bother accounting for door
                    // hit points if they can.
                    return 300;
                }
                return Cost_BlockedDoorBase + (int)(door.HitPoints * Cost_BlockedDoorPerHitPoint);
            }
            return 0;
        }
    }
}
