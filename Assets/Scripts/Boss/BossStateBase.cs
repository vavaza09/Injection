public abstract class BossStateBase
{
    protected BossBase boss;

    protected BossStateBase(BossBase boss) => this.boss = boss;

    public abstract void Enter();
    public abstract void StateUpdate();
    public abstract void Exit();
}
