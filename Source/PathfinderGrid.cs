using System;
using System.Collections.Generic;
using System.Linq;
using Trailblazer.Rules;

namespace Trailblazer
{
    /// <summary>
    /// A working grid for pathfinders.  The grid allows calculated stats such as a cell's full cost or passability
    /// info to be cached.
    /// 
    /// Note:  As of now, only cell-based rules are cached.  Advanced rules that require the full move data are not.
    /// </summary>
    public class PathfinderGrid
    {
        private readonly Dictionary<CellRef, int> cellCostCache = new Dictionary<CellRef, int>();
        private readonly Dictionary<CellRef, bool> cellPassabilityCach = new Dictionary<CellRef, bool>();

        private readonly List<CellCostRule> cellCostRules;
        private readonly List<CostRule> costRules;
        private readonly List<CellPassabilityRule> cellPassabilityRules;
        private readonly List<PassabilityRule> passabilityRules;

        public PathfinderGrid(IEnumerable<CellCostRule> cellCostRules, IEnumerable<CostRule> costRules,
            IEnumerable<CellPassabilityRule> cellPassabilityRules, IEnumerable<PassabilityRule> passabilityRules)
        {
            this.cellCostRules = new List<CellCostRule>(cellCostRules);
            this.costRules = new List<CostRule>(costRules);
            this.cellPassabilityRules = new List<CellPassabilityRule>(cellPassabilityRules);
            this.passabilityRules = new List<PassabilityRule>(passabilityRules);
        }

        public bool MoveIsValid(MoveData moveData)
        {
            if (!CellIsPassable(moveData.cell))
                return false;
            return passabilityRules.All(r => r.IsPassable(moveData));
        }

        public int MoveCost(MoveData moveData)
        {
            int cost = CellCost(moveData.cell) + costRules.Sum(r => r.GetCost(moveData));
            // Ensure cost is never less than zero
            return Math.Max(0, cost);
        }

        private bool CellIsPassable(CellRef cell)
        {
            if (!cellPassabilityCach.ContainsKey(cell))
            {
                cellPassabilityCach[cell] = cellPassabilityRules.All(r => r.IsPassable(cell));
            }
            return cellPassabilityCach[cell];
        }

        private int CellCost(CellRef cell)
        {
            if (!cellCostCache.ContainsKey(cell))
            {
                cellCostCache[cell] = cellCostRules.Sum(r => r.GetCost(cell));
            }
            return cellCostCache[cell];
        }
    }
}
