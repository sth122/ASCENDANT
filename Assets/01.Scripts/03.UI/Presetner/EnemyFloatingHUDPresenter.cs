using UnityEngine;

/// <summary>
/// Enemy 개체에 부착되어 UnitStatController(Model)와 EnemyFloatingHUDView(View)를 1:1 중계하는 절충형 MVP Presenter
/// 컴포넌트 라이프사이클을 활용하여 풀에서 View를 배정받고 반환하며,
/// 부모 캔버스를 UIManager의 HUD 레이어로 안전하게 리패런팅
/// </summary>
[RequireComponent(typeof(EnemyBTController), typeof(UnitStatusController))]
public class EnemyFloatingHUDPresenter : MonoBehaviour
{
    [Header("Tracking Offset")]
    [SerializeField] private Vector3 _worldOffset = new Vector3(0f, 2.3f, 0f);

    private UnitStatusController _statModel;
    private EnemyFloatingHUDView _floatingView;

    private void Awake()
    {
        _statModel = GetComponent<UnitStatusController>();
    }

    private void OnEnable()
    {
        SubscribeModelEvents();
    }

    private void OnDisable()
    {
        UnsubscribeModelEvents();
        ReleaseView();
    }

    /// <summary>
    /// 적이 월드에 스폰될 때(SpawnInit) 호출되어 풀에서 View를 꺼내고 캔버스에 안착
    /// </summary>
    public void AllocateAndBindView()
    {
        if (_floatingView != null) return;

        // 1. UIManager의 HUD 레이어 컨테이너(@Layer_100_HUD) 획득
        RectTransform hudContainer = UIManager.Instance != null
            ? UIManager.Instance.GetLayerContainer(UILayer.HUD) : null;

        // 2. 풀에서 View 인스턴스 획득 (부모를 캔버스 HUD 레이어로 즉시 지정)
        _floatingView = ObjectPoolManager.Instance.Spawn<EnemyFloatingHUDView>(
            PoolType.EnemyFloatingHpBar, Vector3.zero, Quaternion.identity, hudContainer
        );

        if (_floatingView != null)
        {
            // 풀 루트에 머물지 않도록 캔버스 컨테이너로 리패런팅 보장
            if (hudContainer != null && _floatingView.transform.parent != hudContainer)
            {
                _floatingView.transform.SetParent(hudContainer, false);
            }

            // 3. View 초기화 및 타깃 바인딩
            float curHp = _statModel != null ? _statModel.CurrentHp : 100f;
            float maxHp = _statModel != null ? _statModel.MaxHp : 100f;
            float curPoise = _statModel != null ? _statModel.CurrentPoise : 50f;
            float maxPoise = _statModel != null ? _statModel.MaxPoise : 50f;

            _floatingView.BindTarget(transform, curHp, maxHp, curPoise, maxPoise, _worldOffset);
        }
    }

    /// <summary>
    /// 적 비활성화, 사망 또는 화톳불 리셋 시 View를 풀에 반환
    /// </summary>
    public void ReleaseView()
    {
        if (_floatingView != null)
        {
            ObjectPoolManager.Instance.Despawn(PoolType.EnemyFloatingHpBar, _floatingView);
            _floatingView = null;
        }
    }

    #region Model(UnitStatusController) 이벤트 수신 및 View 전달
    private void SubscribeModelEvents()
    {
        UnsubscribeModelEvents();

        if (_statModel == null) return;

        _statModel.OnHpChangedEvent += HandleHpChanged;
        _statModel.OnPoiseChangedEvent += HandlePoiseChanged;
    }

    private void UnsubscribeModelEvents()
    {
        if (_statModel == null) return;

        _statModel.OnHpChangedEvent -= HandleHpChanged;
        _statModel.OnPoiseChangedEvent -= HandlePoiseChanged;
    }

    private void HandleHpChanged(float currentHp, float maxHp)
    {
        if (_floatingView != null)
        {
            _floatingView.UpdateHp(currentHp, maxHp);
        }
    }

    private void HandlePoiseChanged(float currentPoise, float maxPoise)
    {
        if (_floatingView != null)
        {
            _floatingView.UpdatePoise(currentPoise, maxPoise);
        }
    }
    #endregion

    private void OnDestroy()
    {
        UnsubscribeModelEvents();
        ReleaseView();
    }
}
