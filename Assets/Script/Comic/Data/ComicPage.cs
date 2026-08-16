using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Comic
{
    [Serializable]
    public class ComicPage
    {
        public string name = "Page";
        public Color background = Color.black;

        [Tooltip("How this page replaces the previous one. Ignored on the first page of a sequence (always Cut).")]
        public ComicTransitionKind enterTransition = ComicTransitionKind.FadeToBlack;
        [Min(0f)] public float transitionDuration = 0.4f;

        public List<ComicPanel> panels = new List<ComicPanel>();
        public List<ComicBeatEvent> beatEvents = new List<ComicBeatEvent>();

        /// <summary>Highest beatIndex referenced anywhere on the page — the last beat Advance can reach.</summary>
        public int MaxBeatIndex()
        {
            int max = 0;
            for (int i = 0; i < panels.Count; i++)
            {
                var p = panels[i];
                if (p.beatIndex > max) max = p.beatIndex;
                for (int j = 0; j < p.layers.Count; j++)
                    if (p.layers[j].beatIndex > max) max = p.layers[j].beatIndex;
            }
            for (int i = 0; i < beatEvents.Count; i++)
                if (beatEvents[i].beatIndex > max) max = beatEvents[i].beatIndex;
            return max;
        }
    }
}
