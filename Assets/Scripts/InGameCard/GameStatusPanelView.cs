// GameStatusPanelView.cs — 턴·페이즈 표시 / 진행 로그 / 결과 오버레이의 참조 모음.
//
// 구조 (프리팹: Resources/Build/GameStatusPanelRoot):
//   GameStatusPanelRoot        화면 전체로 늘어난 빈 그릇 + 이 컴포넌트
//   ├── StatusHeader           화면 가운데 띠. "ROUND 3 · 메인 페이즈  세트 12초"
//   │   └── Text
//   ├── StatusLog              좌측 진행 로그. RectMask2D로 넘치는 줄을 자른다
//   │   └── Text
//   └── ResultOverlay          [평소 꺼짐] 화면 전체 딤 + 결과 문구 + 이탈 버튼
//       ├── Title
//       ├── Detail
//       ├── RetryButton  └ Label
//       └── MainMenuButton └ Label
//
// 나눠 갖는 몫:
//   프리팹 — 위치·크기·앵커·글꼴·바탕색·버튼 색·정렬 순서
//   코드   — 표시할 문구 · 로그 줄 쌓기 · 오버레이를 열고 닫기 · 승패에 따른 글자색
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class GameStatusPanelView : MonoBehaviour
{
    [Header("턴 / 페이즈")]
    [Tooltip("가운데 띠 전체. 결과가 뜨는 동안에는 통째로 감춘다.")]
    public GameObject header;

    [Tooltip("띠에 찍을 문구. 리치 텍스트를 쓴다(남은 시간에 색을 입힌다).")]
    public TextMeshProUGUI headerText;

    [Header("진행 로그")]
    [Tooltip("로그 바탕. 참조만 해 두고 코드가 켜고 끄지는 않는다.")]
    public GameObject logPanel;

    [Tooltip("최근 몇 줄을 이어 붙여 넣는다. 리치 텍스트·줄바꿈이 켜져 있어야 한다.")]
    public TextMeshProUGUI logText;

    [Header("결과 오버레이")]
    [Tooltip("상대가 무언가 고르는 동안 띄울 안내. 상대 보드 쪽(화면 위) 가운데에 둔다.")]
    public TextMeshProUGUI opponentWaitText;

    [Tooltip("화면 전체 딤. 평소에는 꺼 둔다.")]
    public GameObject overlay;

    [Tooltip("'승리' / '패배' 등. 색은 코드가 승패에 따라 바꾼다.")]
    public TextMeshProUGUI overlayTitle;

    [Tooltip("'OO 승리' 같은 부연.")]
    public TextMeshProUGUI overlayDetail;

    [Tooltip("[다시 하기].")]
    public Button retryButton;

    [Tooltip("[메인 메뉴로].")]
    public Button mainMenuButton;

    /// <summary>인스펙터에서 비워 둔 것을 이름으로 채운다. 여러 번 불러도 안전하다.</summary>
    public void ResolveMissingReferences()
    {
        if (header == null) header = FindChildObject("StatusHeader");
        if (headerText == null && header != null)
            headerText = header.GetComponentInChildren<TextMeshProUGUI>(true);

        if (logPanel == null) logPanel = FindChildObject("StatusLog");
        if (logText == null && logPanel != null)
            logText = logPanel.GetComponentInChildren<TextMeshProUGUI>(true);

        if (overlay == null) overlay = FindChildObject("ResultOverlay");
        if (overlay != null)
        {
            if (overlayTitle == null) overlayTitle = FindChild<TextMeshProUGUI>("Title");
            if (overlayDetail == null) overlayDetail = FindChild<TextMeshProUGUI>("Detail");
            if (retryButton == null) retryButton = FindChild<Button>("RetryButton");
            if (mainMenuButton == null) mainMenuButton = FindChild<Button>("MainMenuButton");
        }
    }

    /// <summary>빠진 참조를 이름으로 알린다. 하나라도 없으면 false.</summary>
    public bool Validate(out string reason)
    {
        ResolveMissingReferences();

        var missing = new System.Collections.Generic.List<string>();

        if (header == null) missing.Add(nameof(header));
        if (headerText == null) missing.Add(nameof(headerText));
        if (logText == null) missing.Add(nameof(logText));
        if (overlay == null) missing.Add(nameof(overlay));
        if (overlayTitle == null) missing.Add(nameof(overlayTitle));

        // 부연 문구와 이탈 버튼이 없어도 나머지는 돌아간다. 다만 버튼이 없으면
        // 게임이 끝난 뒤 화면에서 빠져나갈 방법이 사라지므로 크게 알린다.
        if (overlayDetail == null) Debug.LogWarning($"[{name}] 결과 부연 문구가 없다.");
        if (retryButton == null || mainMenuButton == null)
            Debug.LogError($"[{name}] 결과 오버레이에 이탈 버튼이 없다. 게임이 끝나면 화면에서 나갈 수 없다.");

        if (missing.Count == 0)
        {
            reason = null;
            return true;
        }

        reason = $"연결되지 않은 항목: {string.Join(", ", missing)}";
        return false;
    }

    private GameObject FindChildObject(string targetName)
    {
        RectTransform found = FindChild<RectTransform>(targetName);
        return found != null ? found.gameObject : null;
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
