using System;
using UnityEngine;
using UnityEngine.UI;
using Game.Comic;

namespace Game.Comic.Editor
{
    /// <summary>
    /// Offscreen preview camera + Canvas that renders a <see cref="ComicPage"/> through the exact
    /// same <see cref="ComicPageBuilder"/> the game uses at runtime, so the Comic Editor window's
    /// preview is what Play Mode would actually show — including TMP font shaping (Thai glyphs,
    /// wrapping), which an IMGUI-drawn preview could never reproduce.
    /// <para/>
    /// Two non-obvious constraints, both verified empirically with a manual pixel readback of the
    /// RenderTexture (not just "compiles without error") before landing on this shape:
    /// <para/>
    /// 1) This was originally hosted in an <c>EditorSceneManager.NewPreviewScene()</c>, matching
    /// the pattern Unity's own asset-preview tooling uses. URP never renders cameras belonging to
    /// such a scene — confirmed with a plain unlit 3D quad, not just UI, so it isn't Canvas-specific.
    /// Unity also refuses to make a preview scene the active scene (throws), so there's no way to
    /// coax URP into it. The same camera renders correctly the instant it belongs to a real, loaded
    /// scene — so this now lives in whichever scene is currently active, as
    /// <see cref="HideFlags.HideAndDontSave"/> objects: invisible in the Hierarchy, never written to
    /// the scene file. If the user switches scenes while the window is open, Unity destroys these
    /// along with everything else in the old scene; <see cref="Render"/>/<see cref="Rebuild"/> detect
    /// that (the camera reference goes Unity-fake-null) and rebuild transparently.
    /// <para/>
    /// 2) Do **not** set <c>camera.cameraType = CameraType.Preview</c> here, despite that being the
    /// textbook fix for "a camera built purely via script renders nothing." It solves exactly that
    /// problem for ordinary 3D geometry — but it also silently excludes uGUI Canvas compositing
    /// entirely, on any scene, regardless of render mode (ScreenSpaceCamera and WorldSpace canvases
    /// were both tested). A plain default `CameraType.Game` camera renders the Canvas correctly.
    /// </summary>
    public sealed class ComicPreviewStage : IDisposable
    {
        public const int TextureWidth = 1280;
        public const int TextureHeight = 720;

        private Camera _camera;
        private RectTransform _canvasRoot;
        private RenderTexture _renderTexture;
        private bool _disposed;

        public RenderTexture Texture => _renderTexture;
        public ComicPageView CurrentView { get; private set; }

        public ComicPreviewStage()
        {
            EnsureStage();
        }

        private void BuildStageObjects()
        {
            var cameraGO = new GameObject("ComicPreviewStage_Camera", typeof(Camera));
            cameraGO.hideFlags = HideFlags.HideAndDontSave;
            _camera = cameraGO.GetComponent<Camera>();
            // Left at the default CameraType.Game deliberately — see the class doc comment.
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.09f, 0.09f, 0.09f, 1f);
            _camera.orthographic = true;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 100f;
            _camera.targetTexture = _renderTexture;
            _camera.enabled = false; // never part of the normal render loop — only Render() draws it

            var canvasGO = new GameObject("ComicPreviewStage_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGO.hideFlags = HideFlags.HideAndDontSave;
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

        // Defensive against Unity-fake-null on either piece independently: the camera/canvas die
        // together if their owning scene unloads (see constraint 1 above), but the RenderTexture
        // has also been observed going stale on its own (e.g. across the OnDisable/OnEnable pair
        // a domain reload drives on an already-open window) without _disposed ever being set.
        // Recreating the texture always rebuilds the camera+canvas too, so targetTexture is never
        // left pointing at a dead RenderTexture.
        private void EnsureStage()
        {
            if (_renderTexture == null)
            {
                _renderTexture = new RenderTexture(TextureWidth, TextureHeight, 24, RenderTextureFormat.ARGB32)
                {
                    name = "ComicPreviewRT"
                };
                _renderTexture.Create();
                _camera = null;
                _canvasRoot = null;
            }

            if (_camera != null && _canvasRoot != null) return;

            CurrentView = null; // its GameObjects die along with the old camera/canvas
            BuildStageObjects();
        }

        /// <summary>Tears down the current page (if any) and builds a fresh one. Call whenever
        /// the selected page or any of its data changes.</summary>
        public void Rebuild(ComicPage page, ComicStyle style)
        {
            if (_disposed) return;
            EnsureStage();

            CurrentView?.Destroy();
            CurrentView = null;
            if (page == null) return;

            CurrentView = ComicPageBuilder.Build(page, style, _canvasRoot, InlineComicTextProvider.Instance);
        }

        public void Render()
        {
            if (_disposed) return;
            EnsureStage();
            // Canvas geometry (CanvasRenderer meshes) is normally rebuilt by Unity's own per-frame
            // tick; Rebuild()+Render() can run back to back within the same synchronous call with
            // no such tick in between, which left brand-new layers with no mesh yet to draw.
            Canvas.ForceUpdateCanvases();
            _camera.Render();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            CurrentView?.Destroy();
            CurrentView = null;

            if (_camera != null) UnityEngine.Object.DestroyImmediate(_camera.gameObject);
            _camera = null;

            if (_canvasRoot != null) UnityEngine.Object.DestroyImmediate(_canvasRoot.gameObject);
            _canvasRoot = null;

            if (_renderTexture != null)
            {
                _renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(_renderTexture);
                _renderTexture = null;
            }
        }
    }
}
