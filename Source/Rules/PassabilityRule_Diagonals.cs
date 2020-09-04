using Verse;
using Verse.AI;

namespace Trailblazer.Rules
{
    /// <summary>
    /// Rule that declares a diagonal movement impossible if something would block it (like a wall)
    /// </summary>
    public class PassabilityRule_Diagonals : PassabilityRule
    {
        public PassabilityRule_Diagonals(PathfindData pathfindData) : base(pathfindData) { }

        public override bool IsPassable(MoveData moveData)
        {
            Direction sideA;
            Direction sideB;
            switch (moveData.enterDirection)
            {
                case Direction.NW:
                    sideA = Direction.S;
                    sideB = Direction.E;
                    break;
                case Direction.NE:
                    sideA = Direction.S;
                    sideB = Direction.W;
                    break;
                case Direction.SE:
                    sideA = Direction.N;
                    sideB = Direction.W;
                    break;
                case Direction.SW:
                    sideA = Direction.N;
                    sideB = Direction.E;
                    break;
                default:
                    return true;
            }

            if (BlocksDiagonalMovement(sideA.From(moveData.cell)) || BlocksDiagonalMovement(sideB.From(moveData.cell)))
            {
                // NOTE
                // The vanilla pathfinder has a weird if statement here.  It seems like in some cases blocked diagonal
                // movement is supposed to add a cost of 70 rather than making the move impossible, but if you trace the
                // boolean value it's always true and that case never happens. I'm replicating the effective result here
                // (since I don't understand in what cases it should have applied) but if the unused if-statement ever
                // gets fixed a cost rule should be added too
                return false;
            }
            return true;
        }

        /// <summary>
        /// Checks whether a cell blocks diagonal movement.
        /// </summary>
        /// <returns><c>true</c> if diagonal movement is blocked, <c>false</c> otherwise.</returns>
        /// <param name="cell">The cell to check.</param>
        private bool BlocksDiagonalMovement(IntVec3 cell)
        {
            return PathFinder.BlocksDiagonalMovement(cell.x, cell.z, pathfindData.map);
        }
    }
}
