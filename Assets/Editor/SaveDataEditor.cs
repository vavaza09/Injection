#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using Game.Persistence;

/// <summary>
/// Editor window for inspecting and deleting the player save file without leaving Unity.
/// Open via Tools > Save Data.
/// </summary>
public class SaveDataEditor : EditorWindow
{
    private const string FileName = "save.json";

    private string SavePath => Path.Combine(Application.persistentDataPath, FileName);

    private SaveData _loaded;
    private string   _rawJson;
    private Vector2  _scroll;

    [MenuItem("Tools/Save Data")]
    public static void Open() => GetWindow<SaveDataEditor>("Save Data").minSize = new Vector2(360, 280);

    private void OnFocus() => Refresh();

    private void Refresh()
    {
        _loaded  = null;
        _rawJson = null;

        if (!File.Exists(SavePath)) return;

        _rawJson = File.ReadAllText(SavePath);
        try { _loaded = JsonUtility.FromJson<SaveData>(_rawJson); }
        catch { _loaded = null; }
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Save File", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(SavePath, EditorStyles.miniLabel);
        EditorGUILayout.Space(4);

        bool exists = File.Exists(SavePath);

        using (new EditorGUI.DisabledScope(!exists))
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Delete Save", GUILayout.Height(28)))
            {
                if (EditorUtility.DisplayDialog("Delete Save",
                    "ลบ save file ถาวร ไม่สามารถ undo ได้ ยืนยันหรือไม่?",
                    "ลบ", "ยกเลิก"))
                {
                    File.Delete(SavePath);
                    Debug.Log("[SaveDataEditor] Save file deleted.");
                    Refresh();
                }
            }

            if (GUILayout.Button("Open Folder", GUILayout.Height(28)))
                EditorUtility.RevealInFinder(SavePath);

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(8);

        if (!exists)
        {
            EditorGUILayout.HelpBox("ไม่พบ save file — เกมยังไม่เคย save หรือถูกลบไปแล้ว", MessageType.Info);
            return;
        }

        if (_loaded == null)
        {
            EditorGUILayout.HelpBox("อ่าน save file ไม่ได้ (JSON เสียหาย)", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.LabelField("Contents", EditorStyles.boldLabel);

            using (new EditorGUI.IndentLevelScope(1))
            {
                EditorGUILayout.LabelField("Version",     _loaded.version.ToString());
                EditorGUILayout.LabelField("Last Room",   FormatAnchor(_loaded.lastRoom?.roomId, _loaded.lastRoom?.spawnPointId));
                EditorGUILayout.LabelField("Checkpoint",  FormatAnchor(_loaded.checkpoint?.roomId, _loaded.checkpoint?.spawnPointId));

                int bossCount = _loaded.bossesDefeated?.Count ?? 0;
                EditorGUILayout.LabelField("Bosses Defeated", bossCount == 0 ? "none" : string.Join(", ", _loaded.bossesDefeated));

                int objCount = _loaded.objectivesCompleted?.Count ?? 0;
                EditorGUILayout.LabelField("Objectives Done", objCount == 0 ? "none" : string.Join(", ", _loaded.objectivesCompleted));
            }
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Raw JSON", EditorStyles.boldLabel);

        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
        EditorGUILayout.TextArea(_rawJson ?? "", GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(4);
        if (GUILayout.Button("Refresh"))
            Refresh();
    }

    private static string FormatAnchor(string roomId, string spawnId)
    {
        if (string.IsNullOrEmpty(roomId)) return "(empty)";
        return $"{roomId} / {spawnId}";
    }
}
#endif
