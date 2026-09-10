using System;
using System.Collections.Generic;

namespace Vkaike2.StarterKit.Components.Animations
{
    public partial class CustomAnimator2d
    {
        public class ClipEvents
        {
            private static readonly Dictionary<int, Action> NoFrameEvents = new();

            public string ClipName { get; }
            public IReadOnlyDictionary<int, Action> ByFrame { get; }
            public Action Finished { get; }

            public ClipEvents(string clipName, Dictionary<int, Action> byFrame = null, Action finished = null)
            {
                ClipName = clipName;
                ByFrame = byFrame ?? NoFrameEvents;
                Finished = finished;
            }
        }
    }
}
