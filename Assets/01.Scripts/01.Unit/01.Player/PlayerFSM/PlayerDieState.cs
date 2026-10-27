using Cysharp.Threading.Tasks;

public class PlayerDieState : PlayerBaseState
{
    private const float GameOverUIDelay = 2.5f;
    public PlayerDieState(PlayerController owner, PlayerStateMachine stateMachine) : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();

        owner.StopMovementAPI();
        owner.InputReader.DisablePlayerInput();

        // Any State -> death 트리거 발동
        _playerAnimController.SetTrigger(AnimParamType.Die);

        // GameManager에 사망 상태 통지 (소울라이크 슬로우 연출)
        if (GameManager.Instance != null)
        {
            GameManager.Instance.TriggerGameOver();
        }

        ShowGameOverUIAsync(owner).Forget();
    }

    private async UniTaskVoid ShowGameOverUIAsync(PlayerController owner)
    {
        // 실시간(unscaled) 기준 2.5초 대기
        await UniTask.Delay((int)(GameOverUIDelay * 1000f), ignoreTimeScale: true, cancellationToken: owner.destroyCancellationToken);

        if (UIManager.Instance != null)
        {
            GameOverModel model = new GameOverModel();
            await UIManager.Instance.OpenUIAsync<GameOverView, GameOverPresenter, GameOverModel>(
                UIId.GameOverUI, "GameOverUI", model, owner.destroyCancellationToken
            );
        }
    }
}
