using Verse;
using UnityEngine;
using System.Collections.Generic;

namespace Trailblazer
{

    public class TrailblazerSettingController : Mod
    {

        private struct RadioButton<T>
        {
            public readonly string labelKey;
            public readonly T value;
            public readonly string tooltipKey;

            public TaggedString Label => labelKey.Translate();
            public TaggedString Tooltip => tooltipKey?.Translate() ?? null;

            public RadioButton(string labelKey, T value, string tooltipKey = null)
            {
                this.labelKey = labelKey;
                this.value = value;
                this.tooltipKey = tooltipKey;
            }
        }

        public TrailblazerSettingController(ModContentPack content) : base(content) { }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

            // Pathfinder selection
            listingStandard.Label("TrailblazerPathfinder".Translate());
            var radioButtons = new RadioButton<PathfinderEnum>[]
            {
                new RadioButton<PathfinderEnum>("TrailblazerAStar", PathfinderEnum.AStar, "TrailblazerAStarDesc"),
                new RadioButton<PathfinderEnum>("TrailblazerHAStar", PathfinderEnum.HAStar, "TrailblazerHAStarDesc"),
                new RadioButton<PathfinderEnum>("TrailblazerTwinAStar", PathfinderEnum.TwinAStar, "TrailblazerTwinAStarDesc")
            };
            AddRadioButtons(listingStandard, radioButtons, ref TrailblazerSettings.pathfinder);

            listingStandard.End();
            base.DoSettingsWindowContents(inRect);
        }

        private void AddRadioButtons<T>(Listing_Standard listingStandard, IEnumerable<RadioButton<T>> buttons, ref T selectedVal)
        {
            foreach (RadioButton<T> button in buttons)
            {
                bool selected = EqualityComparer<T>.Default.Equals(button.value, selectedVal);
                if (listingStandard.RadioButton_NewTemp(button.Label, selected, tooltip: button.Tooltip))
                    selectedVal = button.value;
            }
        }

        public override string SettingsCategory()
        {
            return "TrailblazerModName".Translate();
        }
    }

    public class TrailblazerSettings : ModSettings
    {
        public static PathfinderEnum pathfinder;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref pathfinder, "pathfinder");
            base.ExposeData();
        }
    }

    public enum PathfinderEnum
    {
        AStar,
        HAStar,
        TwinAStar
    }
}
