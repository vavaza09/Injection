using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Health
{
    public class HealthSlotDisplay : MonoBehaviour
    {
        [SerializeField] private Image[] slots;
        [SerializeField] private Color filledColor = Color.white;
        [SerializeField] private Color emptyColor  = new Color(0.2f, 0.2f, 0.2f, 0.5f);

        public void Refresh(int remaining, int max)
        {
            if (slots == null) return;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                slots[i].gameObject.SetActive(i < max);
                slots[i].color = i < remaining ? filledColor : emptyColor;
            }
        }

        public void SetVisible(bool visible) => gameObject.SetActive(visible);
    }
}
