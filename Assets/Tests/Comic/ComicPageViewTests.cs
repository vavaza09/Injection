using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Comic;

namespace Game.Tests.Comic
{
    public class ComicPageViewTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        private RectTransform NewParent()
        {
            var go = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas));
            _spawned.Add(go);
            return (RectTransform)go.transform;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
                if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        private static ComicPage BuildTestPage()
        {
            var page = new ComicPage { name = "P1" };

            var panelA = new ComicPanel { name = "A", beatIndex = 0, rect = new Rect(0, 0, 400, 300) };
            var panelB = new ComicPanel { name = "B", beatIndex = 1, rect = new Rect(500, 0, 400, 300) };
            page.panels.Add(panelA);
            page.panels.Add(panelB);

            return page;
        }

        [Test]
        public void ApplyBeat_HidesPanelsWithHigherBeatIndex()
        {
            var page = BuildTestPage();
            var view = ComicPageBuilder.Build(page, new ComicStyle(), NewParent());

            view.ApplyBeat(0, false);

            Assert.IsTrue(view.Root.Find("A").gameObject.activeSelf);
            Assert.IsFalse(view.Root.Find("B").gameObject.activeSelf);
        }

        [Test]
        public void ApplyBeat_SequentialWalk_MatchesDirectJump()
        {
            const int target = 3;
            var page = BuildTestPage();
            page.panels.Add(new ComicPanel { name = "C", beatIndex = 2, rect = new Rect(0, 400, 400, 300) });
            page.panels[1].layers.Add(new ComicLayer { id = "late", kind = ComicLayerKind.Sprite, beatIndex = target, rect = new Rect(0, 0, 50, 50) });

            var walked = ComicPageBuilder.Build(page, new ComicStyle(), NewParent());
            for (int b = 0; b <= target; b++)
                walked.ApplyBeat(b, true);

            var jumped = ComicPageBuilder.Build(page, new ComicStyle(), NewParent());
            jumped.ApplyBeat(target, false);

            foreach (var name in new[] { "A", "B", "C" })
            {
                bool walkedActive = walked.Root.Find(name).gameObject.activeSelf;
                bool jumpedActive = jumped.Root.Find(name).gameObject.activeSelf;
                Assert.AreEqual(jumpedActive, walkedActive, $"Panel '{name}' visibility diverged between sequential walk and direct jump.");
            }

            bool walkedLayerActive = walked.Root.Find("B/Clip/late").gameObject.activeSelf;
            bool jumpedLayerActive = jumped.Root.Find("B/Clip/late").gameObject.activeSelf;
            Assert.AreEqual(jumpedLayerActive, walkedLayerActive);
        }

        [Test]
        public void ApplyBeat_FocusedPanelIsNotDimmed_EarlierPanelIsDimmed()
        {
            var page = BuildTestPage();
            var style = new ComicStyle { dimAmount = 0.6f };
            var view = ComicPageBuilder.Build(page, style, NewParent());

            view.ApplyBeat(1, false);

            var dimA = view.Root.Find("A/Clip/DimOverlay").GetComponent<Image>();
            var dimB = view.Root.Find("B/Clip/DimOverlay").GetComponent<Image>();

            Assert.AreEqual(0.6f, dimA.color.a, 0.001f, "Panel A (earlier) should be dimmed once B is focused.");
            Assert.AreEqual(0f, dimB.color.a, 0.001f, "Panel B (current focus) should not be dimmed.");
        }

        [Test]
        public void ApplyBeat_NegativeBeat_HidesEverything()
        {
            var page = BuildTestPage();
            var view = ComicPageBuilder.Build(page, new ComicStyle(), NewParent());

            view.ApplyBeat(-1, false);

            Assert.IsFalse(view.Root.Find("A").gameObject.activeSelf);
            Assert.IsFalse(view.Root.Find("B").gameObject.activeSelf);
        }

        [Test]
        public void SnapCurrentBeat_ForcesAnimatingLayerToSettledState()
        {
            var page = new ComicPage { name = "P1" };
            var panel = new ComicPanel
            {
                name = "A",
                beatIndex = 0,
                rect = new Rect(0, 0, 400, 300),
                entrance = new ComicTween { duration = 10f, fromAlpha = 0f }
            };
            page.panels.Add(panel);

            var view = ComicPageBuilder.Build(page, new ComicStyle(), NewParent());
            view.ApplyBeat(0, true);

            Assert.IsTrue(view.IsAnimating);
            view.SnapCurrentBeat();
            Assert.IsFalse(view.IsAnimating);
        }

        [Test]
        public void TextLayer_ResolvesInlineText_WhenNoProviderGiven()
        {
            var page = new ComicPage { name = "P1" };
            var panel = new ComicPanel { name = "A", beatIndex = 0, rect = new Rect(0, 0, 400, 300) };
            panel.layers.Add(new ComicLayer
            {
                id = "dialog",
                kind = ComicLayerKind.Text,
                beatIndex = 0,
                text = "Hello there",
                rect = new Rect(0, 0, 200, 60)
            });
            page.panels.Add(panel);

            var view = ComicPageBuilder.Build(page, new ComicStyle(), NewParent());
            view.ApplyBeat(0, false);

            var tmp = view.Root.Find("A/Clip/dialog").GetComponent<TextMeshProUGUI>();
            Assert.AreEqual("Hello there", tmp.text);
            // Default reveal mode is Instant — always fully shown, not gated by character count.
            Assert.AreEqual(int.MaxValue, tmp.maxVisibleCharacters);
        }

        [Test]
        public void TextLayer_Typewriter_StartsAtZeroVisibleCharactersOnFreshBeat()
        {
            var page = new ComicPage { name = "P1" };
            var panel = new ComicPanel { name = "A", beatIndex = 0, rect = new Rect(0, 0, 400, 300) };
            panel.layers.Add(new ComicLayer
            {
                id = "dialog",
                kind = ComicLayerKind.Text,
                beatIndex = 0,
                text = "Hello",
                reveal = TextRevealMode.Typewriter,
                rect = new Rect(0, 0, 200, 60)
            });
            page.panels.Add(panel);

            var view = ComicPageBuilder.Build(page, new ComicStyle(), NewParent());
            view.ApplyBeat(0, true);

            var tmp = view.Root.Find("A/Clip/dialog").GetComponent<TextMeshProUGUI>();
            Assert.AreEqual(0, tmp.maxVisibleCharacters);

            view.SnapCurrentBeat();
            Assert.AreEqual("Hello".Length, tmp.maxVisibleCharacters);
        }

        [Test]
        public void PanelRotation_AppliesToRoot()
        {
            var page = new ComicPage { name = "P1" };
            page.panels.Add(new ComicPanel { name = "A", beatIndex = 0, rect = new Rect(0, 0, 400, 300), rotation = 6f });

            var view = ComicPageBuilder.Build(page, new ComicStyle(), NewParent());
            view.ApplyBeat(0, false);

            var root = view.Root.Find("A");
            Assert.AreEqual(6f, root.localEulerAngles.z, 0.001f);
        }
    }
}
