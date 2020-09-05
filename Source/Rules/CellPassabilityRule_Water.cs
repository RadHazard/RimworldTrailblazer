using Verse;

namespace Trailblazer.Rules
{
    /// <summary>
    /// Rule that declares water cells impassible when the TraverseMode requires it
    /// </summary>
    public class CellPassabilityRule_Water : CellPassabilityRule
    {
        public CellPassabilityRule_Water(PathfindData pathfindData) : base(pathfindData) { }

        public override bool Applies()
        {
            TraverseMode mode = pathfindData.traverseParms.mode;
            return mode == TraverseMode.NoPassClosedDoorsOrWater ||
                mode == TraverseMode.PassAllDestroyableThingsNotWater;
        }

        public override bool IsPassable(CellRef cell)
        {
            return !cell.Cell.GetTerrain(pathfindData.map).HasTag("Water");
        }
    }
}
