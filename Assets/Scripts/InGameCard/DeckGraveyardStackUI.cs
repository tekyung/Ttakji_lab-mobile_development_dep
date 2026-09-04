using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;

public struct DeckGraveyardSyncContext
{
    public Transform ZoneRoot;
    public IReadOnlyList<Card> EngineList;
    public Transform HiddenPool;
    public float CardWidth;
    public bool FaceDown;
    public bool TopIsIndexZero;
    public float StackOffsetPx;
    public string SkipLayoutInstanceId;
}

public static class DeckGraveyardStackUI
{
    private const float DefaultCardWidth = 120f;
    private const float DefaultOffset = 3f;
    private const string DeckAnchorName = "DeckCardAnchor";

    public static Transform EnsureDeckCardAnchor(Transform deckRoot)
    {
        if (deckRoot == null || deckRoot.name == DeckAnchorName)
            return deckRoot;

        Transform existing = deckRoot.Find(DeckAnchorName);
        if (existing != null)
            return existing;

        var anchorObject = new GameObject(DeckAnchorName, typeof(RectTransform));
        RectTransform anchor = anchorObject.GetComponent<RectTransform>();
        anchor.SetParent(deckRoot, false);
        anchor.anchorMin = new Vector2(0.5f, 0.5f);
        anchor.anchorMax = new Vector2(0.5f, 0.5f);
        anchor.pivot = new Vector2(0.5f, 0.5f);
        anchor.anchoredPosition = Vector2.zero;
        anchor.sizeDelta = new Vector2(DefaultCardWidth, 168f);
        anchor.localScale = Vector3.one;
        return anchor;
    }

    public static Vector2 GetStackedAnchoredPosition(
        IReadOnlyList<Card> engineList,
        Card card,
        bool topIsIndexZero,
        float offset = DefaultOffset)
    {
        if (engineList == null || card == null)
            return Vector2.zero;

        if (offset <= 0f)
            offset = DefaultOffset;

        int index = -1;
        for (int i = 0; i < engineList.Count; i++)
        {
            Card entry = engineList[i];
            if (entry != null && entry.InstanceId == card.InstanceId)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
            return Vector2.zero;

        int layerFromBottom = topIsIndexZero ? (engineList.Count - 1 - index) : index;
        return new Vector2(layerFromBottom * offset, layerFromBottom * offset);
    }

    /// <summary>
    /// 엔진 목록에 맞춰 덱·폐기존 더미를 다시 세운다.
    ///
    /// ★ <b>빈 목록으로 부르면 그 존이 통째로 비워진다.</b> HideExtras가 원하지 않는 카드를
    ///   전부 숨김 풀로 보내기 때문이다. 그래서 "존에는 카드가 있는데 목록이 비어 있다"는
    ///   조합은 거의 언제나 <b>잘못된 Player를 넘긴 것</b>이다 — 실제로 게스트에서
    ///   임시로 만든 빈 Player가 흘러들어와 덱이 껐다 켜졌다.
    ///   진짜로 덱을 비우는 경우는 게임이 끝날 때뿐이고 그때는 이 경로를 타지 않는다.
    ///   그래서 그런 호출은 <b>거절하고 알린다.</b> 조용히 지나가면 원인을 찾는 데 며칠이 걸린다.
    /// </summary>
    public static void Sync(DeckGraveyardSyncContext ctx)
    {
        if (ctx.ZoneRoot == null || ctx.EngineList == null)
            return;

        if (ctx.EngineList.Count == 0 && HasCardChild(ctx.ZoneRoot))
        {
            Debug.LogWarning(
                $"[DeckGraveyardStackUI] '{ctx.ZoneRoot.name}'에 카드가 있는데 엔진 목록이 비어 있다. " +
                "잘못된 Player가 넘어온 것으로 보고 이번 정리를 건너뛴다.");
            return;
        }

        float cardWidth = ctx.CardWidth > 0f ? ctx.CardWidth : DefaultCardWidth;
        float offset = ctx.StackOffsetPx > 0f ? ctx.StackOffsetPx : DefaultOffset;

        var wanted = new HashSet<string>();
        var rects = new List<RectTransform>();

        foreach (Card engineCard in ctx.EngineList)
        {
            if (engineCard == null || string.IsNullOrEmpty(engineCard.InstanceId))
                continue;

            if (!CardBoardRegistry.TryGet(engineCard, out GameObject cardObj) || cardObj == null)
                continue;

            wanted.Add(engineCard.InstanceId);
            PrepareStackedCard(cardObj, engineCard, ctx.ZoneRoot, ctx.FaceDown, cardWidth);
            RectTransform rect = cardObj.GetComponent<RectTransform>();
            if (rect != null)
                rects.Add(rect);
        }

        HideExtras(ctx.ZoneRoot, wanted, ctx.HiddenPool);
        LayoutStack(rects, ctx.TopIsIndexZero, offset, ctx.SkipLayoutInstanceId);
    }

    public static void ApplyStackedCardFrame(RectTransform rect, float cardWidth)
    {
        if (rect == null)
            return;

        if (cardWidth <= 0f)
            cardWidth = DefaultCardWidth;

        Vector2 parentPivot = Vector2.one * 0.5f;
        RectTransform parentRect = rect.parent as RectTransform;
        if (parentRect != null)
            parentPivot = parentRect.pivot;

        rect.anchorMin = parentPivot;
        rect.anchorMax = parentPivot;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(cardWidth, 168f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    public static void LayoutStackedCard(RectTransform rect, int layerFromBottom, float cardWidth, float offset)
    {
        if (rect == null)
            return;

        if (offset <= 0f)
            offset = DefaultOffset;

        ApplyStackedCardFrame(rect, cardWidth);
        rect.anchoredPosition = new Vector2(layerFromBottom * offset, layerFromBottom * offset);
    }

    private static void PrepareStackedCard(
        GameObject cardObj,
        Card engineCard,
        Transform zoneRoot,
        bool faceDown,
        float cardWidth)
    {
        // ★ 날아가는 중인 카드는 건드리지 않는다.
        //   예전에는 무조건 Complete를 걸어, 이동이 촘촘히 들어오면(게스트가 그렇다)
        //   0.36초짜리 트윈이 몇 프레임 만에 잘려 카드가 <b>순간이동</b>했다.
        //   호스트는 행동 사이가 0.5초라 트윈이 늘 완주해서 이 문제를 겪지 않았다.
        bool moving = CardMoveTween.IsPlaying(cardObj.transform);
        if (!moving)
        {
            CardMoveTween.Complete(cardObj.transform);
            CardBoardRegistry.ResetVisualState(cardObj);
        }

        cardObj.SetActive(true);
        if (cardObj.transform.parent != zoneRoot)
            cardObj.transform.SetParent(zoneRoot, true);

        CardUI ui = cardObj.GetComponent<CardUI>();
        if (ui != null)
        {
            ui.BindEngineCard(engineCard);
            ui.SetFaceDown(faceDown);
        }

        CardInteraction interaction = cardObj.GetComponent<CardInteraction>();
        if (interaction != null)
        {
            interaction.enabled = false;
            interaction.isInSetZone = false;
        }

        CanvasGroup group = cardObj.GetComponent<CanvasGroup>();
        if (group != null)
        {
            group.alpha = 1f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        GraphicRaycaster raycaster = cardObj.GetComponent<GraphicRaycaster>();
        if (raycaster != null)
            raycaster.enabled = false;

        // 날아가는 중이면 자리(anchoredPosition)를 건드리는 프레임 재설정도 미룬다.
        //   도착하면 다음 Sync가 제자리에 앉힌다.
        if (!moving)
        {
            RectTransform rect = cardObj.GetComponent<RectTransform>();
            if (rect != null)
                ApplyStackedCardFrame(rect, cardWidth);
        }
    }

    /// <summary>그 존에 카드 GO가 하나라도 붙어 있는가.</summary>
    private static bool HasCardChild(Transform zoneRoot)
    {
        foreach (Transform child in zoneRoot)
            if (child.GetComponent<CardUI>() != null) return true;

        return false;
    }

    private static void HideExtras(Transform zoneRoot, HashSet<string> wanted, Transform hiddenPool)
    {
        var extras = new List<GameObject>();
        foreach (Transform child in zoneRoot)
        {
            CardUI ui = child.GetComponent<CardUI>();
            if (ui == null)
                continue;

            if (string.IsNullOrEmpty(ui.myInstanceId) || !wanted.Contains(ui.myInstanceId))
                extras.Add(child.gameObject);
        }

        foreach (GameObject extra in extras)
            CardBoardRegistry.PlaceHidden(extra, hiddenPool);
    }

    private static void LayoutStack(
        IList<RectTransform> rects,
        bool topIsIndexZero,
        float offset,
        string skipLayoutInstanceId)
    {
        int count = rects.Count;
        if (count == 0)
            return;

        int reserved = 0;
        Transform parent = rects[0] != null ? rects[0].parent : null;
        if (parent != null)
        {
            foreach (Transform child in parent)
            {
                if (child != null && child.GetComponent<CardUI>() == null)
                    reserved++;
            }
        }

        for (int i = 0; i < count; i++)
        {
            int layerFromBottom = topIsIndexZero ? (count - 1 - i) : i;
            bool skip = false;
            if (!string.IsNullOrEmpty(skipLayoutInstanceId) && rects[i] != null)
            {
                CardUI ui = rects[i].GetComponent<CardUI>();
                skip = ui != null && ui.myInstanceId == skipLayoutInstanceId;
            }

            // ★ 날아가는 중인 카드는 자리를 잡아 주지 않는다.
            //   트윈이 매 프레임 anchoredPosition을 쓰는데 여기서 덮으면 카드가 제자리에 붙박인다.
            //   도착하면 다음 Sync가 앉힌다. 겹침 순서(SetSiblingIndex)는 그대로 맞춘다.
            if (!skip && rects[i] != null && CardMoveTween.IsPlaying(rects[i]))
                skip = true;

            if (!skip)
                LayoutStackedCard(rects[i], layerFromBottom, DefaultCardWidth, offset);
            if (rects[i] != null)
                rects[i].SetSiblingIndex(reserved + layerFromBottom);
        }
    }
}
