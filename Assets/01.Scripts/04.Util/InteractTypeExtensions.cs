using ConstStrings;
using System.Collections.Generic;

/// <summary>
/// InteractType Enum과 ConstString 문구를 1:1로 매핑하여 조회하는 정적 헬퍼
/// </summary>
public static class InteractTypeExtensions
{
    private static readonly Dictionary<InteractType, string> _promptMap = new Dictionary<InteractType, string>
    {
        { InteractType.Bonfire, ConstString.BonfirePrompt },
        { InteractType.BossDoor, ConstString.BossDoorPrompt },
        { InteractType.ItemDrop, ConstString.ItemDropPrompt },
        { InteractType.NpcTalk, ConstString.NpcTalkPrompt },
        { InteractType.Chest, ConstString.ChestOpenPrompt },
        { InteractType.Lever, ConstString.LeverPullPrompt }
    };

    public static string GetPrompt(this InteractType type)
    {
        if (_promptMap.TryGetValue(type, out string prompt))
        {
            return prompt;
        }
        return string.Empty;
    }
}