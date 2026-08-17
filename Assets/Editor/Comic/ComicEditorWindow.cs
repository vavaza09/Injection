using System.IO;
using UnityEditor;
using UnityEngine;
using TMPro;
using Game.Comic;

namespace Game.Comic.Editor
{
    /// <summary>
    /// Comic authoring window: page list, a live WYSIWYG preview (rendered through the same
    /// <see cref="ComicPageBuilder"/> the game uses — see <see cref="ComicPreviewStage"/>), a
    /// drag-to-move/resize canvas for panels and layers, a field-based inspector with tween
    /// presets, and a beat scrubber. Consolidated into two files (this + ComicPreviewStage) rather
    /// than the five originally sketched (window/canvas-view/inspector/beat-bar as separate
    /// classes) — the split added file-boundary risk without changing what gets built; every
    /// numeric field still doubles as a fallback if a specific drag gesture ever misbehaves.
    /// </summary>
    public class ComicEditorWindow : EditorWindow
    {
        private enum DragMode { None, MovePanel, ResizePanel, MoveLayer, ResizeLayer }
        private enum Corner { TL, TR, BL, BR }
        private enum PresetKind { Fade, SlideLeft, SlideRight, SlideUp, SlideDown, Zoom, Punch, Instant }

        [MenuItem("Tools/Comic Editor")]
        private static void Open()
        {
            var win = GetWindow<ComicEditorWindow>();
            win.titleContent = new GUIContent("Comic Editor");
            win.minSize = new Vector2(940, 580);
            win.Show();
        }

        private ComicSequenceAsset _asset;
        private int _pageIndex = -1;
        private int _panelIndex = -1;
        private int _layerIndex = -1;
        private int _beat;
        private bool _isPlaying;
        private bool _needsRebuild = true;
        private bool _pageSettingsFoldout = true;

        private ComicPreviewStage _stage;
        private double _lastTickTime;

        private Vector2 _leftScroll;
        private Vector2 _rightScroll;
        private Rect _previewScreenRect;

        private DragMode _dragMode;
        private Corner _dragCorner;
        private Vector2 _dragStartMousePage;
        private Rect _dragStartRect;

        private const float HandleSize = 8f;
        private const float LeftColumnWidth = 220f;
        private const float RightColumnWidth = 320f;

        private void OnEnable()
        {
            _stage = new ComicPreviewStage();
            _lastTickTime = EditorApplication.timeSinceStartup;
            _needsRebuild = true;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            _stage?.Dispose();
            _stage = null;
        }

        private void OnEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - _lastTickTime);
            _lastTickTime = now;

            if (_needsRebuild)
            {
                var page = CurrentPage;
                _stage.Rebuild(page, _asset != null ? _asset.Style : new ComicStyle());
                _stage.CurrentView?.ApplyBeat(_beat, false);
                _needsRebuild = false;
            }
            else if (_isPlaying && _stage.CurrentView != null)
            {
                _stage.CurrentView.Tick(dt);
            }

            // The actual camera.Render() call happens in DrawCanvasColumn during OnGUI's
            // Repaint event, not here — interleaving a manual URP render with
            // EditorApplication.update's independent tick (instead of the IMGUI repaint pass
            // that's actually synchronized with the real render pipeline) corrupted Render
            // Graph pass state under continuous use ("BlitFinalToBackBuffer ... dimensions
            // do not match RenderPass specifications"), even though a single isolated render
            // call works fine.
            Repaint();
        }

        // ------------------------------------------------------------ selection helpers

        private ComicPage CurrentPage
        {
            get
            {
                if (_asset == null) return null;
                var pages = _asset.EditablePages;
                if (_pageIndex < 0 || _pageIndex >= pages.Count) return null;
                return pages[_pageIndex];
            }
        }

        private ComicPanel CurrentPanel
        {
            get
            {
                var page = CurrentPage;
                if (page == null || _panelIndex < 0 || _panelIndex >= page.panels.Count) return null;
                return page.panels[_panelIndex];
            }
        }

        private ComicLayer CurrentLayer
        {
            get
            {
                var panel = CurrentPanel;
                if (panel == null || _layerIndex < 0 || _layerIndex >= panel.layers.Count) return null;
                return panel.layers[_layerIndex];
            }
        }

        // ------------------------------------------------------------ OnGUI

        private void OnGUI()
        {
            DrawTopToolbar();

            if (_asset == null)
            {
                EditorGUILayout.HelpBox("Assign or create a Comic Sequence asset above to begin.", MessageType.Info);
                return;
            }

            DrawPageTabs();

            var page = CurrentPage;
            if (page == null)
            {
                EditorGUILayout.HelpBox("Add a page to begin.", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            DrawPanelLayerColumn(page);
            DrawCanvasColumn(page);
            DrawInspectorColumn(page);
            EditorGUILayout.EndHorizontal();

            DrawBeatBar(page);
        }

        private void DrawTopToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUI.BeginChangeCheck();
            var newAsset = (ComicSequenceAsset)EditorGUILayout.ObjectField(_asset, typeof(ComicSequenceAsset), false, GUILayout.Width(260));
            if (EditorGUI.EndChangeCheck())
            {
                _asset = newAsset;
                _pageIndex = _asset != null && _asset.PageCount > 0 ? 0 : -1;
                _panelIndex = -1;
                _layerIndex = -1;
                _beat = 0;
                _needsRebuild = true;
            }

            if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(50)))
                CreateNewSequence();

            GUI.enabled = _asset != null;
            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                EditorUtility.SetDirty(_asset);
                AssetDatabase.SaveAssets();
            }
            GUI.enabled = true;

            GUILayout.FlexibleSpace();

            if (_asset != null)
            {
                GUILayout.Label("Id", GUILayout.Width(16));
                EditorGUI.BeginChangeCheck();
                string newId = EditorGUILayout.TextField(_asset.SequenceId, GUILayout.Width(160));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_asset, "Edit Comic Sequence Id");
                    _asset.SetSequenceId(newId);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void CreateNewSequence()
        {
            string path = EditorUtility.SaveFilePanelInProject("New Comic Sequence", "ComicSequence", "asset", "Choose a location for the new comic sequence.");
            if (string.IsNullOrEmpty(path)) return;

            var asset = ScriptableObject.CreateInstance<ComicSequenceAsset>();
            asset.SetSequenceId(Path.GetFileNameWithoutExtension(path));
            asset.EditablePages.Add(new ComicPage { name = "Page 1" });
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            _asset = asset;
            _pageIndex = 0;
            _panelIndex = -1;
            _layerIndex = -1;
            _beat = 0;
            _needsRebuild = true;
        }

        private void DrawPageTabs()
        {
            var pages = _asset.EditablePages;
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            for (int i = 0; i < pages.Count; i++)
            {
                bool selected = i == _pageIndex;
                string label = string.IsNullOrEmpty(pages[i].name) ? $"Page {i + 1}" : pages[i].name;
                if (GUILayout.Toggle(selected, label, EditorStyles.toolbarButton, GUILayout.Width(90)) && !selected)
                {
                    _pageIndex = i;
                    _panelIndex = -1;
                    _layerIndex = -1;
                    _beat = 0;
                    _needsRebuild = true;
                }
            }

            if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(24)))
            {
                Undo.RecordObject(_asset, "Add Comic Page");
                pages.Add(new ComicPage { name = $"Page {pages.Count + 1}" });
                EditorUtility.SetDirty(_asset);
                _pageIndex = pages.Count - 1;
                _panelIndex = -1;
                _layerIndex = -1;
                _beat = 0;
                _needsRebuild = true;
            }

            if (_pageIndex >= 0 && GUILayout.Button("Delete Page", EditorStyles.toolbarButton, GUILayout.Width(90)))
            {
                if (EditorUtility.DisplayDialog("Delete Page", "Remove the current page?", "Delete", "Cancel"))
                {
                    Undo.RecordObject(_asset, "Delete Comic Page");
                    pages.RemoveAt(_pageIndex);
                    EditorUtility.SetDirty(_asset);
                    _pageIndex = pages.Count == 0 ? -1 : Mathf.Clamp(_pageIndex, 0, pages.Count - 1);
                    _panelIndex = -1;
                    _layerIndex = -1;
                    _beat = 0;
                    _needsRebuild = true;
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        // ------------------------------------------------------------ left column: panel/layer list

        private void DrawPanelLayerColumn(ComicPage page)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(LeftColumnWidth));
            EditorGUILayout.LabelField("Panels", EditorStyles.boldLabel);
            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll, GUILayout.ExpandHeight(true));

            for (int p = 0; p < page.panels.Count; p++)
            {
                var panel = page.panels[p];

                EditorGUILayout.BeginHorizontal();
                bool selected = _panelIndex == p && _layerIndex < 0;
                string label = (string.IsNullOrEmpty(panel.name) ? "Panel" : panel.name) + $"  (beat {panel.beatIndex})";
                if (GUILayout.Toggle(selected, label, EditorStyles.miniButton))
                {
                    _panelIndex = p;
                    _layerIndex = -1;
                }
                if (GUILayout.Button("x", GUILayout.Width(20)))
                {
                    Undo.RecordObject(_asset, "Remove Panel");
                    page.panels.RemoveAt(p);
                    EditorUtility.SetDirty(_asset);
                    if (_panelIndex >= page.panels.Count) _panelIndex = page.panels.Count - 1;
                    _layerIndex = -1;
                    _needsRebuild = true;
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                if (_panelIndex != p) continue;

                EditorGUI.indentLevel++;
                for (int l = 0; l < panel.layers.Count; l++)
                {
                    var layer = panel.layers[l];
                    EditorGUILayout.BeginHorizontal();
                    bool layerSelected = _layerIndex == l;
                    string layerLabel = $"{(string.IsNullOrEmpty(layer.id) ? "layer" : layer.id)} [{layer.kind}]";
                    if (GUILayout.Toggle(layerSelected, layerLabel, EditorStyles.miniButton))
                        _layerIndex = l;
                    if (GUILayout.Button("x", GUILayout.Width(20)))
                    {
                        Undo.RecordObject(_asset, "Remove Layer");
                        panel.layers.RemoveAt(l);
                        EditorUtility.SetDirty(_asset);
                        if (_layerIndex >= panel.layers.Count) _layerIndex = -1;
                        _needsRebuild = true;
                        EditorGUILayout.EndHorizontal();
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("+Sprite", EditorStyles.miniButton)) AddLayer(panel, ComicLayerKind.Sprite);
                if (GUILayout.Button("+Text", EditorStyles.miniButton)) AddLayer(panel, ComicLayerKind.Text);
                EditorGUILayout.EndHorizontal();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("+ Add Panel"))
            {
                Undo.RecordObject(_asset, "Add Panel");
                page.panels.Add(new ComicPanel { name = $"Panel {page.panels.Count + 1}" });
                EditorUtility.SetDirty(_asset);
                _panelIndex = page.panels.Count - 1;
                _layerIndex = -1;
                _needsRebuild = true;
            }

            EditorGUILayout.EndVertical();
        }

        private void AddLayer(ComicPanel panel, ComicLayerKind kind)
        {
            Undo.RecordObject(_asset, "Add Layer");
            var layer = new ComicLayer { kind = kind, id = $"{kind}_{panel.layers.Count + 1}" };
            if (kind == ComicLayerKind.Text) layer.text = "New text";
            panel.layers.Add(layer);
            EditorUtility.SetDirty(_asset);
            _layerIndex = panel.layers.Count - 1;
            _needsRebuild = true;
        }

        // ------------------------------------------------------------ middle column: preview canvas

        private void DrawCanvasColumn(ComicPage page)
        {
            EditorGUILayout.BeginVertical();

            float availableWidth = position.width - LeftColumnWidth - RightColumnWidth - 24f;
            float width = Mathf.Max(240f, availableWidth);
            float height = width * (1080f / 1920f);
            Rect rect = GUILayoutUtility.GetRect(width, height);
            _previewScreenRect = rect;

            if (Event.current.type == EventType.Repaint)
            {
                _stage.Render();
                if (_stage.Texture != null)
                    GUI.DrawTexture(rect, _stage.Texture, ScaleMode.StretchToFill, false);
            }

            HandleCanvasInput(page, rect);
            DrawSelectionOverlay(page, rect);

            EditorGUILayout.EndVertical();
        }

        private Vector2 ScreenToPage(Vector2 screenPos, Rect screenRect)
        {
            float u = (screenPos.x - screenRect.x) / screenRect.width;
            float v = 1f - (screenPos.y - screenRect.y) / screenRect.height;
            return new Vector2(u * 1920f, v * 1080f);
        }

        private Vector2 PageToScreen(Vector2 pagePos, Rect screenRect)
        {
            float x = screenRect.x + (pagePos.x / 1920f) * screenRect.width;
            float y = screenRect.y + (1f - pagePos.y / 1080f) * screenRect.height;
            return new Vector2(x, y);
        }

        private Rect PageRectToScreen(Rect pageRect, Rect screenRect)
        {
            Vector2 topLeft = PageToScreen(new Vector2(pageRect.xMin, pageRect.yMax), screenRect);
            Vector2 bottomRight = PageToScreen(new Vector2(pageRect.xMax, pageRect.yMin), screenRect);
            return new Rect(topLeft.x, topLeft.y, bottomRight.x - topLeft.x, bottomRight.y - topLeft.y);
        }

        // Panels are authored in page space; a layer's rect is relative to its panel's
        // bottom-left corner (see ComicLayer.rect doc) — translate into page space so it can
        // share the drawing/dragging code below.
        private static Rect LayerRectInPageSpace(ComicPanel panel, ComicLayer layer)
        {
            Rect resolved = ComicAutoFit.Resolve(panel, layer);
            return new Rect(panel.rect.x + resolved.x, panel.rect.y + resolved.y, resolved.width, resolved.height);
        }

        private void DrawSelectionOverlay(ComicPage page, Rect screenRect)
        {
            if (Event.current.type != EventType.Repaint) return;

            Handles.BeginGUI();
            for (int p = 0; p < page.panels.Count; p++)
            {
                Rect s = PageRectToScreen(page.panels[p].rect, screenRect);
                Color c = (p == _panelIndex && _layerIndex < 0) ? Color.yellow : new Color(1f, 1f, 1f, 0.35f);
                Handles.DrawSolidRectangleWithOutline(s, Color.clear, c);
            }

            var selPanel = CurrentPanel;
            var selLayer = CurrentLayer;
            if (selPanel != null && selLayer != null)
            {
                Rect s = PageRectToScreen(LayerRectInPageSpace(selPanel, selLayer), screenRect);
                Color c = string.IsNullOrEmpty(selLayer.autoFitTo) ? Color.cyan : new Color(0.6f, 0.6f, 0.6f, 0.8f);
                Handles.DrawSolidRectangleWithOutline(s, Color.clear, c);
            }
            Handles.EndGUI();
        }

        private void HandleCanvasInput(ComicPage page, Rect screenRect)
        {
            var e = Event.current;
            if (_dragMode == DragMode.None && !screenRect.Contains(e.mousePosition)) return;

            switch (e.type)
            {
                case EventType.MouseDown:
                    HandleMouseDown(page, screenRect, e);
                    break;
                case EventType.MouseDrag:
                    HandleMouseDrag(screenRect, e);
                    break;
                case EventType.MouseUp:
                    if (_dragMode != DragMode.None)
                    {
                        _dragMode = DragMode.None;
                        e.Use();
                    }
                    break;
            }
        }

        private void HandleMouseDown(ComicPage page, Rect screenRect, Event e)
        {
            var panel = CurrentPanel;
            var layer = CurrentLayer;

            if (panel != null && layer != null && string.IsNullOrEmpty(layer.autoFitTo))
            {
                Rect pageRect = LayerRectInPageSpace(panel, layer);
                if (TryStartDrag(pageRect, screenRect, e, out var lc))
                {
                    BeginDrag(DragMode.ResizeLayer, lc, pageRect, e);
                    return;
                }
                if (PageRectToScreen(pageRect, screenRect).Contains(e.mousePosition))
                {
                    BeginDrag(DragMode.MoveLayer, Corner.BL, pageRect, e);
                    return;
                }
            }

            if (panel != null && layer == null)
            {
                if (TryStartDrag(panel.rect, screenRect, e, out var pc))
                {
                    BeginDrag(DragMode.ResizePanel, pc, panel.rect, e);
                    return;
                }
                if (PageRectToScreen(panel.rect, screenRect).Contains(e.mousePosition))
                {
                    BeginDrag(DragMode.MovePanel, Corner.BL, panel.rect, e);
                    return;
                }
            }

            for (int p = page.panels.Count - 1; p >= 0; p--)
            {
                if (PageRectToScreen(page.panels[p].rect, screenRect).Contains(e.mousePosition))
                {
                    _panelIndex = p;
                    _layerIndex = -1;
                    e.Use();
                    return;
                }
            }
        }

        private bool TryStartDrag(Rect pageRect, Rect screenRect, Event e, out Corner corner)
        {
            Vector2 tl = PageToScreen(new Vector2(pageRect.xMin, pageRect.yMax), screenRect);
            Vector2 tr = PageToScreen(new Vector2(pageRect.xMax, pageRect.yMax), screenRect);
            Vector2 bl = PageToScreen(new Vector2(pageRect.xMin, pageRect.yMin), screenRect);
            Vector2 br = PageToScreen(new Vector2(pageRect.xMax, pageRect.yMin), screenRect);

            if (Vector2.Distance(e.mousePosition, tl) <= HandleSize) { corner = Corner.TL; return true; }
            if (Vector2.Distance(e.mousePosition, tr) <= HandleSize) { corner = Corner.TR; return true; }
            if (Vector2.Distance(e.mousePosition, bl) <= HandleSize) { corner = Corner.BL; return true; }
            if (Vector2.Distance(e.mousePosition, br) <= HandleSize) { corner = Corner.BR; return true; }

            corner = Corner.BL;
            return false;
        }

        private void BeginDrag(DragMode mode, Corner corner, Rect startRect, Event e)
        {
            _dragMode = mode;
            _dragCorner = corner;
            _dragStartRect = startRect;
            _dragStartMousePage = ScreenToPage(e.mousePosition, _previewScreenRect);
            e.Use();
        }

        private void HandleMouseDrag(Rect screenRect, Event e)
        {
            if (_dragMode == DragMode.None) return;
            var panel = CurrentPanel;
            var layer = CurrentLayer;
            if (panel == null) { _dragMode = DragMode.None; return; }

            Vector2 currentPage = ScreenToPage(e.mousePosition, screenRect);
            Vector2 delta = currentPage - _dragStartMousePage;

            Rect newRect;
            if (_dragMode == DragMode.MovePanel || _dragMode == DragMode.MoveLayer)
            {
                newRect = new Rect(_dragStartRect.x + delta.x, _dragStartRect.y + delta.y, _dragStartRect.width, _dragStartRect.height);
            }
            else
            {
                float fixedX = (_dragCorner == Corner.TR || _dragCorner == Corner.BR) ? _dragStartRect.xMin : _dragStartRect.xMax;
                float fixedY = (_dragCorner == Corner.TL || _dragCorner == Corner.TR) ? _dragStartRect.yMin : _dragStartRect.yMax;
                float movingX = ((_dragCorner == Corner.TR || _dragCorner == Corner.BR) ? _dragStartRect.xMax : _dragStartRect.xMin) + delta.x;
                float movingY = ((_dragCorner == Corner.TL || _dragCorner == Corner.TR) ? _dragStartRect.yMax : _dragStartRect.yMin) + delta.y;
                newRect = Rect.MinMaxRect(Mathf.Min(fixedX, movingX), Mathf.Min(fixedY, movingY), Mathf.Max(fixedX, movingX), Mathf.Max(fixedY, movingY));
            }

            Undo.RecordObject(_asset, "Drag Comic Rect");
            if (_dragMode == DragMode.MovePanel || _dragMode == DragMode.ResizePanel)
                panel.rect = newRect;
            else if (layer != null)
                layer.rect = new Rect(newRect.x - panel.rect.x, newRect.y - panel.rect.y, newRect.width, newRect.height);

            EditorUtility.SetDirty(_asset);
            _needsRebuild = true;
            e.Use();
        }

        // ------------------------------------------------------------ right column: inspector

        private void DrawInspectorColumn(ComicPage page)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(RightColumnWidth));
            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

            _pageSettingsFoldout = EditorGUILayout.Foldout(_pageSettingsFoldout, "Page Settings", true);
            if (_pageSettingsFoldout)
            {
                DrawPageSettings(page);
                EditorGUILayout.Space();
                DrawBeatEvents(page);
                EditorGUILayout.Space();
            }

            var panel = CurrentPanel;
            var layer = CurrentLayer;

            if (panel != null && layer != null) DrawLayerInspector(panel, layer);
            else if (panel != null) DrawPanelInspector(panel);
            else EditorGUILayout.HelpBox("Select a panel or layer to edit its properties.", MessageType.None);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawPageSettings(ComicPage page)
        {
            EditorGUI.BeginChangeCheck();
            string name = EditorGUILayout.TextField("Page Name", page.name);
            Color bg = EditorGUILayout.ColorField("Background", page.background);
            var transition = (ComicTransitionKind)EditorGUILayout.EnumPopup("Enter Transition", page.enterTransition);
            float duration = EditorGUILayout.FloatField("Transition Duration", page.transitionDuration);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_asset, "Edit Page Settings");
                page.name = name;
                page.background = bg;
                page.enterTransition = transition;
                page.transitionDuration = Mathf.Max(0f, duration);
                EditorUtility.SetDirty(_asset);
                _needsRebuild = true;
            }
            EditorGUILayout.HelpBox("Enter Transition is ignored on this page's first appearance if it's page 1 of the sequence (always Cut).", MessageType.None);
        }

        private void DrawBeatEvents(ComicPage page)
        {
            EditorGUILayout.LabelField("Beat Events (SFX / Music / Shake / Auto-Advance)", EditorStyles.boldLabel);

            for (int i = 0; i < page.beatEvents.Count; i++)
            {
                var e = page.beatEvents[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUI.BeginChangeCheck();
                int beat = EditorGUILayout.IntField("Beat", e.beatIndex);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_asset, "Edit Beat Event");
                    e.beatIndex = Mathf.Max(0, beat);
                    EditorUtility.SetDirty(_asset);
                    _needsRebuild = true;
                }

                DrawEnumNameField("SFX Name", "SoundType", e.sfxName, v =>
                {
                    Undo.RecordObject(_asset, "Edit Beat Event SFX");
                    e.sfxName = v;
                    EditorUtility.SetDirty(_asset);
                });
                DrawEnumNameField("Music Name", "MusicType", e.musicName, v =>
                {
                    Undo.RecordObject(_asset, "Edit Beat Event Music");
                    e.musicName = v;
                    EditorUtility.SetDirty(_asset);
                });

                EditorGUI.BeginChangeCheck();
                float shakeAmp = EditorGUILayout.FloatField("Shake Amplitude", e.shakeAmplitude);
                float shakeDur = EditorGUILayout.FloatField("Shake Duration", e.shakeDuration);
                float autoAdvance = EditorGUILayout.FloatField("Auto-Advance After (s)", e.autoAdvanceAfter);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_asset, "Edit Beat Event");
                    e.shakeAmplitude = Mathf.Max(0f, shakeAmp);
                    e.shakeDuration = Mathf.Max(0f, shakeDur);
                    e.autoAdvanceAfter = Mathf.Max(0f, autoAdvance);
                    EditorUtility.SetDirty(_asset);
                    _needsRebuild = true;
                }

                if (GUILayout.Button("Remove Beat Event"))
                {
                    Undo.RecordObject(_asset, "Remove Beat Event");
                    page.beatEvents.RemoveAt(i);
                    EditorUtility.SetDirty(_asset);
                    _needsRebuild = true;
                    EditorGUILayout.EndVertical();
                    break;
                }

                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("+ Add Beat Event"))
            {
                Undo.RecordObject(_asset, "Add Beat Event");
                page.beatEvents.Add(new ComicBeatEvent());
                EditorUtility.SetDirty(_asset);
                _needsRebuild = true;
            }
        }

        // Sfx/Music are matched by enum NAME at runtime (see ComicBeatEvent doc comment — this
        // asmdef can't reference SoundType/MusicType directly, they live in Assembly-CSharp).
        // This picker looks them up via reflection purely to save designers from typos; the
        // plain text field underneath still works fine on its own if reflection finds nothing.
        private void DrawEnumNameField(string label, string typeName, string currentValue, System.Action<string> onChange)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            string newVal = EditorGUILayout.TextField(label, currentValue);
            if (EditorGUI.EndChangeCheck()) onChange(newVal);

            if (GUILayout.Button("Pick", GUILayout.Width(40)))
            {
                var type = System.Type.GetType(typeName + ", Assembly-CSharp");
                var menu = new GenericMenu();
                if (type == null || !type.IsEnum)
                {
                    menu.AddDisabledItem(new GUIContent("(" + typeName + " not found)"));
                }
                else
                {
                    foreach (var n in System.Enum.GetNames(type))
                    {
                        string captured = n;
                        menu.AddItem(new GUIContent(captured), currentValue == captured, () => onChange(captured));
                    }
                }
                menu.ShowAsContext();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPanelInspector(ComicPanel panel)
        {
            EditorGUILayout.LabelField("Panel", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            string name = EditorGUILayout.TextField("Name", panel.name);
            int beat = EditorGUILayout.IntField("Beat Index", panel.beatIndex);
            EditorGUILayout.LabelField("Rect (page space, bottom-left origin)");
            float x = EditorGUILayout.FloatField("X", panel.rect.x);
            float y = EditorGUILayout.FloatField("Y", panel.rect.y);
            float w = EditorGUILayout.FloatField("W", panel.rect.width);
            float h = EditorGUILayout.FloatField("H", panel.rect.height);
            float rot = EditorGUILayout.FloatField("Rotation", panel.rotation);
            var maskShape = (Sprite)EditorGUILayout.ObjectField("Mask Shape", panel.maskShape, typeof(Sprite), false);
            bool drawBorder = EditorGUILayout.Toggle("Draw Border", panel.drawBorder);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_asset, "Edit Panel");
                panel.name = name;
                panel.beatIndex = Mathf.Max(0, beat);
                panel.rect = new Rect(x, y, Mathf.Max(1f, w), Mathf.Max(1f, h));
                panel.rotation = rot;
                panel.maskShape = maskShape;
                panel.drawBorder = drawBorder;
                EditorUtility.SetDirty(_asset);
                _needsRebuild = true;
            }

            EditorGUILayout.Space();
            DrawTweenSection("Entrance", panel.entrance);
            EditorGUILayout.Space();
            DrawTweenSection("Exit", panel.exit);
        }

        private void DrawLayerInspector(ComicPanel panel, ComicLayer layer)
        {
            EditorGUILayout.LabelField("Layer", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            string id = EditorGUILayout.TextField("Id", layer.id);
            int beat = EditorGUILayout.IntField("Beat Index", layer.beatIndex);
            EditorGUILayout.LabelField("Rect (relative to panel's bottom-left)");
            float x = EditorGUILayout.FloatField("X", layer.rect.x);
            float y = EditorGUILayout.FloatField("Y", layer.rect.y);
            float w = EditorGUILayout.FloatField("W", layer.rect.width);
            float h = EditorGUILayout.FloatField("H", layer.rect.height);
            float rot = EditorGUILayout.FloatField("Rotation", layer.rotation);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_asset, "Edit Layer Transform");
                layer.id = id;
                layer.beatIndex = Mathf.Max(0, beat);
                layer.rect = new Rect(x, y, Mathf.Max(1f, w), Mathf.Max(1f, h));
                layer.rotation = rot;
                EditorUtility.SetDirty(_asset);
                _needsRebuild = true;
            }

            EditorGUILayout.Space();
            DrawAutoFitPicker(panel, layer);
            if (!string.IsNullOrEmpty(layer.autoFitTo))
            {
                EditorGUI.BeginChangeCheck();
                float pl = EditorGUILayout.FloatField("Pad Left", layer.autoFitPadding.x);
                float pb = EditorGUILayout.FloatField("Pad Bottom", layer.autoFitPadding.y);
                float pr = EditorGUILayout.FloatField("Pad Right", layer.autoFitPadding.z);
                float pt = EditorGUILayout.FloatField("Pad Top", layer.autoFitPadding.w);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_asset, "Edit Auto Fit Padding");
                    layer.autoFitPadding = new Vector4(pl, pb, pr, pt);
                    EditorUtility.SetDirty(_asset);
                    _needsRebuild = true;
                }
            }

            EditorGUILayout.Space();
            if (layer.kind == ComicLayerKind.Sprite) DrawSpriteLayerFields(layer);
            else DrawTextLayerFields(layer);

            EditorGUILayout.Space();
            DrawTweenSection("Entrance", layer.entrance);
            EditorGUILayout.Space();
            DrawTweenSection("Exit", layer.exit);
            EditorGUILayout.Space();
            DrawIdleSection(layer.idle);
        }

        private void DrawAutoFitPicker(ComicPanel panel, ComicLayer layer)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Auto Fit To", GUILayout.Width(EditorGUIUtility.labelWidth));
            string display = string.IsNullOrEmpty(layer.autoFitTo) ? "(none)" : layer.autoFitTo;
            if (GUILayout.Button(display, EditorStyles.popup))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("(none)"), string.IsNullOrEmpty(layer.autoFitTo), () => ApplyAutoFitChoice(layer, ""));
                foreach (var other in panel.layers)
                {
                    if (other == layer) continue;
                    string id = other.id;
                    menu.AddItem(new GUIContent(id), layer.autoFitTo == id, () => ApplyAutoFitChoice(layer, id));
                }
                menu.ShowAsContext();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void ApplyAutoFitChoice(ComicLayer layer, string id)
        {
            Undo.RecordObject(_asset, "Set Auto Fit Target");
            layer.autoFitTo = id;
            EditorUtility.SetDirty(_asset);
            _needsRebuild = true;
        }

        private void DrawSpriteLayerFields(ComicLayer layer)
        {
            EditorGUILayout.LabelField("Sprite", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var sprite = (Sprite)EditorGUILayout.ObjectField("Sprite", layer.sprite, typeof(Sprite), false);
            var tint = EditorGUILayout.ColorField("Tint", layer.tint);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_asset, "Edit Sprite Layer");
                layer.sprite = sprite;
                layer.tint = tint;
                EditorUtility.SetDirty(_asset);
                _needsRebuild = true;
            }

            EditorGUILayout.LabelField("Flipbook (optional — overrides Sprite when non-empty)");
            for (int i = 0; i < layer.flipbookFrames.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                var frame = (Sprite)EditorGUILayout.ObjectField(layer.flipbookFrames[i], typeof(Sprite), false);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_asset, "Edit Flipbook Frame");
                    layer.flipbookFrames[i] = frame;
                    EditorUtility.SetDirty(_asset);
                    _needsRebuild = true;
                }
                if (GUILayout.Button("x", GUILayout.Width(20)))
                {
                    Undo.RecordObject(_asset, "Remove Flipbook Frame");
                    layer.flipbookFrames.RemoveAt(i);
                    EditorUtility.SetDirty(_asset);
                    _needsRebuild = true;
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("+ Add Frame"))
            {
                Undo.RecordObject(_asset, "Add Flipbook Frame");
                layer.flipbookFrames.Add(null);
                EditorUtility.SetDirty(_asset);
                _needsRebuild = true;
            }

            EditorGUI.BeginChangeCheck();
            float fps = EditorGUILayout.FloatField("Flipbook Fps", layer.flipbookFps);
            bool loop = EditorGUILayout.Toggle("Flipbook Loop", layer.flipbookLoop);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_asset, "Edit Flipbook Settings");
                layer.flipbookFps = Mathf.Max(0.1f, fps);
                layer.flipbookLoop = loop;
                EditorUtility.SetDirty(_asset);
                _needsRebuild = true;
            }
        }

        private void DrawTextLayerFields(ComicLayer layer)
        {
            EditorGUILayout.LabelField("Text", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            string text = EditorGUILayout.TextArea(layer.text, GUILayout.MinHeight(50));
            string locKey = EditorGUILayout.TextField("Loc Key (optional)", layer.locKey);
            var font = (TMP_FontAsset)EditorGUILayout.ObjectField("Font", layer.font, typeof(TMP_FontAsset), false);
            float fontSize = EditorGUILayout.FloatField("Font Size", layer.fontSize);
            var color = EditorGUILayout.ColorField("Color", layer.textColor);
            var reveal = (TextRevealMode)EditorGUILayout.EnumPopup("Reveal", layer.reveal);
            float cps = EditorGUILayout.FloatField("Chars / Sec", layer.charsPerSecond);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_asset, "Edit Text Layer");
                layer.text = text;
                layer.locKey = locKey;
                layer.font = font;
                layer.fontSize = Mathf.Max(1f, fontSize);
                layer.textColor = color;
                layer.reveal = reveal;
                layer.charsPerSecond = Mathf.Max(1f, cps);
                EditorUtility.SetDirty(_asset);
                _needsRebuild = true;
            }
        }

        private void DrawTweenSection(string label, ComicTween tween)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Fade", EditorStyles.miniButton)) ApplyPreset(tween, PresetKind.Fade);
            if (GUILayout.Button("<-", EditorStyles.miniButton)) ApplyPreset(tween, PresetKind.SlideLeft);
            if (GUILayout.Button("->", EditorStyles.miniButton)) ApplyPreset(tween, PresetKind.SlideRight);
            if (GUILayout.Button("Up", EditorStyles.miniButton)) ApplyPreset(tween, PresetKind.SlideUp);
            if (GUILayout.Button("Dn", EditorStyles.miniButton)) ApplyPreset(tween, PresetKind.SlideDown);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Zoom", EditorStyles.miniButton)) ApplyPreset(tween, PresetKind.Zoom);
            if (GUILayout.Button("Punch", EditorStyles.miniButton)) ApplyPreset(tween, PresetKind.Punch);
            if (GUILayout.Button("Instant", EditorStyles.miniButton)) ApplyPreset(tween, PresetKind.Instant);
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            Vector2 fromOffset = EditorGUILayout.Vector2Field("From Offset", tween.fromOffset);
            float fromScale = EditorGUILayout.FloatField("From Scale", tween.fromScale);
            float fromRotation = EditorGUILayout.FloatField("From Rotation", tween.fromRotation);
            float fromAlpha = EditorGUILayout.Slider("From Alpha", tween.fromAlpha, 0f, 1f);
            float delay = EditorGUILayout.FloatField("Delay", tween.delay);
            float duration = EditorGUILayout.FloatField("Duration", tween.duration);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_asset, $"Edit {label} Tween");
                tween.fromOffset = fromOffset;
                tween.fromScale = fromScale;
                tween.fromRotation = fromRotation;
                tween.fromAlpha = fromAlpha;
                tween.delay = Mathf.Max(0f, delay);
                tween.duration = Mathf.Max(0f, duration);
                EditorUtility.SetDirty(_asset);
                _needsRebuild = true;
            }
        }

        private void ApplyPreset(ComicTween tween, PresetKind kind)
        {
            Undo.RecordObject(_asset, "Apply Tween Preset");
            float dur = tween.duration > 0f ? tween.duration : 0.35f;
            switch (kind)
            {
                case PresetKind.Fade: tween.fromOffset = Vector2.zero; tween.fromScale = 1f; tween.fromRotation = 0f; tween.fromAlpha = 0f; break;
                case PresetKind.SlideLeft: tween.fromOffset = new Vector2(-300f, 0f); tween.fromScale = 1f; tween.fromRotation = 0f; tween.fromAlpha = 0f; break;
                case PresetKind.SlideRight: tween.fromOffset = new Vector2(300f, 0f); tween.fromScale = 1f; tween.fromRotation = 0f; tween.fromAlpha = 0f; break;
                case PresetKind.SlideUp: tween.fromOffset = new Vector2(0f, -300f); tween.fromScale = 1f; tween.fromRotation = 0f; tween.fromAlpha = 0f; break;
                case PresetKind.SlideDown: tween.fromOffset = new Vector2(0f, 300f); tween.fromScale = 1f; tween.fromRotation = 0f; tween.fromAlpha = 0f; break;
                case PresetKind.Zoom: tween.fromOffset = Vector2.zero; tween.fromScale = 1.3f; tween.fromRotation = 0f; tween.fromAlpha = 0f; break;
                case PresetKind.Punch: tween.fromOffset = Vector2.zero; tween.fromScale = 0.7f; tween.fromRotation = -4f; tween.fromAlpha = 0f; break;
                case PresetKind.Instant: tween.duration = 0f; break;
            }
            if (kind != PresetKind.Instant) tween.duration = dur;
            EditorUtility.SetDirty(_asset);
            _needsRebuild = true;
        }

        private void DrawIdleSection(ComicIdleLoop idle)
        {
            EditorGUILayout.LabelField("Idle Loop", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var kind = (IdleKind)EditorGUILayout.EnumPopup("Kind", idle.kind);
            Vector2 amp = EditorGUILayout.Vector2Field("Amplitude", idle.amplitude);
            float period = EditorGUILayout.FloatField("Period", idle.period);
            float zoom = EditorGUILayout.FloatField("Zoom Amount", idle.zoomAmount);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_asset, "Edit Idle Loop");
                idle.kind = kind;
                idle.amplitude = amp;
                idle.period = Mathf.Max(0.01f, period);
                idle.zoomAmount = zoom;
                EditorUtility.SetDirty(_asset);
                _needsRebuild = true;
            }
        }

        // ------------------------------------------------------------ bottom: beat bar

        private void DrawBeatBar(ComicPage page)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("|<", EditorStyles.toolbarButton, GUILayout.Width(28))) SetBeat(0);
            if (GUILayout.Button("<", EditorStyles.toolbarButton, GUILayout.Width(28))) SetBeat(_beat - 1);

            int maxBeat = page.MaxBeatIndex();
            EditorGUI.BeginChangeCheck();
            int newBeat = EditorGUILayout.IntSlider(_beat, 0, maxBeat);
            if (EditorGUI.EndChangeCheck()) SetBeat(newBeat);

            if (GUILayout.Button(">", EditorStyles.toolbarButton, GUILayout.Width(28))) SetBeat(_beat + 1);
            if (GUILayout.Button(">|", EditorStyles.toolbarButton, GUILayout.Width(28))) SetBeat(maxBeat);

            _isPlaying = GUILayout.Toggle(_isPlaying, _isPlaying ? "Pause" : "Play", EditorStyles.toolbarButton, GUILayout.Width(50));

            GUILayout.Label($"Beat {_beat} / {maxBeat}", GUILayout.Width(90));

            EditorGUILayout.EndHorizontal();
        }

        private void SetBeat(int beat)
        {
            var page = CurrentPage;
            int maxBeat = page != null ? page.MaxBeatIndex() : 0;
            _beat = Mathf.Clamp(beat, 0, maxBeat);
            _stage.CurrentView?.ApplyBeat(_beat, false);
        }
    }
}
