using UnityEngine;
using Util = DebugLogger<PlayerController>;

public class PlayerController : UnitController<PlayerController>
{
    #region MyRegion

    #endregion

    [Header("Player SO")]
    [SerializeField] private PlayerStat _playerStat;

    [Header("Ground Check")]
    [SerializeField] private LayerMask _groundLayer;

    #region MyRegion

    #endregion

    private PlayerInputReader InputReader;

    protected override void Awake()
    {
        base.Awake();
    }

    protected override void InitializeStateMachine()
    {
        if(_stateMachine != null && _stateMachine is PlayerStateMachine playerSM)
        {
            playerSM.SetUpPlayerState(this);
            playerSM.Initialize(UnitState.Idle);
        }
    }

    public override void Init()
    {
        InputReader = new PlayerInputReader();
        _stateMachine = new PlayerStateMachine();

        base.Init();
    }
}
