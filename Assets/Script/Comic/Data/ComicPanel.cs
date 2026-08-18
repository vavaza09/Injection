using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Comic
{
    /// <summary>One panel on a page. Panels coexist in the layout at all times; <see cref="beatIndex"/>
    /// only controls when this panel's own entrance plays and it starts drawing.</summary>
    [Serializable]
    public class ComicPanel
    {
        public string name = "Panel";

        [Tooltip("Layout rect in the page's 1920x1080 reference space, origin at bottom-left.")]
        public Rect rect = new Rect(0, 0, 900, 500);
        public float rotation;

        [Tooltip("Normalized rotation/scale origin within Rect (0,0 = bottom-left, 0.5,0.5 = center, 1,1 = top-right). Purely a build-time transform origin — does not affect Rect's authored bounds.")]
        public Vector2 pivot = new Vector2(0.5f, 0.5f);

        [Tooltip("Silhouette the panel content is clipped to (uGUI Mask). Null = plain rectangle.")]
        public Sprite maskShape;
        public bool drawBorder = true;

        [Tooltip("Beat at which this panel becomes visible.")]
        public int beatIndex;

        [Tooltip("Beat at which this panel plays its own Exit tween and is removed from view, " +
            "independent of focus. -1 = never — the panel just stays on screen, dimmed whenever " +
            "a later panel takes focus (the default behavior).")]
        public int exitBeatIndex = -1;

        public ComicTween entrance = ComicTween.DefaultEntrance();
        public ComicTween exit = ComicTween.DefaultExit();

        [Tooltip("Layers drawn back to front.")]
        public List<ComicLayer> layers = new List<ComicLayer>();
    }
}
