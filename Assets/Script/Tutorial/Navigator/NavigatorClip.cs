using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Tutorial.Navigator
{
    /// <summary>
    /// Recorded playback data for one tutorial step demonstration.
    /// Frames are relative to the step's navigator anchor (frame-0 = anchor position).
    /// </summary>
    [CreateAssetMenu(fileName = "NavigatorClip", menuName = "Tutorial/Navigator Clip")]
    public class NavigatorClip : ScriptableObject
    {
        [Serializable]
        public struct Frame
        {
            [Tooltip("Time in seconds from start of clip (unscaled).")]
            public float time;
            [Tooltip("Position relative to the step's navigator anchor.")]
            public Vector2 localPos;
            [Tooltip("Sprite displayed on this frame.")]
            public Sprite sprite;
            [Tooltip("+1 = facing right, -1 = facing left.")]
            public float flipX;
        }

        [SerializeField] private List<Frame> frames = new List<Frame>();

        public IReadOnlyList<Frame> Frames => frames;
        public float Duration => frames.Count > 0 ? frames[frames.Count - 1].time : 0f;
        public bool IsEmpty => frames.Count == 0;

#if UNITY_EDITOR
        public void SetFrames(List<Frame> recorded)
        {
            frames = recorded;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
