using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 버튼 호버/클릭 시 SFX 재생 및 스케일 강조 연출을 제공하는 컴포넌트
/// </summary>
[RequireComponent(typeof(Selectable))]
public class UIButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float _hoverScale = 1.04f;
    [SerializeField] private float _pressScale = 0.97f;
    [SerializeField] private float _lerpSpeed = 14f;

    private Selectable _selectable;
    private Vector3 _targetScale = Vector3.one;

    private void Awake()
    {
        _selectable = GetComponent<Selectable>();
    }

    private void OnEnable()
    {
        _targetScale = Vector3.one;
        transform.localScale = Vector3.one;
    }

    private void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, Time.unscaledDeltaTime * _lerpSpeed);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_selectable != null && !_selectable.interactable) return;
        _targetScale = Vector3.one * _hoverScale;
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySFX(SoundId.UI_Hover);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _targetScale = Vector3.one;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_selectable != null && !_selectable.interactable) return;
        _targetScale = Vector3.one * _pressScale;
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySFX(SoundId.UI_Click);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_selectable != null && !_selectable.interactable) return;
        _targetScale = Vector3.one * _hoverScale;
    }
}