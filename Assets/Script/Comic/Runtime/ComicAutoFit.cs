using UnityEngine;

namespace Game.Comic
{
    /// <summary>Resolves a layer's built rect when it declares <see cref="ComicLayer.autoFitTo"/> —
    /// e.g. a balloon Sprite layer growing to enclose a Dialog Text layer plus padding. Pure and
    /// data-only (computed from authored rects, not live-rendered text bounds) so layout stays a
    /// deterministic function of the asset, both for the runtime builder and for EditMode tests.</summary>
    public static class ComicAutoFit
    {
        /// <summary>Returns the rect <paramref name="layer"/> should be built with. Falls back to
        /// <see cref="ComicLayer.rect"/> as authored when autoFitTo is empty or references a layer
        /// id that doesn't exist in the panel (dangling id — degrades gracefully, never throws).</summary>
        public static Rect Resolve(ComicPanel panel, ComicLayer layer)
        {
            if (layer == null) return default;
            if (panel == null || string.IsNullOrEmpty(layer.autoFitTo)) return layer.rect;

            for (int i = 0; i < panel.layers.Count; i++)
            {
                var target = panel.layers[i];
                if (target == null || target == layer) continue;
                if (target.id == layer.autoFitTo)
                {
                    var p = layer.autoFitPadding;
                    float xMin = target.rect.xMin - p.x;
                    float yMin = target.rect.yMin - p.y;
                    float xMax = target.rect.xMax + p.z;
                    float yMax = target.rect.yMax + p.w;
                    return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
                }
            }

            // Dangling id (target renamed/removed) — degrade to the authored rect rather than throw.
            return layer.rect;
        }
    }
}
