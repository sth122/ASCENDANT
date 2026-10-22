public class PlayerHUDView : UIBase
{
    [UnityEngine.Header("Sliders")]
    [UnityEngine.SerializeField] private UnityEngine.UI.Slider _hpSlider;
    [UnityEngine.SerializeField] private UnityEngine.UI.Slider _staminaSlider;

    public void UpdateHpBar(float current, float max)
    {
        if (_hpSlider != null && max > 0f)
        {
            _hpSlider.value = UnityEngine.Mathf.Clamp01(current / max);
        }
    }

    public void UpdateStaminaBar(float current, float max)
    {
        if (_staminaSlider != null && max > 0f)
        {
            _staminaSlider.value = UnityEngine.Mathf.Clamp01(current / max);
        }
    }
}