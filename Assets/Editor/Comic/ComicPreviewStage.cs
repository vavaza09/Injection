using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Game.Comic;

namespace Game.Comic.Editor
{
    /// <summary>
    /// Offscreen preview scene + camera + RenderTexture that renders a <see cref="ComicPage"/>
    /// through the exact same <see cref="ComicPageBuilder"/> the game uses at runtime, so the
    /// Comic Editor window's preview is what Play Mode would actually show — including TMP font
    /// shaping (Thai glyphs, wrapping), which an IMGUI-drawn preview could never reproduce.
    /// Uses the same on-demand <c>Camera.Render()</c> pattern Unity's own preview tooling
    /// (asset thumbnails, etc.) uses to pull frames out of a scene that never renders on its own.
    /// </summary>
    public sealed class ComicPreviewStage : IDisposable
    {
        public const int TextureWidth = 1280;
        public const int TextureHeight = 720;

        private Scene _scene;
        private Camera _camera;
        private RectTransform _canvasRoot;
        private RenderTexture _renderTexture;
        private bool _disposed;

        public RenderTexture Texture => _renderTexture;
        public ComicPageView CurrentView { get; private set; }

        public ComicPreviewStage()
        {
            _scene = EditorSceneManager.NewPreviewScene();

            _renderTexture = new RenderTexture(TextureWidth, TextureHeight, 24, RenderTextureFormat.ARGB32)
            {
                name = "ComicPreviewRT"
            };
            _renderTexture.Create();

            var cameraGO = new GameObject("PreviewCamera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraGO, _scene);
            _camera = cameraGO.GetComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.09f, 0.09f, 0.09f, 1f);
            _camera.orthographic = true;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 100f;
            _camera.targetTexture = _renderTexture;
            _camera.enabled = false; // never part of the normal render loop — only Render() draws it

            var canvasGO = new GameObject("PreviewCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            SceneManager.MoveGameObjectToScene(canvasGO, _scene);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _camera;
            canvas.planeDistance = 10f;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _canvasRoot = (RectTransform)canvasGO.transform;
        }

        /// <summary>Tears down the current page (if any) and builds a fresh one. Call whenever
        /// the selected page or any of its data changes.</summary>
        public void Rebuild(ComicPage page, ComicStyle style)
        {
            CurrentView?.Destroy();
            CurrentView = null;
            if (page == null || _disposed) return;

            CurrentView = ComicPageBuilder.Build(page, style, _canvasRoot, InlineComicTextProvider.Instance);
        }

        public void Render()
        {
            if (_disposed || _camera == null) return;
            _camera.Render();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            CurrentView?.Destroy();
            CurrentView = null;

            if (_renderTexture != null)
            {
                if (_camera != null) _camera.targetTexture = null;
                _renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(_renderTexture);
                _renderTexture = null;
            }

            if (_scene.IsValid())
                EditorSceneManager.ClosePreviewScene(_scene);
        }
    }
}
