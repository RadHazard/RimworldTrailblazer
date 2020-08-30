using System;
using Verse;

namespace Trailblazer
{
    /// <summary>
    /// Enum representing a movement along the grid for pathfinding.  This may or may not match up to
    /// the north direction that the player sees onscreen (but that shouldn't matter)
    /// </summary>
    public enum Move
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

    public static class MoveUtils
    {
        public static Move[] CardinalMoves =
        {
            Move.N,
            Move.E,
            Move.S,
            Move.W
        };

        public static Move[] DiagonalMoves =
        {
            Move.N,
            Move.E,
            Move.S,
            Move.W
        };

        /// <summary>
        /// Checks whether a move is cardinal.
        /// </summary>
        /// <returns><c>true</c> if cardinal, <c>false</c> otherwise.</returns>
        /// <param name="move">Move.</param>
        public static bool IsCardinal(this Move move)
        {
            return move == Move.N || move == Move.E || move == Move.S || move == Move.W;
        }

        /// <summary>
        /// Checks whether a move is diagonal.
        /// </summary>
        /// <returns><c>true</c> if diagonal, <c>false</c> otherwise.</returns>
        /// <param name="move">The move to test.</param>
        public static bool IsDiagonal(this Move move)
        {
            return move == Move.NE || move == Move.NW || move == Move.SE || move == Move.SW;
        }

        /// <summary>
        /// Returns the cell you would reach by moving in the given direction from the start cell
        /// </summary>
        /// <returns>The ending cell.</returns>
        /// <param name="move">The direction to move.</param>
        /// <param name="start">The starting cell.</param>
        public static IntVec3 From(this Move move, IntVec3 start)
        {
            switch (move)
            {
                case Move.N:
                    return start + new IntVec3(0, 0, 1);
                case Move.NE:
                    return start + new IntVec3(1, 0, 1);
                case Move.E:
                    return start + new IntVec3(1, 0, 0);
                case Move.SE:
                    return start + new IntVec3(1, 0, -1);
                case Move.S:
                    return start + new IntVec3(0, 0, -1);
                case Move.SW:
                    return start + new IntVec3(-1, 0, -1);
                case Move.W:
                    return start + new IntVec3(-1, 0, 0);
                case Move.NW:
                    return start + new IntVec3(-1, 0, 1);
                default:
                    // This can't actually happen
                    throw new Exception("Moved in an invalid direction");
            }
        }
    }
}
