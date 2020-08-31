using System.Collections.Generic;
using Verse;

namespace Trailblazer
{
    /// <summary>
    /// A more convenient reference to a cell that can return either the cell's index or it's coordinates.
    /// </summary>
    public class CellRef
    {
        private readonly CellIndices cellIndices;
        private int _index = -1;
        private IntVec3 _cell = IntVec3.Invalid;

        public int Index
        {
            get
            {
                if (_index == -1)
                {
                    _index = cellIndices.CellToIndex(_cell);
                }
                return _index;
            }
        }

        public IntVec3 Cell
        {
            get
            {
                if (!_cell.IsValid)
                {
                    _cell = cellIndices.IndexToCell(_index);
                }
                return _cell;
            }
        }

        public CellRef(CellIndices cellIndices, int index)
        {
            this.cellIndices = cellIndices;
            _index = index;
        }

        public CellRef(CellIndices cellIndices, IntVec3 cell)
        {
            this.cellIndices = cellIndices;
            _cell = cell;
        }

        public static implicit operator int(CellRef r) => r.Index;
        public static implicit operator IntVec3(CellRef r) => r.Cell;

        public static bool operator ==(CellRef a, CellRef b)
        {
            if (a != null)
            {
                return a.Equals(b);
            }
            return b == null;
        }

        public static bool operator !=(CellRef a, CellRef b)
        {
            return !(a == b);
        }

        public override int GetHashCode()
        {
            return Gen.HashCombineInt(cellIndices.NumGridCells, Gen.HashCombineInt(0, Index));
        }

        public override bool Equals(object obj)
        {
            var @ref = obj as CellRef;
            return @ref != null &&
                   EqualityComparer<CellIndices>.Default.Equals(cellIndices, @ref.cellIndices) &&
                   Index == @ref.Index;
        }
    }

    public static class CellRefUtils
    {
        public static CellRef GetCellRef(this Map map, int index)
        {
            return new CellRef(map.cellIndices, index);
        }

        public static CellRef GetCellRef(this Map map, IntVec3 cell)
        {
            return new CellRef(map.cellIndices, cell);
        }

        public static CellRef GetCellRef(this CellIndices cellIndices, int index)
        {
            return new CellRef(cellIndices, index);
        }

        public static CellRef GetCellRef(this CellIndices cellIndices, IntVec3 cell)
        {
            return new CellRef(cellIndices, cell);
        }
    }
}
