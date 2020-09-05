using System;
using Verse;

namespace Trailblazer
{
    /// <summary>
    /// Enum representing a movement along the grid for pathfinding.  This may or may not match up to
    /// the north direction that the player sees onscreen (but that shouldn't matter)
    /// </summary>
    public enum Direction
    {
        N,
        NE,
        E,
        SE,
        S,
        SW,
        W,
        NW
    }

    public static class DirectionUtils
    {
        public static Direction[] AllDirections =
        {
            Direction.N,
            Direction.NE,
            Direction.E,
            Direction.SE,
            Direction.S,
            Direction.SW,
            Direction.W,
            Direction.NW
        };

        public static Direction[] CardinalDirections =
        {
            Direction.N,
            Direction.E,
            Direction.S,
            Direction.W
        };

        public static Direction[] DiagonalDirections =
        {
            Direction.N,
            Direction.E,
            Direction.S,
            Direction.W
        };

        /// <summary>
        /// Checks whether a direction is cardinal.
        /// </summary>
        /// <returns><c>true</c> if cardinal, <c>false</c> otherwise.</returns>
        /// <param name="direction">Direction.</param>
        public static bool IsCardinal(this Direction direction)
        {
            return direction == Direction.N || direction == Direction.E || direction == Direction.S || direction == Direction.W;
        }

        /// <summary>
        /// Checks whether a direction is diagonal.
        /// </summary>
        /// <returns><c>true</c> if diagonal, <c>false</c> otherwise.</returns>
        /// <param name="direction">Direction.</param>
        public static bool IsDiagonal(this Direction direction)
        {
            return direction == Direction.NE || direction == Direction.NW || direction == Direction.SE || direction == Direction.SW;
        }

        /// <summary>
        /// Returns the cell you would reach by moving in the given direction from the start cell
        /// </summary>
        /// <returns>The ending cell.</returns>
        /// <param name="direction">The direction to move.</param>
        /// <param name="start">The starting cell.</param>
        public static CellRef From(this Direction direction, CellRef start)
        {
            switch (direction)
            {
                case Direction.N:
                    return start.Relative(0, 1);
                case Direction.NE:
                    return start.Relative(1, 1);
                case Direction.E:
                    return start.Relative(1, 0);
                case Direction.SE:
                    return start.Relative(1, -1);
                case Direction.S:
                    return start.Relative(0, -1);
                case Direction.SW:
                    return start.Relative(-1, -1);
                case Direction.W:
                    return start.Relative(-1, 0);
                case Direction.NW:
                    return start.Relative(-1, 1);
                default:
                    // This can't actually happen
                    throw new Exception("Moved in an invalid direction");
            }
        }

        /// <summary>
        /// Returns the cell you would reach by moving in the given direction from the start cell
        /// </summary>
        /// <returns>The ending cell.</returns>
        /// <param name="direction">The direction to move.</param>
        /// <param name="start">The starting cell.</param>
        public static IntVec3 From(this Direction direction, IntVec3 start)
        {
            switch (direction)
            {
                case Direction.N:
                    return start + new IntVec3(0, 0, 1);
                case Direction.NE:
                    return start + new IntVec3(1, 0, 1);
                case Direction.E:
                    return start + new IntVec3(1, 0, 0);
                case Direction.SE:
                    return start + new IntVec3(1, 0, -1);
                case Direction.S:
                    return start + new IntVec3(0, 0, -1);
                case Direction.SW:
                    return start + new IntVec3(-1, 0, -1);
                case Direction.W:
                    return start + new IntVec3(-1, 0, 0);
                case Direction.NW:
                    return start + new IntVec3(-1, 0, 1);
                default:
                    // This can't actually happen
                    throw new Exception("Moved in an invalid direction");
            }
        }
    }
}
