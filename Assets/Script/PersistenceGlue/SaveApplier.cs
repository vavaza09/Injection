using System.Collections;
using UnityEngine;
using VContainer;
using Game.Persistence;
using Game.Components.Skills;

public class SaveApplier : MonoBehaviour
{
    [SerializeField] private float respawnInvincDuration = 2f;

    private SaveService _saveService;
    private Player _player;
    private IEnergyStore _energy;

    [Inject]
    public void Construct(SaveService saveService, Player player, IEnergyStore energy)
    {
        _saveService = saveService;
        _player = player;
        _energy = energy;
    }

    private void Start()
    {
        StartCoroutine(ApplyNextFrame());
    }

    private IEnumerator ApplyNextFrame()
    {
        yield return null;

        var data = _saveService?.Load();

        if (data == null)
        {
            ScreenFader.Instance?.FadeIn();
            yield break;
        }

        if (!string.IsNullOrEmpty(data.spawnPointId))
        {
            var triggers = FindObjectsByType<SavePointTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in triggers)
            {
                if (t.Id == data.spawnPointId)
                {
                    if (_player != null)
                        _player.transform.position = t.SpawnTransform.position;
                    break;
                }
            }
        }

        _player?.Respawn(respawnInvincDuration);
        _energy?.SetCurrent(data.playerEnergy);

        ScreenFader.Instance?.FadeIn();
    }
}
