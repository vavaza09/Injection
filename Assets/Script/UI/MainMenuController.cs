using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private GameObject settingsPanel;

    private void Start()
    {
        settingsPanel.SetActive(false);
        SoundManager.PlayMusic(MusicType.MENU);
    }

    public void OnStart()
    {
        SoundManager.PlaySound(SoundType.UI_CLICK);
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
