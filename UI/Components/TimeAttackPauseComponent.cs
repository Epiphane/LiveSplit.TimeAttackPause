using System;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using IrcDotNet.Collections;
using LiveSplit.Model;
using LiveSplit.TimeAttackPause.UI.Components;

namespace LiveSplit.UI.Components
{
    public class TimeAttackPauseComponent : LogicComponent
    {
        private const string kComparisonName = "TimeAttackPause_Ongoing";
        private TimeAttackPauseSettings Settings { get; set; }

        private ITimerModel Model { get; set; }

        // This object contains all of the current information about the splits, the timer, etc.
        private LiveSplitState CurrentState { get; set; }

        public override string ComponentName => "TimeAttackPause";

        // This function is called when LiveSplit creates your component. This happens when the
        // component is added to the layout, or when LiveSplit opens a layout with this component
        // already added.
        public TimeAttackPauseComponent(LiveSplitState state)
        {
            Settings = new TimeAttackPauseSettings();

            CurrentState = state;
            if (!CurrentState.Run.CustomComparisons.Contains(kComparisonName))
            {
                CurrentState.Run.CustomComparisons.Add(kComparisonName);
            }

            CurrentState.OnReset += state_OnReset;

            Model = new TimerModel() { CurrentState = state };
            InitializeOngoingRun();
        }

        private void InitializeOngoingRun()
        {
            if (CurrentState.Run.First().Comparisons.TryGetValue(kComparisonName, out Model.Time firstSplitTime))
            {
                if (firstSplitTime[CurrentState.CurrentTimingMethod] == null)
                {
                    return;
                }

                // Send a start to issue notifications, then fix state.
                Model.Start();

                foreach (ISegment segment in CurrentState.Run)
                {
                    if (segment.Comparisons.ContainsKey(kComparisonName))
                    {
                        segment.SplitTime = segment.Comparisons[kComparisonName];
                    }

                    if (segment.SplitTime[CurrentState.CurrentTimingMethod] != null)
                    {
                        CurrentState.CurrentSplitIndex++;
                        CurrentState.AdjustedStartTime = TimeStamp.Now - segment.SplitTime[CurrentState.CurrentTimingMethod].GetValueOrDefault(TimeSpan.Zero);
                    }
                }

                CurrentState.Run.AttemptCount--;
                CurrentState.IsGameTimeInitialized = true;
                CurrentState.Run.HasChanged = false;

                Model.Pause();
            }
        }

        public override Control GetSettingsControl(LayoutMode mode)
        {
            Settings.Mode = mode;
            return Settings;
        }

        public override XmlNode GetSettings(XmlDocument document)
        {
            return Settings.GetSettings(document);
        }

        public override void SetSettings(XmlNode settings)
        {
            Settings.SetSettings(settings);
        }

        // This is the function where we decide what needs to be displayed at this moment in time,
        // and tell the internal component to display it. This function is called hundreds to
        // thousands of times per second.
        public override void Update(IInvalidator invalidator, LiveSplitState state, float width, float height,
            LayoutMode mode)
        {
            CurrentState = state;

            switch (CurrentState.CurrentPhase)
            {
                case TimerPhase.Running:
                case TimerPhase.Paused:
                    CurrentState.CurrentSplit.Comparisons["TimeAttackPause_Ongoing"] = CurrentState.CurrentTime;
                    break;
            }
        }

        private void state_OnReset(object sender, TimerPhase value)
        {
            CurrentState.Run.ForEach((ISegment segment) => {
                segment.Comparisons.Remove(kComparisonName);
            });
            CurrentState.Run.HasChanged = true;
        }

        public override void Dispose()
        {
            CurrentState.OnReset -= state_OnReset;
        }
    }
}