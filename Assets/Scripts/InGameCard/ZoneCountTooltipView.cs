// ZoneCountTooltipView.cs — 존 위에 커서를 올렸을 때 뜨는 작은 말풍선의 참조 모음.
//
// 구조 (프리팹: Resources/Build/ZoneCountTooltip):
//   ZoneCountTooltipRoot        이 컴포넌트 + UiSortingLayer(420)
//   └── Panel                   말풍선 바탕 (평소 꺼짐)
//       └── Label               "메인덱 13장"
//
// 나눠 갖는 몫:
//   프리팹 — 크기·바탕색·글꼴·정렬 순서
//   코드   — 문구 · 켜고 끄기 · 어느 존 옆에 놓을지
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class ZoneCountTooltipView : MonoBehaviour
{
    [Tooltip("말풍선 본체. 평소에는 꺼 둔다.")]
    public RectTransform panel;

    [Tooltip("'메인덱 13장' 같은 문구.")]
    public TextMeshProUGUI label;

    /// <summary>인스펙터에서 비워 둔 것을 이름으로 채운다. 여러 번 불러도 안전하다.</summary>
    public void ResolveMissingReferences()
    {
        if (panel == null) panel = FindChild<RectTransform>("Panel");
        if (label == null && panel != null) label = panel.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    /// <summary>빠진 참조를 이름으로 알린다. 하나라도 없으면 false.</summary>
    public bool Validate(out string reason)
    {
        ResolveMissingReferences();

        var missing = new System.Collections.Generic.List<string>();
        if (panel == null) missing.Add(nameof(panel));
        if (label == null) missing.Add(nameof(label));

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
