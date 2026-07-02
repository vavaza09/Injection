using UnityEngine;
using UnityEngine.Rendering;
using Game.Components.Skills;

// Attach to the Player root. Assign empVolume and trueDamageVolume (child Volume components)
// in the Inspector. Each volume's weight is set to 1 when that skill is ready to cast.
public class SkillReadyVolume : MonoBehaviour
{
    [SerializeField] private Volume empVolume;
    [SerializeField] private Volume trueDamageVolume;

    private ISkillReadinessProvider _provider;

    private void Start()
    {
        _provider = GetComponent<ISkillReadinessProvider>();

        if (empVolume != null)        empVolume.weight        = 0f;
        if (trueDamageVolume != null) trueDamageVolume.weight = 0f;
    }

    private void Update()
    {
        if (_provider == null) return;

        if (empVolume != null)
            empVolume.weight = _provider.Get(SkillId.Emp).CanCast ? 1f : 0f;

        if (trueDamageVolume != null)
            trueDamageVolume.weight = _provider.Get(SkillId.TrueDamage).CanCast ? 1f : 0f;
    }
}
