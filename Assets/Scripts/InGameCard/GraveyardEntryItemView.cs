// GraveyardEntryItemView.cs — 폐기존 목록에 늘어놓는 "카드 한 줄"의 참조 모음.
//
// 구조 (프리팹: Resources/Build/GraveyardEntryItem):
//   GraveyardEntryItem   Image(카드 그림) + Button + LayoutElement + 이 컴포넌트
//
// 자식이 없다. 그림 한 장이 곧 한 줄이다.
//
// 나눠 갖는 몫:
//   프리팹 — 줄 크기 · 비율 유지 여부
//   코드   — 어떤 카드 그림을 넣을지 · 누를 수 있는지(자원 카드는 못 누른다)
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class GraveyardEntryItemView : MonoBehaviour
{
    [Tooltip("카드 그림. 비우면 이 오브젝트에서 찾는다.")]
    public Image image;

    [Tooltip("누르면 서브 팝업으로 크게 본다. 자원 카드에서는 코드가 꺼 둔다.")]
    public Button button;

    [Tooltip("세로 목록에서 자리를 차지하는 크기. 비우면 이 오브젝트에서 찾는다.")]
    public LayoutElement layout;

    /// <summary>인스펙터에서 비워 둔 것을 이름으로 채운다. 여러 번 불러도 안전하다.</summary>
    public void ResolveMissingReferences()
    {
        if (image == null) image = GetComponent<Image>();
        if (button == null) button = GetComponent<Button>();
        if (layout == null) layout = GetComponent<LayoutElement>();
    }

    /// <summary>빠진 참조를 이름으로 알린다. 하나라도 없으면 false.</summary>
    public bool Validate(out string reason)
    {
        ResolveMissingReferences();

        var missing = new System.Collections.Generic.List<string>();

        if (image == null) missing.Add(nameof(image));
        if (button == null) missing.Add(nameof(button));

        // LayoutElement가 없으면 줄 높이가 들쭉날쭉해지지만 목록 자체는 뜬다.
        if (layout == null) Debug.LogWarning($"[{name}] LayoutElement가 없어 줄 크기가 흔들릴 수 있다.");

        if (missing.Count == 0)
        {
            reason = null;
            return true;
        }

        reason = $"연결되지 않은 항목: {string.Join(", ", missing)}";
        return false;
    }

    private void Reset() => ResolveMissingReferences();
}
