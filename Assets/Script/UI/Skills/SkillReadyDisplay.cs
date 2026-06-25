using Game.Components.Skills;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Skills
{
    public class SkillReadyDisplay : MonoBehaviour
    {
        [SerializeField] private Image cooldownRadial;

        [SerializeField] [Range(0f, 1f)] private float notReadyBrightness = 0.35f;
        [SerializeField] [Range(0f, 1f)] private float lockedBrightness   = 0.15f;
        [SerializeField] [Range(0f, 1f)] private float lockedAlpha        = 0.5f;

        private Image[] _allImages;
        private Color[] _originalColors;

        private void Awake()
        {
            _allImages      = GetComponentsInChildren<Image>(true);
            _originalColors = new Color[_allImages.Length];
            for (int i = 0; i < _allImages.Length; i++)
                _originalColors[i] = _allImages[i].color;
        }

        public void Refresh(SkillReadout readout)
        {
            float bright = readout.Unlocked
                ? (readout.CanCast ? 1f : notReadyBrightness)
                : lockedBrightness;
            float alpha = readout.Unlocked ? 1f : lockedAlpha;

            for (int i = 0; i < _allImages.Length; i++)
            {
                Color orig = _originalColors[i];
                _allImages[i].color = new Color(
                    orig.r * bright,
                    orig.g * bright,
                    orig.b * bright,
                    orig.a * alpha);
            }

            if (cooldownRadial != null)
                cooldownRadial.fillAmount = readout.CooldownDuration > 0f
                    ? readout.CooldownRemaining / readout.CooldownDuration
                    : 0f;
        }

        public void SetVisible(bool visible) => gameObject.SetActive(visible);
    }
}
