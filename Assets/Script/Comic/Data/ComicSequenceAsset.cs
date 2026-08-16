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
        [SerializeField] private List<ComicPage> pages = new List<ComicPage>();

        public string SequenceId => sequenceId;
        public ComicStyle Style => style;
        public IReadOnlyList<ComicPage> Pages => pages;
        public int PageCount => pages.Count;

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
#endif
    }
}
