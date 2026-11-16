// TitleBackgroundView.cs
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Title_Background.png를 풀스크린으로 출력하고 은은한 카메라 호흡(미세 줌) 연출을 수행하는 배경 뷰
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class TitleBackgroundView : UIBase
{
    [Header("Background Visuals")]
    [SerializeField] private Image _backgroundImage;
    [SerializeField] private RectTransform _motionContainer;

    private float _zoomAmplitude = 0.03f;
    private float _zoomSpeed = 0.35f;
    private float _timer = 0f;

    public override void InitializeView()
    {
        base.InitializeView();
        Identifier = UIId.TitleBackgroundUI;
        Layer = UILayer.Background;
    }

    public void ConfigureMotion(float amplitude, float speed)
    {
        _zoomAmplitude = amplitude;
        _zoomSpeed = speed;
    }

    private void Update()
    {
        if (!IsVisible || _motionContainer == null) return;

        _timer += Time.unscaledDeltaTime * _zoomSpeed;
        float scale = 1.0f + ((Mathf.Sin(_timer) + 1f) * 0.5f * _zoomAmplitude);
        _motionContainer.localScale = new Vector3(scale, scale, 1f);
    }
}