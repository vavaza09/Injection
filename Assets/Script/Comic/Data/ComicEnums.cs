namespace Game.Comic
{
    public enum ComicLayerKind
    {
        Sprite,
        Text
    }

    public enum TextRevealMode
    {
        Instant,
        Typewriter
    }

    public enum IdleKind
    {
        None,
        Drift,
        Bob,
        Pulse,
        Pan,
        Zoom
    }

    public enum ComicTransitionKind
    {
        Cut,
        FadeToBlack,
        CrossFade,
        Push
    }
}
