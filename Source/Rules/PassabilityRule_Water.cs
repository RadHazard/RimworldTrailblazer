using Verse;

namespace Trailblazer.Rules
{
    /// <summary>
    /// Rule that declares water cells impassible when the TraverseMode requires it
    /// </summary>
    public class PassabilityRule_Water : PassabilityRule
    {
        public PassabilityRule_Water(PathfindData pathfindData) : base(pathfindData) { }

        public override bool Applies()
        {
            TraverseMode mode = pathfindData.traverseParms.mode;
            return mode == TraverseMode.NoPassClosedDoorsOrWater ||
                mode == TraverseMode.PassAllDestroyableThingsNotWater;
        }

        public override bool IsPassable(MoveData moveData)
        {
            return !moveData.cell.Cell.GetTerrain(pathfindData.map).HasTag("Water");
        }
    }
}
