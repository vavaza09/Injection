using System.Collections.Generic;
using UnityEngine;

namespace Game.Comic
{
    /// <summary>
    /// One authored comic (one or more pages). Pages are embedded rather than split into
    /// separate assets — a single merge unit for git and a single thing for the Comic Editor
    /// window to open, mirroring how <c>RoomCatalog</c> embeds its <c>RoomDefinition</c> rows.
    /// </summary>
    [CreateAssetMenu(fileName = "ComicSequence", menuName = "Injection/Comic Sequence")]
    public class ComicSequenceAsset : ScriptableObject
    {
        [Tooltip("Stable key for SaveData.comicsSeen. Must be unique across every comic in the game.")]
        [SerializeField] private string sequenceId;

        [SerializeField] private ComicStyle style = new ComicStyle();

        [Header("Scene Audio")]
        [Tooltip("Silence the scene's background music, ambient bed and looping SFX while this comic " +
                 "plays, so only the comic's own beat-event audio is heard. Untick for a comic meant " +
                 "to play over whatever the room is already playing.")]
        [SerializeField] private bool silenceSceneAudio = true;

        [Tooltip("Seconds to fade the scene music out when this comic starts. 0 = cut instantly — " +
                 "which is what you want for a comic that plays on room entry, where the scene music " +
                 "is only a frame old and a fade is just a second of collision.")]
        [Min(0f)][SerializeField] private float sceneAudioFadeOut = 0f;

        [Tooltip("Bring the scene's music/ambient back when this comic ends. Untick when this comic's " +
                 "own final music beat is meant to carry on into gameplay.")]
        [SerializeField] private bool restoreSceneAudioOnFinish = true;

        [SerializeField] private List<ComicPage> pages = new List<ComicPage>();

        public string SequenceId => sequenceId;
        public ComicStyle Style => style;
        public IReadOnlyList<ComicPage> Pages => pages;
        public int PageCount => pages.Count;

        /// <summary>See the field tooltips — these three are the comic's scene-audio policy, applied
        /// by <c>ComicPlayer</c> via <c>SoundManager.SuspendSceneAudio</c>/<c>RestoreSceneAudio</c>.</summary>
        public bool SilenceSceneAudio => silenceSceneAudio;
        public float SceneAudioFadeOut => sceneAudioFadeOut;
        public bool RestoreSceneAudioOnFinish => restoreSceneAudioOnFinish;

        public ComicPage GetPage(int index) => (index >= 0 && index < pages.Count) ? pages[index] : null;

#if UNITY_EDITOR
        /// <summary>Editor-only mutation surface for ComicEditorWindow. Runtime never edits sequence data.</summary>
        public List<ComicPage> EditablePages => pages;

        public void SetSequenceId(string id)
        {
            sequenceId = id;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        public void MarkDirty() => UnityEditor.EditorUtility.SetDirty(this);

        /// <summary>Editor-only mutation surface for ComicEditorWindow's Scene Audio foldout —
        /// written as one call rather than three setters because the window edits the three as a
        /// single change-check block.</summary>
        public void SetSceneAudioPolicy(bool silence, float fadeOut, bool restore)
        {
            silenceSceneAudio = silence;
            sceneAudioFadeOut = Mathf.Max(0f, fadeOut);
            restoreSceneAudioOnFinish = restore;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
