using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Trailblazer
{
    /// <summary>
    /// All the data associated with a pathfinding job
    /// </summary>
    public class PathfindData
    {
        public readonly Map map;
        public readonly CellRef start;
        public readonly LocalTargetInfo dest;
        public readonly TraverseParms traverseParms;
        public readonly PathEndMode pathEndMode;

        private CellRect? _destRect;
        public CellRect DestRect
        {
            get
            {
                if (_destRect == null)
                {
                    _destRect = CalculateDestinationRect();
                }
                return (CellRect)_destRect;
            }
        }

        private List<CellRef> _disallowedCorners;
        public List<CellRef> DisallowedCorners
        {
            get
            {
                if (_disallowedCorners == null)
                {
                    _disallowedCorners = CalculateDisallowedCorners(DestRect);
                }
                return _disallowedCorners;
            }
        }

        public PathfindData(Map map, CellRef start, LocalTargetInfo dest, TraverseParms traverseParms, PathEndMode pathEndMode)
        {
            this.map = map;
            this.start = start;
            this.dest = dest;
            this.traverseParms = traverseParms;
            this.pathEndMode = pathEndMode;
        }

        public bool CellIsInDestination(CellRef cell)
        {
            return DestRect.Contains(cell) && !DisallowedCorners.Contains(cell);
        }

        private CellRect CalculateDestinationRect()
        {
            CellRect result;
            if (dest.HasThing && pathEndMode != PathEndMode.OnCell)
            {
                result = dest.Thing.OccupiedRect();
            }
            else
            {
                result = CellRect.SingleCell(dest.Cell);
            }

            if (pathEndMode == PathEndMode.Touch)
            {
                result = result.ExpandedBy(1);
            }
            return result;
        }

        private List<CellRef> CalculateDisallowedCorners(CellRect destinationRect)
        {
            List<CellRef> disallowedCornerIndices = new List<CellRef>(4);
            if (pathEndMode == PathEndMode.Touch)
            {
                int minX = destinationRect.minX;
                int minZ = destinationRect.minZ;
                int maxX = destinationRect.maxX;
                int maxZ = destinationRect.maxZ;
                if (!IsCornerTouchAllowed(minX + 1, minZ + 1, minX + 1, minZ, minX, minZ + 1))
                {
                    disallowedCornerIndices.Add(map.GetCellRef(minX, minZ));
                }
                if (!IsCornerTouchAllowed(minX + 1, maxZ - 1, minX + 1, maxZ, minX, maxZ - 1))
                {
                    disallowedCornerIndices.Add(map.GetCellRef(minX, maxZ));
                }
                if (!IsCornerTouchAllowed(maxX - 1, maxZ - 1, maxX - 1, maxZ, maxX, maxZ - 1))
                {
                    disallowedCornerIndices.Add(map.GetCellRef(maxX, maxZ));
                }
                if (!IsCornerTouchAllowed(maxX - 1, minZ + 1, maxX - 1, minZ, maxX, minZ + 1))
                {
                    disallowedCornerIndices.Add(map.GetCellRef(maxX, minZ));
                }
            }
            return disallowedCornerIndices;
        }

        private bool IsCornerTouchAllowed(int cornerX, int cornerZ, int adjCardinal1X, int adjCardinal1Z, int adjCardinal2X, int adjCardinal2Z)
        {
            return TouchPathEndModeUtility.IsCornerTouchAllowed(cornerX, cornerZ, adjCardinal1X, adjCardinal1Z, adjCardinal2X, adjCardinal2Z, map);
        }
    }
}
