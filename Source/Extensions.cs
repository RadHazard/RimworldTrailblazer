using System;
using System.Collections.Generic;
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

    public static class ObjectExtensions
    {
        /// <summary>
        /// Yield the specified item as an IEnumerable (or an empty enumerable if it happens to be null)
        /// </summary>
        /// <returns>The enumerable.</returns>
        /// <param name="item">Item to yield.</param>
        /// <typeparam name="T">Tye type of the item to yield.</typeparam>
        public static IEnumerable<T> Yield<T>(this T item)
        {
#pragma warning disable RECS0017 // Possible compare of value type with 'null'
            if (item == null) yield break;
#pragma warning restore RECS0017 // Possible compare of value type with 'null'
            yield return item;
        }
    }
}
