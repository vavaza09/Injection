using UnityEngine;
using UnityEngine.Rendering;

public class PlayerAimFeedback : MonoBehaviour
{
    [SerializeField] private Volume aimVolume;
    [SerializeField, Range(0f, 1f)] private float weightMax = 1f;
    [SerializeField] private float fadeTime = 0.15f;

    public Volume AimVolume => aimVolume;

    private float _weightVelocity;
    private bool _aiming;

    public void SetAiming(bool aiming) => _aiming = aiming;

    private void Update()
    {
        if (aimVolume == null) return;

        float target = _aiming ? weightMax : 0f;
        aimVolume.weight = Mathf.SmoothDamp(aimVolume.weight, target,
            ref _weightVelocity, fadeTime, float.MaxValue, Time.unscaledDeltaTime);
    }
}
