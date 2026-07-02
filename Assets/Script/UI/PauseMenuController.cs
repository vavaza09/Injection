using UnityEngine;
using UnityEngine.InputSystem;
using Game.Characters.Player;

public class PauseMenuController : MonoBehaviour
{
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private string titleSceneName = "TitleScene";

    private bool _isPaused;
    private bool _settingsOpen;
    private InputAction _pauseAction;

    private void Awake()
    {
        Debug.Log("[Pause] Awake");
        _pauseAction = new InputAction("Pause", InputActionType.Button);
        _pauseAction.AddBinding("<Keyboard>/escape");
        _pauseAction.AddBinding("<Gamepad>/start");
        _pauseAction.performed += OnPausePressed;
        _pauseAction.Enable();
        Debug.Log("[Pause] InputAction enabled, controls=" + _pauseAction.controls.Count);
    }

    private void Start()
    {
        Debug.Log("[Pause] Start, menuRoot=" + (menuRoot == null ? "NULL" : menuRoot.name));
        menuRoot.SetActive(false);
        settingsPanel.SetActive(false);
        _isPaused = false;
        _settingsOpen = false;
    }

    private void OnPausePressed(InputAction.CallbackContext ctx)
    {
        Debug.Log("[Pause] OnPausePressed isPaused=" + _isPaused + " settingsOpen=" + _settingsOpen);
        if (_settingsOpen)
            CloseSettings();
        else if (_isPaused)
            Resume();
        else
            Pause();
    }

    public void Pause()
    {
        Time.timeScale = 0f;
        menuRoot.SetActive(true);
        SetPlayerInput(false);
        _isPaused = true;
    }

    public void Resume()
    {
        SoundManager.PlaySound(SoundType.UI_CLICK);
        Time.timeScale = 1f;
        menuRoot.SetActive(false);
        settingsPanel.SetActive(false);
        SetPlayerInput(true);
        _isPaused = false;
        _settingsOpen = false;
    }

    public void OpenSettings()
    {
        SoundManager.PlaySound(SoundType.UI_CLICK);
        settingsPanel.SetActive(true);
        _settingsOpen = true;
    }

    public void CloseSettings()
    {
        SoundManager.PlaySound(SoundType.UI_CLICK);
        settingsPanel.SetActive(false);
        _settingsOpen = false;
    }

    public void QuitToTitle()
    {
        SoundManager.PlaySound(SoundType.UI_CLICK);
        Game.UI.SessionTeardown.ReturnToTitle(titleSceneName);
    }

    private void SetPlayerInput(bool on)
    {
        var playerGO = GameObject.FindWithTag("Player");
        playerGO?.GetComponent<Player>()?.SetInputEnabled(on);
    }

    private void OnDestroy()
    {
        if (_pauseAction != null)
        {
            _pauseAction.performed -= OnPausePressed;
            _pauseAction.Disable();
            _pauseAction.Dispose();
        }
    }
}
