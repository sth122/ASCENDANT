using ConstStrings;
using System.Collections.Generic;

/// <summary>
/// UIId 숫자 대역 기반 레이어 자동 변환 및 AddressableKey 캐싱 매핑 헬퍼
/// </summary>
public static class UIIdExtensions
{
    private static readonly Dictionary<UIId, string> _addressableKeyMap = new Dictionary<UIId, string>
    {
        { UIId.TitleBackgroundUI, ConstString.TitleBackgroundUI },
        { UIId.GameOverBackgroundUI, ConstString.GameOverBackgroundUI },
        { UIId.PlayerHUD, ConstString.PlayerHUD },
        { UIId.EnemyFloatingHUD, ConstString.EnemyFloatingHUD },
        { UIId.BossHUD, ConstString.BossHUD },
        { UIId.TitleMenuUI, ConstString.TitleMenuUI },
        { UIId.SystemMenuUI, ConstString.SystemMenuUI },
        { UIId.InventoryUI, ConstString.InventoryUI },
        { UIId.EquipmentUI, ConstString.EquipmentUI },
        { UIId.StatusUI, ConstString.StatusUI },
        { UIId.GameOverUI, ConstString.GameOverUI },
        { UIId.ConfirmPopup, ConstString.ConfirmPopupUI },
        { UIId.NoticePopup, ConstString.NoticePopupUI },
        { UIId.KeyBindingPopup, ConstString.KeyBindingPopupUI },
        { UIId.FadeCurtainUI, ConstString.FadeCurtainUI },
        { UIId.LoadingScreenUI, ConstString.LoadingScreenUI },
    };

    public static string GetAddressableKey(this UIId uiId)
    {
        if (_addressableKeyMap.TryGetValue(uiId, out string key))
        {
            return key;
        }
        return uiId.ToString();
    }

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

    public static bool ValidateLayerMatch(this UIId uiId, UILayer actualLayer)
    {
        return uiId.GetExpectedLayer() == actualLayer;
    }
}