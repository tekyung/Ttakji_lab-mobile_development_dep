using System;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using UnityEngine;

public struct StackZoneSyncContext
{
    public Transform barRoot;
    public IReadOnlyList<Card> engineStack;
    public GameObject cardPrefab;
    public Dictionary<string, GameObject> instanceMap;
    public GameObject adoptCard;
    public float cardWidth;
    public Action<RectTransform> applyCardSize;
}

public static class StackZoneRowUI
{
    private const float DefaultCardWidth = 200f;

    public static void LayoutRow(RectTransform bar, IList<RectTransform> cards, float cardWidth)
    {
        if (bar == null || cards == null || cards.Count == 0)
            return;

        float barWidth = bar.rect.width;
        if (barWidth <= 0f)
            barWidth = 280f;

        if (cardWidth <= 0f)
            cardWidth = DefaultCardWidth;

        int count = cards.Count;
        float halfBar = barWidth * 0.5f;
        float rightCenter = halfBar - cardWidth * 0.5f;

        for (int i = 0; i < count; i++)
        {
            RectTransform cardRect = cards[i];
            if (cardRect == null)
                continue;

            float x;
            if (count == 1)
            {
                x = rightCenter;
            }
            else if (count == 2 && cardWidth * 2f <= barWidth)
            {
                float spacing = (barWidth - cardWidth * 2f) / 3f;
                x = rightCenter - (count - 1 - i) * (cardWidth + spacing);
            }
            else
            {
                float step = (barWidth - cardWidth) / (count - 1);
                x = rightCenter - (count - 1 - i) * step;
            }

            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = new Vector2(x, 0f);
            cardRect.localScale = Vector3.one;
            cardRect.SetSiblingIndex(i);
        }
    }

    public static GameObject FindCardUnderRoot(Transform root, Card card)
    {
        if (root == null || card == null)
            return null;

        foreach (Transform child in root)
        {
            CardUI ui = child.GetComponent<CardUI>();
            if (ui == null)
                continue;

            if (!string.IsNullOrEmpty(card.InstanceId) && ui.myInstanceId == card.InstanceId)
                return child.gameObject;

            if (string.IsNullOrEmpty(card.InstanceId) && ui.myCardID == card.Id)
                return child.gameObject;
        }

        if (!string.IsNullOrEmpty(card.InstanceId))
        {
            Transform byName = root.Find(card.InstanceId);
            if (byName != null)
                return byName.gameObject;
        }

        Transform byId = root.Find(card.Id);
        return byId != null ? byId.gameObject : null;
    }

    public static bool MoveCardToGraveyard(
        Transform root,
        Transform graveyard,
        Card card,
        Dictionary<string, GameObject> instanceMap,
        Action<RectTransform> layout)
    {
        if (graveyard == null || card == null)
            return false;

        GameObject cardObj = FindCardUnderRoot(root, card);
        if (cardObj == null && CardBoardRegistry.TryGet(card, out GameObject registryObj))
            cardObj = registryObj;

        if (cardObj == null)
        {
            Debug.LogWarning(
                $"[StackZoneRowUI] 스택 바에서 카드를 찾지 못함 — '{card.Name}'({card.Id})");
            return false;
        }

        cardObj.transform.SetParent(graveyard, true);
        if (layout != null)
            layout(cardObj.GetComponent<RectTransform>());

        if (!string.IsNullOrEmpty(card.InstanceId))
            instanceMap?.Remove(card.InstanceId);

        return true;
    }

    public static void LayoutGraveyardCard(RectTransform rect, float cardWidth = DefaultCardWidth)
    {
        if (rect == null)
            return;

        if (cardWidth <= 0f)
            cardWidth = DefaultCardWidth;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(cardWidth, 280f);
        rect.localScale = Vector3.one;
    }

    public static void SyncFromEngineStack(StackZoneSyncContext ctx)
    {
        if (ctx.barRoot == null || ctx.engineStack == null || ctx.instanceMap == null)
            return;

        var engineIds = new HashSet<string>();
        foreach (Card engineCard in ctx.engineStack)
        {
            if (!string.IsNullOrEmpty(engineCard.InstanceId))
                engineIds.Add(engineCard.InstanceId);
        }

        var keysToRemove = new List<string>();
        foreach (KeyValuePair<string, GameObject> pair in ctx.instanceMap)
        {
            if (!engineIds.Contains(pair.Key))
                keysToRemove.Add(pair.Key);
        }

        foreach (string key in keysToRemove)
            ctx.instanceMap.Remove(key);

        GameObject adoptRemaining = ctx.adoptCard;
        var cardRects = new List<RectTransform>();

        foreach (Card engineCard in ctx.engineStack)
        {
            if (string.IsNullOrEmpty(engineCard.InstanceId))
                continue;

            GameObject cardObj = GetOrCreateCardObject(ctx, engineCard, ref adoptRemaining);
            if (cardObj == null)
                continue;

            PrepareStackCard(cardObj, engineCard, ctx);
            ctx.instanceMap[engineCard.InstanceId] = cardObj;

            RectTransform rect = cardObj.GetComponent<RectTransform>();
            if (rect != null)
                cardRects.Add(rect);
        }

        RectTransform barRect = ctx.barRoot as RectTransform;
        if (barRect != null)
            LayoutRow(barRect, cardRects, ctx.cardWidth > 0f ? ctx.cardWidth : DefaultCardWidth);
    }

    private static GameObject GetOrCreateCardObject(
        StackZoneSyncContext ctx,
        Card engineCard,
        ref GameObject adoptRemaining)
    {
        if (ctx.instanceMap.TryGetValue(engineCard.InstanceId, out GameObject existing) && existing != null)
            return existing;

        if (CardBoardRegistry.TryGet(engineCard.InstanceId, out GameObject registryObj))
            return registryObj;

        if (adoptRemaining != null)
        {
            GameObject adopted = adoptRemaining;
            adoptRemaining = null;
            return adopted;
        }

        if (ctx.cardPrefab == null)
        {
            Debug.LogError(
                $"[StackZoneRowUI] registry miss, no Instantiate in play — '{engineCard.Name}'({engineCard.InstanceId})");
            return null;
        }

        Debug.LogError(
            $"[StackZoneRowUI] registry miss, Instantiate fallback disabled — '{engineCard.Name}'({engineCard.InstanceId})");
        return null;
    }

    private static void PrepareStackCard(GameObject cardObj, Card engineCard, StackZoneSyncContext ctx)
    {
        CardMoveTween.Complete(cardObj.transform);
        CardBoardRegistry.ResetVisualState(cardObj);
        cardObj.transform.SetParent(ctx.barRoot, true);

        CardUI ui = cardObj.GetComponent<CardUI>();
        if (ui != null)
        {
            ui.BindEngineCard(engineCard);
            ui.SetFaceDown(false);
        }
        else
        {
            cardObj.name = string.IsNullOrEmpty(engineCard.InstanceId)
                ? engineCard.Id
                : engineCard.InstanceId;
        }

        CardInteraction interaction = cardObj.GetComponent<CardInteraction>();
        if (interaction != null)
            interaction.enabled = false;

        ctx.applyCardSize?.Invoke(cardObj.GetComponent<RectTransform>());
    }
}
