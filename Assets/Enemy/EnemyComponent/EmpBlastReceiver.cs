using System.Collections.Generic;
using UnityEngine;
using VContainer;
using Game.Components.Skills;
using Core.Logging;

public class EmpBlastReceiver : MonoBehaviour
{
    private IPlayerSkillEvents _skillEvents;
    private Core.Logging.ILogger _logger;

    [Inject]
    public void Construct(Core.Logging.LoggerFactory loggerFactory, IPlayerSkillEvents skillEvents)
    {
        _logger      = loggerFactory?.CreateLogger<EmpBlastReceiver>();
        _skillEvents = skillEvents;
    }

    private void Start()
    {
        if (_skillEvents != null)
            _skillEvents.EmpDetonated += OnEmpDetonated;
    }

    private void OnDestroy()
    {
        if (_skillEvents != null)
            _skillEvents.EmpDetonated -= OnEmpDetonated;
    }

    private void OnEmpDetonated(EmpBlastEvent e)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(e.Position, e.Radius);

        var stunned = new HashSet<Enemy>();
        foreach (Collider2D col in hits)
        {
            Enemy enemy = col.GetComponentInParent<Enemy>();
            if (enemy == null || !stunned.Add(enemy)) continue;
            enemy.Stun(e.StunDuration);
        }

        _logger?.Log($"[EMP] Stunned {stunned.Count} enemies at {e.Position} (r={e.Radius}, t={e.StunDuration}s)");
    }
}
