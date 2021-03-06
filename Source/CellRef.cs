﻿using System.Collections.Generic;
using Verse;

namespace Trailblazer
{
    /// <summary>
    /// A more convenient reference to a cell that can return either the cell's index or it's coordinates.
    /// </summary>
    public class CellRef
    {
        private readonly int mapSizeX;
        private readonly int mapSizeZ;
        private int _index = -1;
        private IntVec3 _cell = IntVec3.Invalid;

        public int Index
        {
            get
            {
                if (_index == -1)
                {
                    if (_cell.x < mapSizeX && _cell.z < mapSizeZ)
                    {
                        _index = CellIndicesUtility.CellToIndex(_cell, mapSizeX);
                    }
                    else
                    {
                        _index = -2;
                    }
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
                    _cell = CellIndicesUtility.IndexToCell(_index, mapSizeX);
                }
                return _cell;
            }
        }

        public CellRef(int mapSizeX, int mapSizeZ, int index)
        {
            this.mapSizeX = mapSizeX;
            this.mapSizeZ = mapSizeZ;
            _index = index;
        }

        public CellRef(int mapSizeX, int mapSizeZ, IntVec3 cell)
        {
            this.mapSizeX = mapSizeX;
            this.mapSizeZ = mapSizeZ;
            _cell = cell;
        }

        public CellRef Relative(IntVec3 delta)
        {
            return new CellRef(mapSizeX, mapSizeZ, _cell + delta);
        }

        public CellRef Relative(int dx, int dz)
        {
            return Relative(new IntVec3(dx, 0, dz));
        }

        public bool InBounds()
        {
            return Index > 0 && Index < mapSizeX * mapSizeZ;
        }

        public static implicit operator int(CellRef r) => r.Index;
        public static implicit operator IntVec3(CellRef r) => r.Cell;

        public static bool operator ==(CellRef a, CellRef b)
        {
            if (!(a is null))
            {
                return a.Equals(b);
            }
            return b is null;
        }

        public static bool operator !=(CellRef a, CellRef b)
        {
            if (!(a is null))
            {
                return !a.Equals(b);
            }
            return !(b is null);
        }

        public override string ToString()
        {
            return Cell.ToString();
        }

        public override int GetHashCode()
        {
            return Gen.HashCombineInt(mapSizeX, Gen.HashCombineInt(Index, mapSizeZ));
        }

        public override bool Equals(object obj)
        {
            var @ref = obj as CellRef;
            return @ref != null &&
                   mapSizeX == @ref.mapSizeX &&
                   mapSizeZ == @ref.mapSizeZ &&
                   Index == @ref.Index;
        }
    }

    public static class CellRefUtils
    {
        public static CellRef GetCellRef(this Map map, int index)
        {
            return new CellRef(map.Size.x, map.Size.z, index);
        }

        public static CellRef GetCellRef(this Map map, IntVec3 cell)
        {
            return new CellRef(map.Size.x, map.Size.z, cell);
        }

        public static CellRef GetCellRef(this Map map, int x, int z)
        {
            return new CellRef(map.Size.x, map.Size.z, new IntVec3(x, 0, z));
        }
    }
}
