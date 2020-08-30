using System;
using Verse;

namespace Trailblazer.Rules
{
    /// <summary>
    /// Rule that declares water cells impassible when the TraverseMode requires it
    /// </summary>
    public abstract class TrailblazerRule_PassabilityWater : TrailblazerRule
    {
        protected readonly bool active;

        TrailblazerRule_PassabilityWater(PathData pathData) : base(pathData)
        {
            active = !pathData.traverseParms.mode.CanPassWater();
        }

        public override bool Applies()
        {
            return active;
        }

        public override int? GetConstantCost(MoveData cellData)
        {
            if (cellData.cell.GetTerrain(pathData.map).HasTag("Water"))
            {
                return null;
            }
            return 0;
        }
    }
}
