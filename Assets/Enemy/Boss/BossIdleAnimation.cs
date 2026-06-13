using UnityEngine;

public class BossIdleAnimation : MonoBehaviour
{
    [Header("Arm References")]
    [SerializeField] private Transform upperR;
    [SerializeField] private Transform upperL;
    [SerializeField] private Transform clawR;
    [SerializeField] private Transform hammerL;

    [Header("Body / Head References")]
    [SerializeField] private Transform bossBody;
    [SerializeField] private Transform bossHead;

    [Header("Bob Settings")]
    [SerializeField] private float bobSpeed = 1.5f;
    [SerializeField] private float bobAmplitude = 0.05f;
    [SerializeField] private float phaseOffset = 0.3f;

    private float _baseYR;
    private float _baseYL;
    private float _baseYBody;
    private float _baseYHead;
    private bool  _leftArmPaused;

    public void PauseLeftArm()  => _leftArmPaused = true;
    public void ResumeLeftArm() => _leftArmPaused = false;

    private void Start()
    {
        ResolveByName();
        ReparentArms();

        _baseYR    = upperR.localPosition.y;
        _baseYL    = upperL.localPosition.y;
        if (bossBody != null) _baseYBody = bossBody.localPosition.y;
        if (bossHead != null) _baseYHead = bossHead.localPosition.y;
    }

    private void Update()
    {
        float t = Time.time * bobSpeed;

        SetLocalY(upperR, _baseYR + Mathf.Sin(t) * bobAmplitude);

        if (!_leftArmPaused)
            SetLocalY(upperL, _baseYL + Mathf.Sin(t + phaseOffset) * bobAmplitude);

        if (bossBody != null)
            SetLocalY(bossBody, _baseYBody + Mathf.Sin(t + phaseOffset * 2f) * bobAmplitude);

        if (bossHead != null)
            SetLocalY(bossHead, _baseYHead + Mathf.Sin(t + phaseOffset * 3f) * bobAmplitude);
    }

    private void ResolveByName()
    {
        if (upperR  == null) upperR  = FindChildByName(transform, "Boss_Upper_R");
        if (upperL  == null) upperL  = FindChildByName(transform, "Boss_Upper_L");
        if (clawR   == null) clawR   = FindChildByName(transform, "Boss_Claw(R)");
        if (hammerL == null) hammerL = FindChildByName(transform, "Boss_Hammer(L)");
        if (bossBody == null) bossBody = FindChildByName(transform, "Boss_Body");
        if (bossHead == null) bossHead = FindChildByName(transform, "Boss_Head");
    }

    private void ReparentArms()
    {
        if (clawR != null && upperR != null && clawR.parent != upperR)
            clawR.SetParent(upperR, true);

        if (hammerL != null && upperL != null && hammerL.parent != upperL)
            hammerL.SetParent(upperL, true);
    }

    private static void SetLocalY(Transform t, float y)
    {
        Vector3 pos = t.localPosition;
        pos.y = y;
        t.localPosition = pos;
    }

    private static Transform FindChildByName(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name) return child;
        }
        return null;
    }
}
