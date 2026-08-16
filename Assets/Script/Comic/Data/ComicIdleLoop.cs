using System;
using UnityEngine;

namespace Game.Comic
{
    /// <summary>Small looping motion applied on top of a layer's settled layout position —
    /// keeps a fully-revealed panel from looking dead while the player reads. Runs continuously
    /// once the entrance tween finishes; does not affect layout or hit-testing.</summary>
    [Serializable]
    public class ComicIdleLoop
    {
        public IdleKind kind = IdleKind.None;

        [Tooltip("Drift/Bob: pixel amplitude (x, y). Pan: pixels panned side to side.")]
        public Vector2 amplitude = new Vector2(6f, 6f);

        [Tooltip("Seconds for one full cycle.")]
        [Min(0.01f)] public float period = 3f;

        [Tooltip("Pulse/Zoom: extra scale added at the peak of the cycle (0.06 = +6%).")]
        public float zoomAmount = 0.06f;
    }
}
