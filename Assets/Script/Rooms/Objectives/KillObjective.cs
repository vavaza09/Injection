using UnityEngine;
using Game.Rooms.Objectives;

public sealed class KillObjective : RoomObjective
{
    [SerializeField] private string objectiveId;

    private readonly KillObjectiveTracker _tracker = new KillObjectiveTracker();

    public string ObjectiveId => objectiveId;

    public void AddEnemy(Enemy enemy)
    {
        if (enemy == null) return;
        _tracker.Register();
        enemy.Died += OnEnemyDied;
    }

    public void Seal()
    {
        _tracker.Seal();
        if (_tracker.IsComplete)
            Complete();
    }

    protected override void OnBind() { }

    private void OnEnemyDied(character c)
    {
        _tracker.MarkDead();
        if (_tracker.IsComplete)
            Complete();
    }
}
