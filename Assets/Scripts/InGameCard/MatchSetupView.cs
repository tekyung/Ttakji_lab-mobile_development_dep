// MatchSetupView.cs — 대전 시작 전 설정 화면의 참조 모음.
//
// 구조 (프리팹: Resources/Build/MatchSetupRoot):
//   MatchSetupRoot        이 컴포넌트 + UiSortingLayer(950)
//   └── Panel
//       ├── Title         "대전 설정"
//       ├── ModeDropdown  사람 vs 봇 / 봇 vs 봇
//       ├── SideA         [MatchSetupSideView]  ← 하단 보드(1P)
//       ├── SideB         [MatchSetupSideView]  ← 상단 보드(2P)
//       └── StartButton   [시작]
//
// 나눠 갖는 몫:
//   프리팹 — 위치·크기·앵커·글꼴·색·정렬 순서
//   코드   — 목록 채우기 · 고른 값 읽기 · 대전 시작
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MatchSetupView : MonoBehaviour
{
    [Tooltip("설정 창 전체. 대전이 시작되면 끈다.")]
    public GameObject panel;

    [Tooltip("사람 vs 봇 / 봇 vs 봇.")]
    public TMP_Dropdown modeDropdown;

    [Tooltip("하단 보드(1P) 쪽 설정.")]
    public MatchSetupSideView sideA;

    [Tooltip("상단 보드(2P) 쪽 설정.")]
    public MatchSetupSideView sideB;

    [Tooltip("[시작].")]
    public Button startButton;

    /// <summary>인스펙터에서 비워 둔 것을 이름으로 채운다. 여러 번 불러도 안전하다.</summary>
    public void ResolveMissingReferences()
    {
        if (panel == null)
        {
            RectTransform found = FindChild<RectTransform>("Panel");
            if (found != null) panel = found.gameObject;
        }

        if (modeDropdown == null) modeDropdown = FindChild<TMP_Dropdown>("ModeDropdown");
        if (startButton == null) startButton = FindChild<Button>("StartButton");

        if (sideA == null) sideA = FindChild<MatchSetupSideView>("SideA");
        if (sideB == null) sideB = FindChild<MatchSetupSideView>("SideB");

        sideA?.ResolveMissingReferences();
        sideB?.ResolveMissingReferences();
    }

    /// <summary>빠진 참조를 이름으로 알린다. 하나라도 없으면 false.</summary>
    public bool Validate(out string reason)
    {
        ResolveMissingReferences();

        var missing = new System.Collections.Generic.List<string>();

        if (panel == null) missing.Add(nameof(panel));
        if (modeDropdown == null) missing.Add(nameof(modeDropdown));
        if (sideA == null) missing.Add(nameof(sideA));
        if (sideB == null) missing.Add(nameof(sideB));

        // ★ [시작]이 없으면 이 화면에서 대전을 시작할 방법이 사라진다. 크게 알린다.
        if (startButton == null)
        {
            missing.Add(nameof(startButton));
            Debug.LogError($"[{name}] [시작] 버튼이 없다. 대전을 시작할 수 없다.");
        }

        if (missing.Count == 0)
        {
            if (!sideA.Validate(out string reasonA)) { reason = reasonA; return false; }
            if (!sideB.Validate(out string reasonB)) { reason = reasonB; return false; }

            reason = null;
            return true;
        }

        reason = $"연결되지 않은 항목: {string.Join(", ", missing)}";
        return false;
    }

    private T FindChild<T>(string targetName) where T : Component
    {
        foreach (T candidate in GetComponentsInChildren<T>(true))
        {
            if (candidate.name == targetName && candidate.gameObject != gameObject) return candidate;
        }

        return null;
    }

    private void Reset() => ResolveMissingReferences();
}
