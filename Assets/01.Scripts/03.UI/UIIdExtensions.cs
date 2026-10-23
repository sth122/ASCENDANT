/// <summary>
/// UIId 숫자 대역 기반 레이어 자동 변환 및 무결성 검증 헬퍼
/// </summary>
public static class UIIdExtensions
{
    /// <summary>
    /// UIId의 정수 대역을 통해 해당 UI가 배치되어야 할 UILayer를 즉각 산출
    /// </summary>
    public static UILayer GetExpectedLayer(this UIId uiId)
    {
        int idValue = (int)uiId;

        if (idValue >= 5000 && idValue < 6000) return UILayer.System;
        if (idValue >= 4000 && idValue < 5000) return UILayer.Popup;
        if (idValue >= 3000 && idValue < 4000) return UILayer.Window;
        if (idValue >= 2000 && idValue < 3000) return UILayer.HUD;
        if (idValue >= 1000 && idValue < 2000) return UILayer.Background;

        return UILayer.HUD;
    }

    /// <summary>
    /// UIBase 컴포넌트에 설정된 Layer와 UIId 번호 대역이 일치하는지 검증
    /// </summary>
    public static bool ValidateLayerMatch(this UIId uiId, UILayer actualLayer)
    {
        return uiId.GetExpectedLayer() == actualLayer;
    }
}