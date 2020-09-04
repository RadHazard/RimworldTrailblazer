# TODO list:

- Optimize the pathfinders
- Write unit tests using RimTest
  - Write tests for each rule
  - Write tests for each pathfinder
- Cache passibility/cost for cells?
  - At the moment, both seem to be only minor contributors to the full pathfinding cost
  - Using the faster priority queue means they're relatively much more costly now
- Expand debug visualizations
  - Show costs for individual rules
  - Pathfinder-specific visualization
  - Add settings to the setting panel (or make a debug setting panel)
- Profiling
  - Profile all the rules
  - Add detailed profiler to pathfinders
- Figure out why settings don't save
- Fix TwinAStar sometimes being unable to reach cells (door passability check?)
