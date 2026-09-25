using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Game.Persistence;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button continueButton;
    [SerializeField] private GameObject newGameConfirmPanel;
    // Hidden on WebGL: Application.Quit() is a no-op inside a browser tab.
    [SerializeField] private GameObject quitButton;

    // Created in Awake, not a field initializer: JsonFileSaveStorage reads
    // Application.persistentDataPath, which Unity forbids during MonoBehaviour construction.
    private ISaveStorage _saveStorage;

    private void Awake()
    {
        _saveStorage = SaveStorageFactory.CreateDefault();
    }

    private void Start()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (quitButton != null)
        {
            quitButton.SetActive(false);
        }
#endif
        settingsPanel.SetActive(false);
        newGameConfirmPanel.SetActive(false);
        continueButton.gameObject.SetActive(_saveStorage.Exists());
        SoundManager.PlayMusic(MusicType.MENU);

        ScreenFader.Instance?.FadeIn();
    }

    public void OnContinue()
    {
        SoundManager.PlaySound(SoundType.UI_CLICK);
        SceneManager.LoadScene("Bootstrap");
    }

    public void OnNewGame()
    {
        SoundManager.PlaySound(SoundType.UI_CLICK);
        if (_saveStorage.Exists())
            newGameConfirmPanel.SetActive(true);
        else
            StartFreshGame();
    }

    public void ConfirmNewGame()
    {
        SoundManager.PlaySound(SoundType.UI_CLICK);
        _saveStorage.Clear();
        StartFreshGame();
    }

    public void CancelNewGame()
    {
        SoundManager.PlaySound(SoundType.UI_CLICK);
        newGameConfirmPanel.SetActive(false);
    }

    private void StartFreshGame()
    {
        SceneManager.LoadScene("Bootstrap");
    }

    public void OnQuit()
    {
        SoundManager.PlaySound(SoundType.UI_CLICK);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OpenSettings()
    {
        SoundManager.PlaySound(SoundType.UI_CLICK);
        settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        SoundManager.PlaySound(SoundType.UI_CLICK);
        settingsPanel.SetActive(false);
    }
}
