using NUnit.Framework;
using Game.Comic;

namespace Game.Tests.Comic
{
    public class InlineComicTextProviderTests
    {
        [Test]
        public void Resolve_EmptyLocKey_ReturnsInlineFallback()
        {
            var result = InlineComicTextProvider.Instance.Resolve("", "Hello there");
            Assert.AreEqual("Hello there", result);
        }

        [Test]
        public void Resolve_NonEmptyLocKey_StillReturnsInlineFallback()
        {
            // InlineComicTextProvider never resolves keys — it's the zero-localization default,
            // guaranteeing the game works before any translation pass exists.
            var result = InlineComicTextProvider.Instance.Resolve("comic.p01.b02", "Hello there");
            Assert.AreEqual("Hello there", result);
        }
    }
}
