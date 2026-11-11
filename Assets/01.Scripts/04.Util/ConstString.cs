namespace ConstStrings
{
    /// <summary>
    /// 프로젝트 전역에서 사용되는 불변 문자열 상수 정의 클래스
    /// </summary>
    public static class ConstString
    {
        #region Interaction Prompts
        public const string BonfirePrompt = "[F] 화톳불에서 휴식";
        public const string BossDoorPrompt = "[F] 보스 씬 들어가기";
        public const string ItemDropPrompt = "[F] 아이템 줍기";
        public const string NpcTalkPrompt = "[F] 대화하기";
        public const string ChestOpenPrompt = "[F] 상자 열기";
        public const string LeverPullPrompt = "[F] 레버 당기기";
        #endregion

        #region Popup Prompts
        public const string GameStartPrompt = "게임을 시작하시겠습니까?";
        public const string GameQuitPrompt = "게임을 종료하시겠습니까?";
        public const string NewGameOverwritePrompt = "이전 데이터를 삭제하고 새로 시작하시겠습니까?";
        public const string LoadGamePrompt = "게임을 불러오시겠습니까?";
        #endregion

        #region UI Addressable Keys
        public const string TitleBackgroundUI = "TitleBackgroundUI";
        public const string GameOverBackgroundUI = "GameOverBackgroundUI";
        public const string PlayerHUD = "PlayerHUD";
        public const string EnemyFloatingHUD = "EnemyFloatingHUD";
        public const string BossHUD = "BossHUD";
        public const string QuickSlotHUD = "QuickSlotHUD";
        public const string TitleMenuUI = "TitleMenuUI";
        public const string SystemMenuUI = "SystemMenuUI";
        public const string InventoryUI = "InventoryUI";
        public const string EquipmentUI = "EquipmentUI";
        public const string StatusUI = "StatusUI";
        public const string GameOverUI = "GameOverUI";
        public const string ConfirmPopupUI = "ConfirmPopupUI";
        public const string NoticePopupUI = "NoticePopupUI";
        public const string KeyBindingPopupUI = "KeyBindingPopupUI";
        public const string FadeCurtainUI = "FadeCurtainUI";
        public const string LoadingScreenUI = "LoadingScreenUI";
        #endregion
    }
}