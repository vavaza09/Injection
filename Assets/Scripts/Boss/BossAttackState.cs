using UnityEngine;

public class BossAttackState : BossStateBase
{
    private readonly BossAttackBase _attack;
    private Coroutine _routine;

    public BossAttackState(BossBase boss, BossAttackBase attack) : base(boss)
    {
        _attack = attack;
    }

    public override void Enter()
    {
        _routine = boss.StartCoroutine(_attack.Execute());
    }

    public override void StateUpdate() { }

    public override void Exit()
    {
        if (_routine != null)
        {
            boss.StopCoroutine(_routine);
            _routine = null;
        }
        _attack.Reset();
    }
}
