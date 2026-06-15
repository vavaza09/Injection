using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

public class RespawnCoordinator : MonoBehaviour
{
    [SerializeField] private float deathDisplayDuration = 1f;

    private Player _player;

    [Inject]
    public void Construct(Player player)
    {
        _player = player;
    }

    private void Start()
    {
        if (_player != null)
            _player.Died += OnPlayerDied;
    }

    private void OnDestroy()
    {
        if (_player != null)
            _player.Died -= OnPlayerDied;
    }

    private void OnPlayerDied(character _)
    {
        StartCoroutine(RespawnSequence());
    }

    private IEnumerator RespawnSequence()
    {
        yield return new WaitForSecondsRealtime(deathDisplayDuration);

        Time.timeScale = 1f;

        if (ScreenFader.Instance != null)
        {
            bool fadeComplete = false;
            ScreenFader.Instance.FadeOut(() => fadeComplete = true);
            yield return new WaitUntil(() => fadeComplete);
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
