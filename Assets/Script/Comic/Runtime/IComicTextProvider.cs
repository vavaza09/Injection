namespace Game.Comic
{
    /// <summary>
    /// Localization seam for comic dialogue. The data model always carries the inline authored
    /// string as a guaranteed fallback, so the game works with zero localization wired up —
    /// this interface only needs to exist the day a translation pass actually starts.
    /// </summary>
    public interface IComicTextProvider
    {
        /// <summary>Return the localized string for locKey, or inlineFallback if locKey is
        /// empty or unresolved.</summary>
        string Resolve(string locKey, string inlineFallback);
    }

    /// <summary>Default provider: always returns the inline authored text.</summary>
    public sealed class InlineComicTextProvider : IComicTextProvider
    {
        public static readonly InlineComicTextProvider Instance = new InlineComicTextProvider();

        public string Resolve(string locKey, string inlineFallback) => inlineFallback;
    }
}
