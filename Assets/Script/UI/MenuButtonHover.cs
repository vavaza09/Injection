using UnityEngine;
using UnityEngine.EventSystems;

public class MenuButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private RectTransform target;
    [SerializeField] private float hoverScale = 1.12f;
    [SerializeField] private float lerpSpeed = 10f;
    [SerializeField] private GameObject[] revealOnHover;

    private Vector3 _idleScale;
    private Vector3 _targetScale;

    private void Awake()
    {
        if (target == null) target = GetComponent<RectTransform>();
        _idleScale = target.localScale;
        _targetScale = _idleScale;
    }

    private void OnDisable()
    {
        _targetScale = _idleScale;
        if (target != null) target.localScale = _idleScale;
        SetRevealed(false);
    }

    private void Update()
    {
        target.localScale = Vector3.Lerp(target.localScale, _targetScale, Time.unscaledDeltaTime * lerpSpeed);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _targetScale = _idleScale * hoverScale;
        SetRevealed(true);
        SoundManager.PlaySound(SoundType.UI_HOVER);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _targetScale = _idleScale;
        SetRevealed(false);
    }

    private void SetRevealed(bool active)
    {
        foreach (var go in revealOnHover)
            if (go != null) go.SetActive(active);
    }
}
