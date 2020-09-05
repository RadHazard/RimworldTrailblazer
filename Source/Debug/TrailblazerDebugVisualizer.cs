using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Trailblazer.Debug
{
    public class TrailblazerDebugVisualizer : MapComponent
    {
        internal class ReplayFrame
        {
            internal readonly HashSet<int> cells = new HashSet<int>();
            internal readonly HashSet<Pair<CellRef, CellRef>> lines = new HashSet<Pair<CellRef, CellRef>>();

            internal ReplayFrame() { }

            internal void DrawCell(CellRef cellRef)
            {
                cells.Add(cellRef.Index);
            }

            internal void DrawLine(CellRef a, CellRef b)
            {
                lines.Add(new Pair<CellRef, CellRef>(a, b));
            }
        }

        public class InstantReplay
        {
            internal readonly Map map;
            internal readonly List<ReplayFrame> frames;
            private ReplayFrame currentFrame;

            internal InstantReplay(Map map)
            {
                this.map = map;
                frames = new List<ReplayFrame>();
                NextFrame();
            }

            /// <summary>
            /// Finalizes the current replay frame and starts a new one.
            /// </summary>
            public void NextFrame()
            {
                currentFrame = new ReplayFrame();
                frames.Add(currentFrame);
            }

            /// <summary>
            /// Draws the given cell in the current replay frame.  Drawing the same cell multiple times in a frame
            /// does nothing.
            /// </summary>
            /// <param name="cellRef">Cell ref.</param>
            public void DrawCell(CellRef cellRef)
            {
                currentFrame.DrawCell(cellRef);
            }

            /// <summary>
            /// Draws the given line in the current replay frame.  Drawing the same line multiple times in a frame
            /// does nothing.
            /// </summary>
            /// <param name="a">First point.</param>
            /// <param name="b">Second point.</param>
            public void DrawLine(CellRef a, CellRef b)
            {
                currentFrame.DrawLine(a, b);
            }
        }

        internal class ReplayCellBoolGiver : ICellBoolGiver
        {
            private readonly Func<int, bool> drawCell;

            public Color Color { get; }

            internal ReplayCellBoolGiver(Func<int, bool> drawCell, Color color)
            {
                Color = color;
                this.drawCell = drawCell;
            }

            public bool GetCellBool(int index)
            {
                return drawCell.Invoke(index);
            }

            public Color GetCellExtraColor(int index)
            {
                return Color.white;
            }
        }

        internal class ReplayDrawer
        {
            private readonly HashSet<int> cells = new HashSet<int>();
            private readonly HashSet<Pair<CellRef, CellRef>> lines = new HashSet<Pair<CellRef, CellRef>>();

            private HashSet<int> frameCells = new HashSet<int>();
            private HashSet<Pair<CellRef, CellRef>> frameLines = new HashSet<Pair<CellRef, CellRef>>();

            private readonly CellBoolDrawer oldCellDrawer;
            private readonly CellBoolDrawer frameCellDrawer;

            private readonly InstantReplay replay;
            private readonly Color color;
            private readonly int framesPerTick;
            private int frame;

            public bool Done { get; private set; }
            public int finishTick;

            private const int finishWithinTicks = 900; // 15 seconds

            private static readonly Color[] colors =
            {
                Color.red,
                Color.yellow,
                Color.green,
                Color.cyan,
                Color.blue,
                Color.magenta
            };
            private static ushort colorIndex = 0;

            internal ReplayDrawer(InstantReplay replay)
            {
                this.replay = replay;
                color = colors[colorIndex % colors.Length];
                colorIndex++;
                frame = 0;
                framesPerTick = Math.Max(replay.frames.Count / finishWithinTicks, 5);

                ReplayFrame firstFrame = replay.frames[0];
                frameCells = firstFrame.cells;
                frameLines = firstFrame.lines;

                ICellBoolGiver oldCellGiver = new ReplayCellBoolGiver(cells.Contains, color);
                oldCellDrawer = new CellBoolDrawer(oldCellGiver, replay.map.Size.x, replay.map.Size.z, 0.10f);

                ICellBoolGiver frameCellGiver = new ReplayCellBoolGiver(c => frameCells.Contains(c), color);
                frameCellDrawer = new CellBoolDrawer(frameCellGiver, replay.map.Size.x, replay.map.Size.z, 0.66f);
            }

            internal void Tick()
            {
                if (!Done)
                {
                    cells.AddRange(frameCells);
                    lines.AddRange(frameLines);

                    frameCells.Clear();
                    frameLines.Clear();


                    if (frame >= replay.frames.Count)
                    {
                        Done = true;
                        finishTick = Find.TickManager.TicksGame;
                    }
                    else
                    {
                        for (int i = 0; i < framesPerTick && frame < replay.frames.Count; i++)
                        {
                            ReplayFrame nextFrame = replay.frames[frame];
                            frameCells.AddRange(nextFrame.cells);
                            frameLines.AddRange(nextFrame.lines);
                            frame++;
                        }
                    }

                    oldCellDrawer.SetDirty();
                    frameCellDrawer.SetDirty();
                }
            }

            internal void Update()
            {
                oldCellDrawer.MarkForDraw();
                oldCellDrawer.CellBoolDrawerUpdate();

                frameCellDrawer.MarkForDraw();
                frameCellDrawer.CellBoolDrawerUpdate();

                foreach (Pair<CellRef, CellRef> line in lines)
                {
                    Vector3 vecA = line.First.Cell.ToVector3Shifted();
                    Vector3 vecB = line.Second.Cell.ToVector3Shifted();
                    GenDraw.DrawLineBetween(vecA, vecB, SimpleColor.White);
                }

                foreach (Pair<CellRef, CellRef> line in frameLines)
                {
                    Vector3 vecA = line.First.Cell.ToVector3Shifted();
                    Vector3 vecB = line.Second.Cell.ToVector3Shifted();
                    GenDraw.DrawLineBetween(vecA, vecB, SimpleColor.Yellow);
                }
            }
        }

        private readonly List<ReplayDrawer> replays = new List<ReplayDrawer>();

        // TODO - make these settings
        private const int FrameRate = 1;
        private const int HoldFrames = 60;

        public TrailblazerDebugVisualizer(Map map) : base(map) { }

        public InstantReplay CreateNewReplay()
        {
            return new InstantReplay(map);
        }

        public void RegisterReplay(InstantReplay replay)
        {
            if (replay.map != map)
            {
                Log.Error("[Trailblazer] Tried to register a replay from a different map");
            }
            else
            {
                if (DebugViewSettings.drawPaths)
                {
                    replays.Add(new ReplayDrawer(replay));
                }
            }
        }

        public override void MapComponentTick()
        {
            int currTick = Find.TickManager.TicksGame;
            replays.RemoveAll(d => d.Done && d.finishTick + HoldFrames <= currTick);
        }

        public override void MapComponentUpdate()
        {
            if (DebugViewSettings.drawPaths)
            {
                foreach (ReplayDrawer replayDrawer in replays)
                {
                    replayDrawer.Tick();
                    replayDrawer.Update();
                }
            }
            else
            {
                replays.Clear();
            }
        }
    }
}
