// DeckInfoPanelView.cs — 톱니바퀴 + 덱 리스트 패널의 참조 모음.
//
// 구조 (프리팹: Resources/Build/DeckInfoPanel):
//   DeckInfoPanelRoot            화면 전체로 늘어난 빈 그릇 + 이 컴포넌트
//   ├── GearButton               좌측 상단. Canvas(600)으로 보드 위에 뜬다
//   │   └── Icon                 톱니바퀴 스프라이트
//   └── DeckInfoPanel            가운데 패널. Canvas(700) — 선택창(500) 위, 결과창(900) 아래
//       ├── Title                "내 덱 (n / m장 남음)"
//       ├── CloseButton
//       │   └── Label
//       ├── DeckGrid             GridLayoutGroup — 칸 크기·간격·열 수를 여기가 정한다
//       └── SurrenderButton
//           └── Label
//
// 나눠 갖는 몫:
//   프리팹 — 위치·크기·앵커·격자 배치·글꼴·기본 색·톱니 스프라이트
//   코드   — 제목 문구 · 열고 닫기 · 격자에 채울 카드 · 항복 확인 단계의 문구와 색
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class DeckInfoPanelView : MonoBehaviour
{
    [Tooltip("좌측 상단 톱니바퀴. 누르면 패널이 열린다.")]
    public Button gearButton;

    [Tooltip("덱 리스트 패널. 평소에는 꺼 둔다.")]
    public GameObject panel;

    [Tooltip("패널 헤더의 제목. 남은 장수를 코드가 채운다.")]
    public TextMeshProUGUI titleText;

    [Tooltip("헤더 우측 [닫기].")]
    public Button closeButton;

    [Tooltip("카드 칸이 들어갈 격자. GridLayoutGroup이 붙어 있어야 한다.")]
    public RectTransform grid;

    [Tooltip("[항복]. 오조작을 막으려 두 번 눌러야 한다.")]
    public Button surrenderButton;

    [Tooltip("항복 버튼의 글자. 1차 클릭에서 '정말 항복?'으로 바뀐다.")]
    public TextMeshProUGUI surrenderLabel;

    [Tooltip("항복 버튼의 바탕. 확인 단계에서 색이 바뀐다. 비우면 버튼에서 찾는다.")]
    public Image surrenderImage;

    /// <summary>인스펙터에서 비워 둔 것을 이름으로 채운다. 여러 번 불러도 안전하다.</summary>
    public void ResolveMissingReferences()
    {
        if (gearButton == null) gearButton = FindChild<Button>("GearButton");

        if (panel == null)
        {
            RectTransform p = FindChild<RectTransform>("DeckInfoPanel");
            if (p != null) panel = p.gameObject;
        }

        if (titleText == null) titleText = FindChild<TextMeshProUGUI>("Title");
        if (closeButton == null) closeButton = FindChild<Button>("CloseButton");
        if (grid == null) grid = FindChild<RectTransform>("DeckGrid");
        if (surrenderButton == null) surrenderButton = FindChild<Button>("SurrenderButton");

        if (surrenderLabel == null && surrenderButton != null)
            surrenderLabel = surrenderButton.GetComponentInChildren<TextMeshProUGUI>(true);

        if (surrenderImage == null && surrenderButton != null)
            surrenderImage = surrenderButton.GetComponent<Image>();
    }

    /// <summary>빠진 참조를 이름으로 알린다. 하나라도 없으면 false.</summary>
    public bool Validate(out string reason)
    {
        ResolveMissingReferences();

        var missing = new System.Collections.Generic.List<string>();

        if (gearButton == null) missing.Add(nameof(gearButton));
        if (panel == null) missing.Add(nameof(panel));
        if (grid == null) missing.Add(nameof(grid));
        if (surrenderButton == null) missing.Add(nameof(surrenderButton));
        if (surrenderLabel == null) missing.Add(nameof(surrenderLabel));

        // 제목과 [닫기]는 없어도 나머지가 돌아간다. 없으면 알리기만 한다.
        if (titleText == null) Debug.LogWarning($"[{name}] 제목이 없어 남은 장수를 보여 줄 수 없다.");
        if (closeButton == null) Debug.LogWarning($"[{name}] [닫기]가 없다. 톱니바퀴로만 닫을 수 있다.");

        if (grid != null && grid.GetComponent<GridLayoutGroup>() == null)
            missing.Add(nameof(grid) + "(GridLayoutGroup 없음)");

        if (missing.Count == 0)
        {
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
            if (candidate.name == targetName) return candidate;
        }

        return null;
    }

    private void Reset() => ResolveMissingReferences();
}
