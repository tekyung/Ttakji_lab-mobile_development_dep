// MatchSetupSideView.cs — 대전 설정 화면의 "한쪽(1P / 2P)" 참조 모음.
//
// 구조 (MatchSetupRoot 프리팹 안):
//   SideA / SideB      이 컴포넌트
//   ├── Header         "1P" / "2P"
//   ├── DeckDropdown   저장된 덱 + (기본 테스트 덱)
//   ├── Char1Dropdown  용병 4종
//   ├── Char2Dropdown  용병 4종
//   └── InfoLabel      "20장 · 엘리 / 다이나"
//
// 양쪽이 같은 모양이라 컴포넌트 하나를 두 번 쓴다.
//
// 나눠 갖는 몫:
//   프리팹 — 위치·크기·글꼴·색
//   코드   — 목록 채우기 · 고른 값 읽기 · 안내 문구
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class MatchSetupSideView : MonoBehaviour
{
    [Tooltip("'1P' / '2P' 처럼 어느 쪽인지 알려 주는 제목.")]
    public TextMeshProUGUI header;

    [Tooltip("덱 고르기. 첫 항목은 항상 (기본 테스트 덱)이다.")]
    public TMP_Dropdown deckDropdown;

    [Tooltip("용병 1.")]
    public TMP_Dropdown char1Dropdown;

    [Tooltip("용병 2.")]
    public TMP_Dropdown char2Dropdown;

    [Tooltip("'20장 · 엘리 / 다이나' 처럼 고른 결과를 요약해 보여 준다.")]
    public TextMeshProUGUI infoLabel;

    /// <summary>인스펙터에서 비워 둔 것을 이름으로 채운다. 여러 번 불러도 안전하다.</summary>
    public void ResolveMissingReferences()
    {
        if (header == null) header = FindChild<TextMeshProUGUI>("Header");
        if (infoLabel == null) infoLabel = FindChild<TextMeshProUGUI>("InfoLabel");

        if (deckDropdown == null) deckDropdown = FindChild<TMP_Dropdown>("DeckDropdown");
        if (char1Dropdown == null) char1Dropdown = FindChild<TMP_Dropdown>("Char1Dropdown");
        if (char2Dropdown == null) char2Dropdown = FindChild<TMP_Dropdown>("Char2Dropdown");
    }

    /// <summary>빠진 참조를 이름으로 알린다. 하나라도 없으면 false.</summary>
    public bool Validate(out string reason)
    {
        ResolveMissingReferences();

        var missing = new System.Collections.Generic.List<string>();

        if (deckDropdown == null) missing.Add(nameof(deckDropdown));
        if (char1Dropdown == null) missing.Add(nameof(char1Dropdown));
        if (char2Dropdown == null) missing.Add(nameof(char2Dropdown));

        // 제목과 안내 문구는 없어도 고르는 데 지장이 없다.
        if (header == null || infoLabel == null)
            Debug.LogWarning($"[{name}] 제목 또는 안내 문구가 없다. 고르는 것 자체는 된다.");

        if (missing.Count == 0)
        {
            reason = null;
            return true;
        }

        reason = $"{name}에 연결되지 않은 항목: {string.Join(", ", missing)}";
        return false;
    }

    private T FindChild<T>(string targetName) where T : Component
    {
        foreach (T candidate in GetComponentsInChildren<T>(true))
        {
            if (candidate.name == targetName) return candidate;
        }

        return null;
    }

    private void Reset() => ResolveMissingReferences();
}
