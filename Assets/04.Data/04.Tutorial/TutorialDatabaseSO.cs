using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TutorialDatabaseSO", menuName = "GameData/TutorialDatabaseSO")]
public class TutorialDatabaseSO : ScriptableObject
{
    [Tooltip("새 게임 첫 진입 시 순서대로 표시할 튜토리얼 가이드 이미지 목록")]
    [SerializeField] private List<Sprite> _pages = new List<Sprite>();

    public IReadOnlyList<Sprite> Pages => _pages;
    public int PageCount => _pages != null ? _pages.Count : 0;
}