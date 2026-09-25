using Game.Persistence;

// Single place that picks the platform's save backend, shared by RootLifetimeScope (the session)
// and MainMenuController (TitleScene has no DI scope, but must know whether a save exists).
public static class SaveStorageFactory
{
    public static ISaveStorage CreateDefault()
    {
        if (BrowserStorage.IsAvailable)
        {
            return new BrowserSaveStorage(SaveFileLocator.FileName);
        }
        return new JsonFileSaveStorage(SaveFileLocator.FileName);
    }
}
