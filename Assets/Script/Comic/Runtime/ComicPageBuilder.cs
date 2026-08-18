using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Core.Logging;

namespace Game.Comic
{
    /// <summary>
    /// Builds a live uGUI hierarchy for one <see cref="ComicPage"/> under a given parent
    /// RectTransform, returning the <see cref="ComicPageView"/> that drives it. This is the
    /// single seam used by both the game (building into the real gameplay Canvas) and the Comic
    /// Editor window's preview (building into an offscreen preview-scene Canvas rendered to a
    /// RenderTexture) — the two are guaranteed to look identical because they are, literally,
    /// the same code path.
    /// </summary>
    public static class ComicPageBuilder
    {
        public static ComicPageView Build(ComicPage page, ComicStyle style, RectTransform parent, IComicTextProvider textProvider = null, Core.Logging.ILogger logger = null)
        {
            if (page == null)
            {
                logger?.LogError("[ComicPageBuilder] page is null.");
                return null;
            }
            if (style == null) style = new ComicStyle();
            if (textProvider == null) textProvider = InlineComicTextProvider.Instance;

            var pageRootGO = new GameObject(string.IsNullOrEmpty(page.name) ? "Page" : page.name, typeof(RectTransform));
            var pageRoot = (RectTransform)pageRootGO.transform;
            pageRoot.SetParent(parent, false);
            pageRoot.anchorMin = new Vector2(0.5f, 0.5f);
            pageRoot.anchorMax = new Vector2(0.5f, 0.5f);
            pageRoot.pivot = new Vector2(0.5f, 0.5f);
            pageRoot.sizeDelta = new Vector2(1920f, 1080f);
            pageRoot.anchoredPosition = Vector2.zero;
            pageRoot.localScale = Vector3.one;

            if (page.background.a > 0f)
            {
                var bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
                var bgRt = (RectTransform)bgGO.transform;
                bgRt.SetParent(pageRoot, false);
                Stretch(bgRt);
                var bgImg = bgGO.GetComponent<Image>();
                bgImg.color = page.background;
                bgImg.raycastTarget = false;
            }

            var view = new ComicPageView(page, style, pageRoot);

            for (int i = 0; i < page.panels.Count; i++)
                view.AddPanel(BuildPanel(page.panels[i], style, pageRoot, textProvider, logger));

            view.ApplyBeat(-1, false);
            return view;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static ComicPanelRuntime BuildPanel(ComicPanel data, ComicStyle style, RectTransform pageRoot, IComicTextProvider textProvider, Core.Logging.ILogger logger)
        {
            var rootGO = new GameObject(string.IsNullOrEmpty(data.name) ? "Panel" : data.name, typeof(RectTransform), typeof(CanvasGroup));
            var root = (RectTransform)rootGO.transform;
            root.SetParent(pageRoot, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.zero;
            root.pivot = data.pivot;
            var basePos = new Vector2(data.rect.x + data.pivot.x * data.rect.width, data.rect.y + data.pivot.y * data.rect.height);
            root.anchoredPosition = basePos;
            root.sizeDelta = new Vector2(data.rect.width, data.rect.height);
            root.localEulerAngles = new Vector3(0f, 0f, data.rotation);
            root.localScale = Vector3.one;

            var canvasGroup = rootGO.GetComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            // Clip: the mask shape's own graphic never renders (showMaskGraphic = false) — it
            // only defines the stencil silhouette that this panel's layers get clipped to.
            var clipGO = new GameObject("Clip", typeof(RectTransform), typeof(Image), typeof(Mask));
            var clip = (RectTransform)clipGO.transform;
            clip.SetParent(root, false);
            Stretch(clip);
            var clipImage = clipGO.GetComponent<Image>();
            clipImage.raycastTarget = false;
            if (data.maskShape != null) clipImage.sprite = data.maskShape;
            var mask = clipGO.GetComponent<Mask>();
            mask.showMaskGraphic = false;

            // Border strips are siblings of Clip (not children of it) so they are never clipped
            // by the mask shape, and rotate rigidly with the panel because Root carries the rotation.
            if (data.drawBorder && style.borderWidth > 0f)
                BuildBorder(root, style);

            // Dim overlay lives inside Clip (so it respects the mask silhouette) and is kept the
            // last sibling so it always draws above every layer.
            var dimGO = new GameObject("DimOverlay", typeof(RectTransform), typeof(Image));
            var dimRt = (RectTransform)dimGO.transform;
            dimRt.SetParent(clip, false);
            Stretch(dimRt);
            var dimImage = dimGO.GetComponent<Image>();
            dimImage.raycastTarget = false;
            dimImage.color = new Color(style.dimColor.r, style.dimColor.g, style.dimColor.b, 0f);

            var runtime = new ComicPanelRuntime
            {
                data = data,
                root = root,
                clip = clip,
                canvasGroup = canvasGroup,
                dimOverlay = dimImage,
                basePos = basePos,
                baseRotation = data.rotation,
            };

            for (int i = 0; i < data.layers.Count; i++)
                runtime.layers.Add(BuildLayer(data, data.layers[i], clip, textProvider, logger));

            dimGO.transform.SetAsLastSibling();

            return runtime;
        }

        private static void BuildBorder(RectTransform root, ComicStyle style)
        {
            CreateBorderStrip(root, "BorderTop", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, style.borderWidth), style.borderColor);
            CreateBorderStrip(root, "BorderBottom", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, style.borderWidth), style.borderColor);
            CreateBorderStrip(root, "BorderLeft", new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(style.borderWidth, 0), style.borderColor);
            CreateBorderStrip(root, "BorderRight", new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(style.borderWidth, 0), style.borderColor);
        }

        private static void CreateBorderStrip(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        private static ComicLayerRuntime BuildLayer(ComicPanel panel, ComicLayer data, RectTransform clip, IComicTextProvider textProvider, Core.Logging.ILogger logger)
        {
            Rect builtRect = ComicAutoFit.Resolve(panel, data);
            var basePos = new Vector2(builtRect.x + data.pivot.x * builtRect.width, builtRect.y + data.pivot.y * builtRect.height);

            GameObject go;
            Image image = null;
            TextMeshProUGUI tmp = null;
            Color baseColor;
            string resolvedText = null;

            if (data.kind == ComicLayerKind.Text)
            {
                go = new GameObject(string.IsNullOrEmpty(data.id) ? "Text" : data.id, typeof(RectTransform), typeof(TextMeshProUGUI));
                tmp = go.GetComponent<TextMeshProUGUI>();
                resolvedText = textProvider.Resolve(data.locKey, data.text) ?? "";
                tmp.text = resolvedText;
                if (data.font != null) tmp.font = data.font;
                tmp.fontSize = data.fontSize;
                tmp.color = data.textColor;
                tmp.raycastTarget = false;
                tmp.alignment = data.textAlignment;
                tmp.maxVisibleCharacters = data.reveal == TextRevealMode.Instant ? int.MaxValue : 0;
                baseColor = data.textColor;
            }
            else
            {
                go = new GameObject(string.IsNullOrEmpty(data.id) ? "Sprite" : data.id, typeof(RectTransform), typeof(Image));
                image = go.GetComponent<Image>();
                image.sprite = data.flipbookFrames.Count > 0 ? data.flipbookFrames[0] : data.sprite;
                image.color = data.tint;
                image.raycastTarget = false;
                image.type = data.drawMode;
                image.preserveAspect = false;
                image.pixelsPerUnitMultiplier = data.borderScale;
                image.fillMethod = data.fillMethod;
                image.fillOrigin = data.fillOrigin;
                image.fillAmount = data.fillAmount;
                image.fillClockwise = data.fillClockwise;
                baseColor = data.tint;
            }

            var rt = (RectTransform)go.transform;
            rt.SetParent(clip, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = data.pivot;
            rt.anchoredPosition = basePos;
            rt.sizeDelta = new Vector2(builtRect.width, builtRect.height);
            rt.localEulerAngles = new Vector3(0f, 0f, data.rotation);
            rt.localScale = new Vector3(data.flipX ? -1f : 1f, data.flipY ? -1f : 1f, 1f);

            return new ComicLayerRuntime
            {
                data = data,
                rt = rt,
                image = image,
                tmp = tmp,
                baseColor = baseColor,
                basePos = basePos,
                baseRotation = data.rotation,
                resolvedText = resolvedText ?? "",
            };
        }
    }
}
