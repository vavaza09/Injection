using UnityEngine;
using Game.Characters.Player;

// Shows the "end of demo" screen: pauses the game, disables player input, and offers
// links back to itch.io for feedback. Mirrors VictoryScreenController's pause/resume pattern.
public class EndOfDemoController : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private string itchIoUrl = "https://networktha.itch.io/blue-steam";
    [SerializeField] private string titleSceneName = "TitleScene";

    public void Show()
    {
        if (panelRoot == null) { Debug.LogWarning("[EndOfDemoController] panelRoot is not assigned.", this); return; }
        panelRoot.SetActive(true);
        Time.timeScale = 0f;
        SetPlayerInput(false);
        SoundManager.PlayMusic(MusicType.MENU);
    }

    public void OnOpenItchIo()
    {
        SoundManager.PlaySound(SoundType.UI_CLICK);
        if (!string.IsNullOrEmpty(itchIoUrl))
            Application.OpenURL(itchIoUrl);
    }

    public void OnContinueExploring()
    {
        SoundManager.PlaySound(SoundType.UI_CLICK);
        Time.timeScale = 1f;
        SetPlayerInput(true);
        panelRoot.SetActive(false);
    }

    public void OnBackToTitle()
    {
        SoundManager.PlaySound(SoundType.UI_CLICK);
        Game.UI.SessionTeardown.ReturnToTitle(titleSceneName);
    }

    private void SetPlayerInput(bool on)
    {
        var playerGO = GameObject.FindWithTag("Player");
        playerGO?.GetComponent<Player>()?.SetInputEnabled(on);
    }
}
