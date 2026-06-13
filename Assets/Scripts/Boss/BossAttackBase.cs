using System.Collections;

public abstract class BossAttackBase
{
    protected BossBase boss;

    protected BossAttackBase(BossBase boss) => this.boss = boss;

    public abstract IEnumerator Execute();

    // Called when the attack is interrupted mid-swing so the arm snaps back cleanly
    public virtual void Reset() { }
}
