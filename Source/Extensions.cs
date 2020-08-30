using System;
using System.Linq;
using Verse;

namespace Trailblazer
{
    public static class TraverseModeExtensions
    {
        // TraverseMode passability
        private static readonly TraverseMode[] canDestroy = {
            TraverseMode.PassAllDestroyableThings,
            TraverseMode.PassAllDestroyableThingsNotWater
        };

        private static readonly TraverseMode[] canPassWater =
        {
            TraverseMode.ByPawn,
            TraverseMode.NoPassClosedDoors,
            TraverseMode.PassAllDestroyableThings,
            TraverseMode.PassDoors
        };

        public static bool CanDestroy(this TraverseMode traverseMode)
        {
            return canDestroy.Contains(traverseMode);
        }

        public static bool CanPassWater(this TraverseMode traverseMode)
        {
            return canPassWater.Contains(traverseMode);
        }
    }
}
