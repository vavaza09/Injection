#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class KikiSetup
{
    [MenuItem("Tools/Setup Kiki Enemy")]
    public static void Run()
    {
        CreateAnimations();
        UpdatePrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[KikiSetup] Done — animations, controller, and prefab updated.");
    }

    static void CreateAnimations()
    {
        const string dir = "Assets/Enemy/Enemy_sprite/Enemies/";

        Sprite[] idle   = LoadSprites(dir + "Float Idle Sprite Sheet.png");
        Sprite[] attack = LoadSprites(dir + "Float Attack Sprite Sheet.png");

        if (idle.Length == 0)   { Debug.LogError("[KikiSetup] No sprites in Float Idle Sprite Sheet"); return; }
        if (attack.Length == 0) { Debug.LogError("[KikiSetup] No sprites in Float Attack Sprite Sheet"); return; }

        CreateClip(idle,   dir + "Float_Idle.anim",   fps: 8f,  loop: true);
        CreateClip(attack, dir + "Float_Attack.anim", fps: 10f, loop: false);

        Debug.Log($"[KikiSetup] Float_Idle ({idle.Length} frames) and Float_Attack ({attack.Length} frames) created.");

        string ctrlPath = dir + "Kiki_Anim.controller";
        AssetDatabase.DeleteAsset(ctrlPath);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        var sm   = ctrl.layers[0].stateMachine;

        var idleClip   = AssetDatabase.LoadAssetAtPath<AnimationClip>(dir + "Float_Idle.anim");
        var attackClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(dir + "Float_Attack.anim");

        var idleState   = sm.AddState("Float_Idle");   idleState.motion   = idleClip;
        var attackState = sm.AddState("Float_Attack"); attackState.motion = attackClip;
        sm.defaultState = idleState;

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
    }

    static Sprite[] LoadSprites(string path) =>
        AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<Sprite>()
            .OrderBy(s => s.name)
            .ToArray();

    static void CreateClip(Sprite[] sprites, string path, float fps, bool loop)
    {
        AssetDatabase.DeleteAsset(path);

        var clip = new AnimationClip { frameRate = fps };

        if (loop)
        {
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        var binding = new EditorCurveBinding
        {
            type         = typeof(SpriteRenderer),
            path         = "",
            propertyName = "m_Sprite"
        };

        var keys = sprites
            .Select((s, i) => new ObjectReferenceKeyframe { time = i / fps, value = s })
            .ToArray();

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        AssetDatabase.CreateAsset(clip, path);
    }

    static void UpdatePrefab()
    {
        const string prefabPath = "Assets/Enemy/Enemy_prefabs/Kiki.prefab";
        const string ctrlPath   = "Assets/Enemy/Enemy_sprite/Enemies/Kiki_Anim.controller";

        var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ctrlPath);
        if (ctrl == null) { Debug.LogError("[KikiSetup] Controller not found — animation step may have failed"); return; }

        using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
        {
            var root = scope.prefabContentsRoot;
            if (root == null) { Debug.LogError("[KikiSetup] Kiki prefab not found"); return; }

            // Remove EnemyAI — KikiEnemy manages its own state machine
            var ai = root.GetComponent<EnemyAI>();
            if (ai != null)
            {
                Object.DestroyImmediate(ai);
                Debug.Log("[KikiSetup] Removed EnemyAI from Kiki prefab.");
            }

            // Disable gravity so Kiki floats
            var rb = root.GetComponent<Rigidbody2D>();
            if (rb != null) rb.gravityScale = 0f;

            // Add Animator with controller
            var anim = root.GetComponent<Animator>();
            if (anim == null) anim = root.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
        }

        Debug.Log("[KikiSetup] Kiki prefab updated: EnemyAI removed, gravity=0, Animator assigned.");
    }
}
#endif
