# TODO list:

- Debug the HAStar pathfinder
  - NPE from somewhere -- only when colonists aren't drafted
  - Why is it slow?
    - The heuristic is *cheating* -- it costs nothing to path down a long span, so costs are way too low
    - Try pathing using span centers instead?  Note -- check admissibility
- Write unit tests using RimTest
  - Write tests for each rule
  - Write tests for each pathfinder
- Make ruleset properly extensible somehow
- Add a setting to swap between pathfinders
- Expand debug visualizations
  - Show costs for individual rules
  - Pathfinder-specific visualizaiton
  - "Instant replay" feature to record and playback scanned cells
- Profile various rules to make sure we're running efficiently
