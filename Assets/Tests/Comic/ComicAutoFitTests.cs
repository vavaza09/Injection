using NUnit.Framework;
using UnityEngine;
using Game.Comic;

namespace Game.Tests.Comic
{
    public class ComicAutoFitTests
    {
        [Test]
        public void Resolve_NoAutoFitTo_ReturnsAuthoredRect()
        {
            var panel = new ComicPanel();
            var layer = new ComicLayer { autoFitTo = "", rect = new Rect(10, 20, 30, 40) };
            panel.layers.Add(layer);

            var result = ComicAutoFit.Resolve(panel, layer);

            Assert.AreEqual(layer.rect, result);
        }

        [Test]
        public void Resolve_WithTarget_ExpandsByPadding()
        {
            var panel = new ComicPanel();
            var target = new ComicLayer { id = "dialog", rect = new Rect(100, 100, 200, 60) };
            var bubble = new ComicLayer { id = "bubble", autoFitTo = "dialog", autoFitPadding = new Vector4(10, 5, 10, 5) };
            panel.layers.Add(target);
            panel.layers.Add(bubble);

            var result = ComicAutoFit.Resolve(panel, bubble);

            Assert.AreEqual(90f, result.xMin, 0.001f);
            Assert.AreEqual(95f, result.yMin, 0.001f);
            Assert.AreEqual(310f, result.xMax, 0.001f);
            Assert.AreEqual(165f, result.yMax, 0.001f);
        }

        [Test]
        public void Resolve_DanglingId_DegradesToAuthoredRect()
        {
            var panel = new ComicPanel();
            var layer = new ComicLayer { id = "bubble", autoFitTo = "does_not_exist", rect = new Rect(1, 2, 3, 4) };
            panel.layers.Add(layer);

            var result = ComicAutoFit.Resolve(panel, layer);

            Assert.AreEqual(layer.rect, result);
        }

        [Test]
        public void Resolve_NullPanel_ReturnsAuthoredRect()
        {
            var layer = new ComicLayer { autoFitTo = "x", rect = new Rect(5, 6, 7, 8) };
            var result = ComicAutoFit.Resolve(null, layer);
            Assert.AreEqual(layer.rect, result);
        }

        [Test]
        public void Resolve_NullLayer_ReturnsDefault()
        {
            var result = ComicAutoFit.Resolve(new ComicPanel(), null);
            Assert.AreEqual(default(Rect), result);
        }

        [Test]
        public void Resolve_IgnoresSelfReference()
        {
            var panel = new ComicPanel();
            var layer = new ComicLayer { id = "self", autoFitTo = "self", rect = new Rect(1, 1, 1, 1) };
            panel.layers.Add(layer);

            var result = ComicAutoFit.Resolve(panel, layer);

            // A layer can't auto-fit to itself — degrades to the authored rect rather than
            // matching itself trivially (which the "target == layer" skip in Resolve prevents).
            Assert.AreEqual(layer.rect, result);
        }
    }
}
