using Verse;

namespace Trailblazer.Rules
{
    /// <summary>
    /// Rule that declares water cells impassible when the TraverseMode requires it
    /// </summary>
    public class TrailblazerRule_PassabilityWater : TrailblazerRule
    {
        protected readonly bool active;

        public TrailblazerRule_PassabilityWater(PathData pathData) : base(pathData)
        {
            active = !pathData.traverseParms.mode.CanPassWater();
        }

        public override bool Applies()
        {
            return active;
        }

        public override int? GetConstantCost(MoveData moveData)
        {
            if (moveData.cell.GetTerrain(pathData.map).HasTag("Water"))
            {
                return null;
            }
            return 0;
        }
    }
}
