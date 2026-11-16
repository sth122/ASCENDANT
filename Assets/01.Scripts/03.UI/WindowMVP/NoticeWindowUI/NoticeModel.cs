using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 단일 안내 및 다중 페이지 튜토리얼 가이드를 모두 지원하는 팝업 데이터 모델
/// </summary>
public class NoticeModel : IUIModel
{
    public List<Sprite> Pages { get; private set; } = new List<Sprite>();
    public int CurrentPageIndex { get; set; } = 0;
    public string FinalConfirmLabel { get; set; } = "모험 시작";

    public int TotalPages => Pages != null ? Pages.Count : 0;
    public bool HasPreviousPage => CurrentPageIndex > 0;
    public bool HasNextPage => CurrentPageIndex < TotalPages - 1;

    public NoticeModel()
    {
        Reset();
    }

    /// <summary>
    /// 단일 이미지 출력용 생성자
    /// </summary>
    public NoticeModel(Sprite singleImage, string confirmLabel = "확인")
    {
        Pages = new List<Sprite>();
        if (singleImage != null)
        {
            Pages.Add(singleImage);
        }
        CurrentPageIndex = 0;
        FinalConfirmLabel = string.IsNullOrEmpty(confirmLabel) ? "확인" : confirmLabel;
    }

    /// <summary>
    /// 다중 페이지 튜토리얼 이미지 슬라이드용 생성자
    /// </summary>
    public NoticeModel(IReadOnlyList<Sprite> pages, string finalConfirmLabel = "모험 시작")
    {
        Pages = new List<Sprite>();
        if (pages != null)
        {
            for (int i = 0; i < pages.Count; i++)
            {
                if (pages[i] != null)
                {
                    Pages.Add(pages[i]);
                }
            }
        }
        CurrentPageIndex = 0;
        FinalConfirmLabel = string.IsNullOrEmpty(finalConfirmLabel) ? "모험 시작" : finalConfirmLabel;
    }

    public Sprite GetCurrentSprite()
    {
        if (Pages == null || Pages.Count == 0) return null;
        int clamped = Mathf.Clamp(CurrentPageIndex, 0, Pages.Count - 1);
        return Pages[clamped];
    }

    public void Reset()
    {
        Pages.Clear();
        CurrentPageIndex = 0;
        FinalConfirmLabel = "모험 시작";
    }
}