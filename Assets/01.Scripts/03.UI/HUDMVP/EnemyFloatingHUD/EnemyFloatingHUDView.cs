/// <summary>
/// 일반 Enemy 머리 위에 표시되는 스크린 스페이스 플로팅 HUD 뷰
/// HP 잔상 애니메이션, Groggy 상태 판단용 Poise 게이지, 카메라-벽 차폐 숨김을 제공
/// </summary>
public class EnemyFloatingHUDView : UIBase
{
    [UnityEngine.Header("HP Sliders")]
    [UnityEngine.SerializeField] private UnityEngine.UI.Slider _hpSlider;
    [UnityEngine.SerializeField] private UnityEngine.UI.Slider _hpGhostSlider;

    [UnityEngine.Header("Poise Sliders")]
    [UnityEngine.SerializeField] private UnityEngine.UI.Slider _poiseSlider;
    [UnityEngine.SerializeField] private UnityEngine.UI.Image _poiseFillImage;
    [UnityEngine.SerializeField] private UnityEngine.Color _normalPoiseColor = new UnityEngine.Color(0.95f, 0.75f, 0.15f, 1f);
    [UnityEngine.SerializeField] private UnityEngine.Color _groggyPoiseColor = new UnityEngine.Color(0.9f, 0.1f, 0.1f, 1f);

    [UnityEngine.Header("Ghost, Tracking & Occlusion Settings")]
    [UnityEngine.SerializeField] private float _ghostDelayDuration = 0.4f;
    [UnityEngine.SerializeField] private float _ghostDrainSpeed = 0.5f;
    [UnityEngine.SerializeField] private UnityEngine.Vector3 _worldOffset = new UnityEngine.Vector3(0f, 2.2f, 0f);
    [UnityEngine.SerializeField] private float _maxVisibleDistance = 30f;

    private UnityEngine.Transform _targetTransform;
    private UnityEngine.Camera _mainCamera;
    private UnityEngine.RectTransform _selfRect;

    private float _hpGhostTimer = 0f;
    private float _previousHpRatio = 1.0f;
    private float _autoHideTimer = 0f;
    private const float AutoHideDuration = 6.0f;

    private int _occlusionLayerMask;
    private float _nextOcclusionCheckTime = 0f;
    private bool _isOccludedByObstacle = false;

    protected override void Awake()
    {
        base.Awake();
        _selfRect = GetComponent<UnityEngine.RectTransform>();
        _mainCamera = UnityEngine.Camera.main;

        // Player(6), Enemy(7), Hitbox(9), UI(5), IgnoreRaycast(2)를 제외한 모든 지형/벽 레이어 차폐 감지
        int ignoredLayers = (1 << 2) | (1 << 5) | (1 << 6) | (1 << 7) | (1 << 9);
        _occlusionLayerMask = ~ignoredLayers;
    }

    public void BindTarget(UnityEngine.Transform targetTransform, float currentHp, float maxHp, float currentPoise, float maxPoise, UnityEngine.Vector3 customOffset = default)
    {
        _targetTransform = targetTransform;
        _mainCamera = UnityEngine.Camera.main;
        _isOccludedByObstacle = false;
        _nextOcclusionCheckTime = 0f;

        if (customOffset != default)
        {
            _worldOffset = customOffset;
        }

        _previousHpRatio = maxHp > 0f ? UnityEngine.Mathf.Clamp01(currentHp / maxHp) : 1.0f;
        if (_hpSlider != null) _hpSlider.value = _previousHpRatio;
        if (_hpGhostSlider != null) _hpGhostSlider.value = _previousHpRatio;

        UpdatePoise(currentPoise, maxPoise);
        _autoHideTimer = AutoHideDuration;

        LateUpdate();
    }

    public void UpdateHp(float current, float max)
    {
        if (_hpSlider == null || max <= 0f) return;

        float targetRatio = UnityEngine.Mathf.Clamp01(current / max);

        if (targetRatio < _previousHpRatio)
        {
            _hpSlider.value = targetRatio;
            _hpGhostTimer = _ghostDelayDuration;
        }
        else
        {
            _hpSlider.value = targetRatio;
            if (_hpGhostSlider != null) _hpGhostSlider.value = targetRatio;
            _hpGhostTimer = 0f;
        }

        _previousHpRatio = targetRatio;
        _autoHideTimer = AutoHideDuration;
    }

    public void UpdatePoise(float current, float max)
    {
        if (_poiseSlider == null || max <= 0f) return;

        float ratio = UnityEngine.Mathf.Clamp01(current / max);
        _poiseSlider.value = ratio;

        if (_poiseFillImage != null)
        {
            // 강인도가 0이 되면 붉은색으로 강조하여 앞잡 찬스임을 표시
            _poiseFillImage.color = (ratio <= 0.001f) ? _groggyPoiseColor : _normalPoiseColor;
        }

        _autoHideTimer = AutoHideDuration;
    }

    private void Update()
    {
        // HP 잔상 감쇄
        if (_hpGhostSlider != null && _hpSlider != null)
        {
            if (_hpGhostTimer > 0f)
            {
                _hpGhostTimer -= UnityEngine.Time.deltaTime;
            }
            else if (_hpGhostSlider.value > _hpSlider.value)
            {
                _hpGhostSlider.value = UnityEngine.Mathf.MoveTowards(
                    _hpGhostSlider.value,
                    _hpSlider.value,
                    _ghostDrainSpeed * UnityEngine.Time.deltaTime
                );
            }
        }

        // 비전투 지속 시 알파 감쇄 페이드아웃
        if (_autoHideTimer > 0f)
        {
            _autoHideTimer -= UnityEngine.Time.deltaTime;
            if (_autoHideTimer <= 0f && _canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
            }
        }
    }

    private void LateUpdate()
    {
        if (_targetTransform == null || !_targetTransform.gameObject.activeInHierarchy)
        {
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
            return;
        }

        if (_mainCamera == null)
        {
            _mainCamera = UnityEngine.Camera.main;
            if (_mainCamera == null) return;
        }

        if (_autoHideTimer <= 0f)
        {
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
            return;
        }

        UnityEngine.Vector3 targetWorldPos = _targetTransform.position + _worldOffset;
        UnityEngine.Vector3 screenPos = _mainCamera.WorldToScreenPoint(targetWorldPos);

        // 카메라 뒤편(Z <= 0)에 있는 경우 렌더링 배제
        if (screenPos.z <= 0f)
        {
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
            return;
        }

        // 벽·기둥 등 장애물 시선 차폐 검사 (0.08초 간격 갱신)
        if (UnityEngine.Time.unscaledTime >= _nextOcclusionCheckTime)
        {
            _nextOcclusionCheckTime = UnityEngine.Time.unscaledTime + 0.08f;
            UnityEngine.Vector3 camPos = _mainCamera.transform.position;
            UnityEngine.Vector3 targetChestPos = _targetTransform.position + UnityEngine.Vector3.up * 1.35f;

            if (UnityEngine.Physics.Linecast(camPos, targetChestPos, out UnityEngine.RaycastHit hit, _occlusionLayerMask, UnityEngine.QueryTriggerInteraction.Ignore))
            {
                _isOccludedByObstacle = hit.transform.root != _targetTransform.root;
            }
            else
            {
                _isOccludedByObstacle = false;
            }
        }

        if (_isOccludedByObstacle)
        {
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
            return;
        }

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
        }

        _selfRect.position = screenPos;
    }

    public override void OnSpawn()
    {
        base.OnSpawn();
        _autoHideTimer = AutoHideDuration;
        _isOccludedByObstacle = false;
        _nextOcclusionCheckTime = 0f;
        if (_canvasGroup != null) _canvasGroup.alpha = 1f;
    }

    public override void OnDespawn()
    {
        base.OnDespawn();
        _targetTransform = null;
        _isOccludedByObstacle = false;
    }
}
