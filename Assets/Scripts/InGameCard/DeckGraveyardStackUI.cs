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
    private const float DefaultCardWidth = 200f;
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
        anchor.sizeDelta = new Vector2(DefaultCardWidth, 280f);
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

    public static void Sync(DeckGraveyardSyncContext ctx)
    {
        if (ctx.ZoneRoot == null || ctx.EngineList == null)
            return;

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
        rect.sizeDelta = new Vector2(cardWidth, 280f);
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
        CardMoveTween.Complete(cardObj.transform);
        CardBoardRegistry.ResetVisualState(cardObj);

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

        RectTransform rect = cardObj.GetComponent<RectTransform>();
        if (rect != null)
            ApplyStackedCardFrame(rect, cardWidth);
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

            if (!skip)
                LayoutStackedCard(rects[i], layerFromBottom, DefaultCardWidth, offset);
            if (rects[i] != null)
                rects[i].SetSiblingIndex(reserved + layerFromBottom);
        }
    }
}
