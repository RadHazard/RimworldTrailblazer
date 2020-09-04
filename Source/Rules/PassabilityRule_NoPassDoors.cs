using RimWorld;
using Verse;

namespace Trailblazer.Rules
{
    /// <summary>
    /// Rule that declares doors impassible when the TraverseMode requires it
    /// </summary>
    public class PassabilityRule_NoPassDoors : PassabilityRule
    {
        private readonly EdificeGrid edificeGrid;

        public PassabilityRule_NoPassDoors(PathfindData pathfindData) : base(pathfindData)
        {
            edificeGrid = pathfindData.map.edificeGrid;
        }

        public override bool Applies()
        {
            TraverseMode mode = pathfindData.traverseParms.mode;
            return mode == TraverseMode.NoPassClosedDoors || mode == TraverseMode.NoPassClosedDoorsOrWater;
        }

        public override bool IsPassable(MoveData moveData)
        {
            Building building = edificeGrid[moveData.cell.Index];
            if (building is Building_Door door)
            {
                return door.FreePassage;
            }
            return true;
        }
    }
}
