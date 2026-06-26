using UnityEngine;
using Game.Characters.Player;

public class VictoryScreenController : MonoBehaviour
{
    [SerializeField] private GameObject victoryRoot;
    [SerializeField] private string titleSceneName = "TitleScene";

    private void OnEnable()
    {
        BossDeathSequence.Completed += ShowVictory;
    }

    private void OnDisable()
    {
        BossDeathSequence.Completed -= ShowVictory;
    }

    private void ShowVictory()
    {
        victoryRoot.SetActive(true);
        Time.timeScale = 0f;
        SetPlayerInput(false);
        SoundManager.PlayMusic(MusicType.MENU);
    }

    public void OnContinuePlaying()
    {
        SoundManager.PlaySound(SoundType.UI_CLICK);
        Time.timeScale = 1f;
        SetPlayerInput(true);
        victoryRoot.SetActive(false);
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
