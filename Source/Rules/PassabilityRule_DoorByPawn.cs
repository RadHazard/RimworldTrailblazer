using RimWorld;
using Verse;

namespace Trailblazer.Rules
{
    /// <summary>
    /// Rule that decideds whether doors are impassible in the ByPawn traverse mode
    /// </summary>
    public class PassabilityRule_DoorByPawn : PassabilityRule
    {
        private readonly EdificeGrid edificeGrid;
        private readonly Pawn pawn;
        private readonly bool canBash;

        public PassabilityRule_DoorByPawn(PathfindData pathfindData) : base(pathfindData)
        {
            edificeGrid = pathfindData.map.edificeGrid;
            pawn = pathfindData.traverseParms.pawn;
            canBash = pathfindData.traverseParms.canBash;
        }

        public override bool Applies()
        {
            TraverseMode mode = pathfindData.traverseParms.mode;
            return mode == TraverseMode.ByPawn;
        }

        public override bool IsPassable(MoveData moveData)
        {
            Building building = edificeGrid[moveData.cell.Index];
            if (building is Building_Door door)
            {
                if (door.IsForbiddenToPass(pawn))
                {
                    return canBash;
                }
                return door.CanPhysicallyPass(pawn) || canBash;
            }
            return true;
        }
    }
}
