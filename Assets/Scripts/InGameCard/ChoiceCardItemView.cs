// ChoiceCardItemView.cs — 선택 다이얼로그에 늘어놓는 "카드 한 장 칸"의 참조 모음.
//
// 구조 (프리팹: Resources/Build/ChoiceCardItem):
//   ChoiceCardItem   Image(테두리) + Button + LayoutElement + 이 컴포넌트
//   ├── CardHost     실제 카드 프리팹(CardSlotInGame)이 들어갈 빈 자리
//   └── OrderBadge   우상단 순서 배지 (평소에는 꺼 둔다)
//       └── Number   1, 2, …
//
// 나눠 갖는 몫:
//   프리팹 — 칸 크기 · 테두리 두께 · 배지 위치와 크기 · 글꼴
//   코드   — 어떤 카드를 넣을지 · 선택 여부에 따른 테두리 색 · 배지 번호와 표시 여부
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ChoiceCardItemView : MonoBehaviour
{
    [Tooltip("선택 여부에 따라 색이 바뀌는 테두리. 비우면 이 오브젝트에서 찾는다.")]
    public Image frameImage;

    [Tooltip("클릭을 받는 버튼. 비우면 이 오브젝트에서 찾는다.")]
    public Button button;

    [Tooltip("카드 그림이 들어갈 자리. 테두리 두께만큼 안쪽으로 들어가 있어야 한다.")]
    public RectTransform cardHost;

    [Tooltip("순서 배지의 루트. 평소에는 꺼 두고, 순서가 중요한 요청에서만 켠다.")]
    public GameObject badgeRoot;

    [Tooltip("배지에 찍을 번호.")]
    public TextMeshProUGUI badgeLabel;

    /// <summary>인스펙터에서 비워 둔 것을 이름으로 채운다. 여러 번 불러도 안전하다.</summary>
    public void ResolveMissingReferences()
    {
        if (frameImage == null) frameImage = GetComponent<Image>();
        if (button == null) button = GetComponent<Button>();

        if (cardHost == null) cardHost = FindChild<RectTransform>("CardHost");

        if (badgeRoot == null)
        {
            RectTransform badge = FindChild<RectTransform>("OrderBadge");
            if (badge != null) badgeRoot = badge.gameObject;
        }

        if (badgeLabel == null && badgeRoot != null)
            badgeLabel = badgeRoot.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    /// <summary>빠진 참조를 이름으로 알린다. 하나라도 없으면 false.</summary>
    public bool Validate(out string reason)
    {
        ResolveMissingReferences();

        var missing = new System.Collections.Generic.List<string>();

        if (frameImage == null) missing.Add(nameof(frameImage));
        if (button == null) missing.Add(nameof(button));
        if (cardHost == null) missing.Add(nameof(cardHost));

        // 배지는 '순서가 중요한 요청'에서만 쓰이므로 없어도 나머지는 동작한다.
        if (badgeRoot == null || badgeLabel == null)
            Debug.LogWarning($"[{name}] 순서 배지가 없어 1·2 번호를 보여 줄 수 없다.");

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
            if (candidate.name == targetName && candidate.gameObject != gameObject) return candidate;
        }

        return null;
    }

    private void Reset() => ResolveMissingReferences();
}
