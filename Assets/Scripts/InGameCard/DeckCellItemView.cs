// DeckCellItemView.cs — 덱 리스트 격자에 늘어놓는 "카드 한 칸"의 참조 모음.
//
// 구조 (프리팹: Resources/Build/DeckCellItem):
//   DeckCellItem   RectTransform + CanvasGroup + 이 컴포넌트
//   └── CardHost   카드 프리팹이 들어갈 자리. 축소 배율을 여기가 갖는다
//
// 왜 CardHost를 따로 두는가:
//   카드 프리팹(200x280)은 자식들이 고정 크기라 sizeDelta로는 줄지 않는다. localScale로만 줄어든다.
//   그 배율을 코드가 들고 있으면 격자 칸 크기를 프리팹에서 바꿔도 카드가 따라오지 않는다.
//   자리를 하나 두고 거기에 배율을 걸어 두면, 칸과 카드를 한곳에서 맞출 수 있다.
//
// ★ 격자 칸 크기를 바꾸면 <b>이 배율도 함께 고쳐야 한다.</b>
//
//       배율 = 칸 가로 ÷ 120     (카드 프리팹의 원래 가로)
//
//   예: 칸 150×210 → 150 ÷ 120 = 1.25
//   칸만 키우고 배율을 그대로 두면 <b>칸만 커지고 카드는 그대로</b>다.
//   실제로 그렇게 놓쳐서 "카드가 안 커졌다"는 말을 들었다(2026-09-03).
//
// 나눠 갖는 몫:
//   프리팹 — 칸 크기 · 카드 축소 배율
//   코드   — 어떤 카드를 넣을지 · 덱에 남아 있는지(흐리기)
using UnityEngine;

[DisallowMultipleComponent]
public class DeckCellItemView : MonoBehaviour
{
    [Tooltip("덱에 남지 않은 카드를 흐리게 만드는 데 쓴다. 비우면 이 오브젝트에서 찾는다.")]
    public CanvasGroup canvasGroup;

    [Tooltip("카드 프리팹이 들어갈 자리. 축소 배율(localScale)을 여기에 걸어 둔다.")]
    public RectTransform cardHost;

    [Tooltip("칸을 누르면 좌측에 크게 보여 준다. 비우면 이 오브젝트에서 찾는다. 없어도 동작한다.")]
    public UnityEngine.UI.Button button;

    /// <summary>인스펙터에서 비워 둔 것을 이름으로 채운다. 여러 번 불러도 안전하다.</summary>
    public void ResolveMissingReferences()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (button == null) button = GetComponent<UnityEngine.UI.Button>();

        if (cardHost == null)
        {
            foreach (RectTransform candidate in GetComponentsInChildren<RectTransform>(true))
            {
                if (candidate.name == "CardHost" && candidate.gameObject != gameObject)
                {
                    cardHost = candidate;
                    break;
                }
            }
        }
    }

    /// <summary>빠진 참조를 이름으로 알린다. 하나라도 없으면 false.</summary>
    public bool Validate(out string reason)
    {
        ResolveMissingReferences();

        var missing = new System.Collections.Generic.List<string>();

        if (canvasGroup == null) missing.Add(nameof(canvasGroup));
        if (cardHost == null) missing.Add(nameof(cardHost));

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
