namespace Trailblazer
{
    /// <summary>
    /// All the data associated with a specific move within a pathfinding job
    /// </summary>
    public struct MoveData
    {
        public readonly CellRef cell;
        public readonly Direction enterDirection;
        public readonly bool diagonal;

        public MoveData(CellRef cell, Direction enterDirection)
        {
            this.cell = cell;
            this.enterDirection = enterDirection;
            this.diagonal = enterDirection.IsDiagonal();
        }
    }
}
