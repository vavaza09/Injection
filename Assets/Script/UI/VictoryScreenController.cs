using UnityEngine;
using Game.Pause;
using Game.UI;

public class VictoryScreenController : MonoBehaviour
{
    private const string PauseHandle = "victoryScreen";

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
        if (victoryRoot == null) { Debug.LogWarning("[VictoryScreenController] victoryRoot is not assigned.", this); return; }
        victoryRoot.SetActive(true);
        PauseStack.Instance.Push(PauseHandle);
        PlayerInputGate.Set(false);
        SoundManager.PlayMusic(MusicType.MENU);
    }

    public void OnContinuePlaying()
    {
        SoundManager.PlaySound(SoundType.UI_CLICK);
        PauseStack.Instance.Release(PauseHandle);
        PlayerInputGate.Set(true);
        victoryRoot.SetActive(false);
    }

    public void OnBackToTitle()
    {
        SoundManager.PlaySound(SoundType.UI_CLICK);
        Game.UI.SessionTeardown.ReturnToTitle(titleSceneName);
    }
}
