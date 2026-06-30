using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SoundManager))]
public class SoundManagerEditor : Editor
{
    private const string PrefabPath = "Assets/Sounds/SFX/SoundManager.prefab";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8);

        if (Application.isPlaying)
        {
            GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f);
            if (GUILayout.Button("Save Play-Mode Values to Prefab", GUILayout.Height(32)))
                SaveToPrefab();
            GUI.backgroundColor = Color.white;
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Enter Play mode to tune values live, then click 'Save Play-Mode Values to Prefab' to persist them.",
                MessageType.Info);
        }
    }

    private void SaveToPrefab()
    {
        var live = target as SoundManager;
        if (live == null) return;

        // Try the prefab asset the instance came from first; fall back to the known path.
        var prefabRoot = PrefabUtility.GetCorrespondingObjectFromSource(live.gameObject) as GameObject;
        if (prefabRoot == null)
            prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

        if (prefabRoot == null)
        {
            Debug.LogError($"[SoundManagerEditor] Could not locate prefab at '{PrefabPath}'.");
            return;
        }

        var prefabComponent = prefabRoot.GetComponent<SoundManager>();
        if (prefabComponent == null)
        {
            Debug.LogError("[SoundManagerEditor] SoundManager component not found on prefab.");
            return;
        }

        string json = EditorJsonUtility.ToJson(live);
        EditorJsonUtility.FromJsonOverwrite(json, prefabComponent);

        EditorUtility.SetDirty(prefabRoot);
        PrefabUtility.SavePrefabAsset(prefabRoot);

        Debug.Log($"[SoundManagerEditor] Saved play-mode values to '{PrefabPath}'.");
    }
}
