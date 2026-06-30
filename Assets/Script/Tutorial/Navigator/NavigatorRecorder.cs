using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Tutorial.Navigator
{
    /// <summary>
    /// Editor-only recorder. Place in the tutorial scene, wire Player's SpriteRenderer and
    /// the step's navigatorAnchor. Press RecordKey to start/stop. Writes a NavigatorClip asset.
    ///
    /// Only active in Editor — stripped from builds via #if UNITY_EDITOR guards on the save path.
    /// </summary>
    public class NavigatorRecorder : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("SpriteRenderer of the Player character to record.")]
        [SerializeField] private SpriteRenderer playerSprite;
        [Tooltip("The navigatorAnchor Transform of the step being recorded.")]
        [SerializeField] private Transform anchor;

        [Header("Settings")]
        [SerializeField] private Key recordKey = Key.F9;
        [Tooltip("Asset path relative to Assets/ (no extension). E.g. Settings/NavigatorClips/Walk")]
        [SerializeField] private string saveAssetPath = "Settings/NavigatorClips/NewClip";
        [Tooltip("Frames per second to sample (lower = smaller asset, still looks smooth with lerp).")]
        [SerializeField] private float sampleRate = 30f;

        private bool _recording;
        private float _recordStartTime;
        private float _nextSampleTime;
        private List<NavigatorClip.Frame> _frames = new();
        private Transform _playerTransform;

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current[recordKey].wasPressedThisFrame)
            {
                if (!_recording) StartRecording();
                else StopAndSave();
            }

            if (_recording && Time.unscaledTime >= _nextSampleTime)
            {
                CaptureFrame();
                _nextSampleTime += 1f / sampleRate;
            }
        }

        private void StartRecording()
        {
            if (playerSprite == null)
            {
                Debug.LogWarning("[NavigatorRecorder] playerSprite is not assigned.");
                return;
            }
            if (anchor == null)
            {
                Debug.LogWarning("[NavigatorRecorder] anchor is not assigned.");
                return;
            }

            _playerTransform = playerSprite.transform.parent != null
                ? playerSprite.transform.parent
                : playerSprite.transform;

            _frames.Clear();
            _recordStartTime = Time.unscaledTime;
            _nextSampleTime = _recordStartTime;
            _recording = true;
            Debug.Log("[NavigatorRecorder] Recording started. Press F9 again to stop and save.");
        }

        private void CaptureFrame()
        {
            Vector2 localPos = (Vector2)_playerTransform.position - (Vector2)anchor.position;
            float flipX = Mathf.Sign(_playerTransform.localScale.x);
            if (flipX == 0) flipX = 1f;

            _frames.Add(new NavigatorClip.Frame
            {
                time = Time.unscaledTime - _recordStartTime,
                localPos = localPos,
                sprite = playerSprite.sprite,
                flipX = flipX
            });
        }

        private void StopAndSave()
        {
            _recording = false;
            Debug.Log($"[NavigatorRecorder] Recording stopped. {_frames.Count} frames captured.");

#if UNITY_EDITOR
            SaveAsset();
#else
            Debug.LogWarning("[NavigatorRecorder] Save is only available in the Editor.");
#endif
        }

#if UNITY_EDITOR
        private void SaveAsset()
        {
            string fullPath = $"Assets/{saveAssetPath}.asset";
            string dir = System.IO.Path.GetDirectoryName(fullPath);
            if (!UnityEditor.AssetDatabase.IsValidFolder(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
                UnityEditor.AssetDatabase.Refresh();
            }

            var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<NavigatorClip>(fullPath);
            if (existing != null)
            {
                existing.SetFrames(_frames);
                UnityEditor.AssetDatabase.SaveAssets();
                Debug.Log($"[NavigatorRecorder] Updated existing clip: {fullPath}");
            }
            else
            {
                var clip = ScriptableObject.CreateInstance<NavigatorClip>();
                clip.SetFrames(_frames);
                UnityEditor.AssetDatabase.CreateAsset(clip, fullPath);
                UnityEditor.AssetDatabase.SaveAssets();
                Debug.Log($"[NavigatorRecorder] Saved new clip: {fullPath}");
            }

            UnityEditor.AssetDatabase.Refresh();
        }
#endif

        private void OnGUI()
        {
            if (!_recording) return;
            GUI.color = Color.red;
            GUI.Label(new Rect(10, 10, 300, 30), $"● REC  {Time.unscaledTime - _recordStartTime:F1}s  ({_frames.Count} frames)");
        }
    }
}
