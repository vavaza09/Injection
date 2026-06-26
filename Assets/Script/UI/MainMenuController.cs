using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Game.Persistence;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button continueButton;
    [SerializeField] private GameObject newGameConfirmPanel;

    private void Start()
    {
        settingsPanel.SetActive(false);
        newGameConfirmPanel.SetActive(false);
        continueButton.gameObject.SetActive(SaveFileLocator.Exists());
        SoundManager.PlayMusic(MusicType.MENU);
    }

    public void OnContinue()
    {
        SoundManager.PlaySound(SoundType.UI_CLICK);
        SceneManager.LoadScene("Bootstrap");
    }

    public void OnNewGame()
    {
        SoundManager.PlaySound(SoundType.UI_CLICK);
        if (SaveFileLocator.Exists())
            newGameConfirmPanel.SetActive(true);
        else
            StartFreshGame();
    }

    public void ConfirmNewGame()
    {
        SoundManager.PlaySound(SoundType.UI_CLICK);
        SaveFileLocator.Delete();
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
