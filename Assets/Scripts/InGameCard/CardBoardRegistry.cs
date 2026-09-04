using System;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;

public struct ZoneMoveContext
{
    public Transform Hand;
    public Transform SetZone;
    public Transform Graveyard;
    public Transform Deck;
    public Transform StackBar;
    public Transform Battlefield;
    public Transform HiddenPool;
    public Transform ResourceZone;
    public float CardWidth;
    public bool UseCardBackInHand;
    public Action<Transform> OnHandLayoutRequested;
    public Action<string> OnPhysicalStackSync;
}

public sealed class CardBoardEntry
{
    public GameObject GameObject;
    public Player Owner;
    public bool IsResource;
}

public static class CardBoardRegistry
{
    private static readonly Dictionary<string, CardBoardEntry> Entries = new Dictionary<string, CardBoardEntry>();
    private static readonly Dictionary<HorizontalLayoutGroup, int> HandLayoutLocks =
        new Dictionary<HorizontalLayoutGroup, int>();

    public static int Count => Entries.Count;

    public static void ClearPool()
    {
        foreach (CardBoardEntry entry in Entries.Values)
        {
            if (entry?.GameObject != null)
                UnityEngine.Object.Destroy(entry.GameObject);
        }

        Entries.Clear();
    }

    public static Transform CreateHiddenPool(string name, Transform fallbackParent, params Transform[] canvasHints)
    {
        var poolObj = new GameObject(name, typeof(RectTransform));
        Transform parent = null;
        foreach (Transform hint in canvasHints)
        {
            if (hint == null)
                continue;

            Canvas canvas = hint.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                parent = canvas.transform;
                break;
            }
        }

        if (parent == null)
            parent = fallbackParent;

        poolObj.transform.SetParent(parent, false);
        RectTransform rect = poolObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(8000f, 0f);
        rect.sizeDelta = Vector2.zero;
        return poolObj.transform;
    }

    public static void BuildPool(
        Player owner,
        IEnumerable<Card> cards,
        GameObject prefab,
        Transform hiddenRoot)
    {
        if (owner == null || prefab == null || hiddenRoot == null || cards == null)
            return;

        foreach (Card card in cards)
        {
            if (card == null || string.IsNullOrEmpty(card.InstanceId))
                continue;

            if (Entries.ContainsKey(card.InstanceId))
                continue;

            GameObject cardObj = UnityEngine.Object.Instantiate(prefab, hiddenRoot);
            cardObj.name = card.InstanceId;

            CardUI ui = cardObj.GetComponent<CardUI>();
            if (ui != null)
                ui.BindEngineCard(card);

            bool isResource = card.Type == CardType.Resource;
            cardObj.SetActive(false);

            Entries[card.InstanceId] = new CardBoardEntry
            {
                GameObject = cardObj,
                Owner = owner,
                IsResource = isResource
            };
        }
    }

    public static bool TryGet(string instanceId, out GameObject gameObject)
    {
        gameObject = null;
        if (string.IsNullOrEmpty(instanceId))
            return false;

        if (!Entries.TryGetValue(instanceId, out CardBoardEntry entry) || entry?.GameObject == null)
            return false;

        gameObject = entry.GameObject;
        return true;
    }

    public static bool TryGet(Card card, out GameObject gameObject)
    {
        gameObject = null;
        if (card == null)
            return false;

        return TryGet(card.InstanceId, out gameObject);
    }

    public static bool ApplyZoneMove(
        Card card,
        Player owner,
        ZoneType fromZone,
        ZoneType toZone,
        ZoneMoveContext ctx)
    {
        if (card == null || ctx.HiddenPool == null)
            return false;

        if (!TryGet(card, out GameObject cardObj))
        {
            Debug.LogWarning(
                $"[CardBoardRegistry] GO 없음 — '{card.Name}'({card.InstanceId}) {fromZone}→{toZone}");
            return false;
        }

        if (ShouldHideInPool(toZone, card))
        {
            if (TryTweenIntoHiddenZone(cardObj, card, fromZone, toZone, ctx))
            {
                ctx.OnPhysicalStackSync?.Invoke(card.InstanceId);
                return true;
            }

            PlaceHidden(cardObj, ctx.HiddenPool);
            ctx.OnPhysicalStackSync?.Invoke(card.InstanceId);
            return true;
        }

        Transform targetRoot = ResolveZoneRoot(toZone, ctx);
        if (targetRoot == null)
            return false;

        bool wasActive = cardObj.activeInHierarchy;
        CardMoveTween.Complete(cardObj.transform);
        Vector3 fromWorld = CaptureFromWorld(cardObj, wasActive, fromZone, ctx);

        HorizontalLayoutGroup handLayout = toZone == ZoneType.Hand && ctx.Hand != null
            ? ctx.Hand.GetComponent<HorizontalLayoutGroup>()
            : null;
        if (handLayout != null)
            handLayout.enabled = false;

        cardObj.SetActive(true);
        if (cardObj.transform.parent != targetRoot)
            cardObj.transform.SetParent(targetRoot, wasActive);

        CardUI ui = cardObj.GetComponent<CardUI>();
        if (ui != null)
            ui.BindEngineCard(card);

        CardInteraction interaction = cardObj.GetComponent<CardInteraction>();
        ApplyZonePresentation(card, toZone, fromZone, cardObj, ui, interaction, ctx);

        RectTransform rect = cardObj.GetComponent<RectTransform>();

        if (toZone == ZoneType.Hand)
        {
            CardMoveTween.CompleteChildren(ctx.Hand, cardObj.transform);
            ApplyHandSpacing(ctx.Hand, ctx.CardWidth);
            ctx.OnHandLayoutRequested?.Invoke(ctx.Hand);
            LockHandLayout(handLayout);
        }
        else if (toZone == ZoneType.SetZone || toZone == ZoneType.BattlefieldZone)
        {
            if (rect != null)
                rect.anchoredPosition = Vector2.zero;
        }

        if (toZone == ZoneType.Deck || toZone == ZoneType.Graveyard
            || fromZone == ZoneType.Deck || fromZone == ZoneType.Graveyard)
            ctx.OnPhysicalStackSync?.Invoke(card.InstanceId);

        Vector2 toAnchored = rect != null ? rect.anchoredPosition : Vector2.zero;
        if ((toZone == ZoneType.Deck || toZone == ZoneType.Graveyard) && owner != null && rect != null)
        {
            DeckGraveyardStackUI.ApplyStackedCardFrame(rect, ctx.CardWidth);
            IReadOnlyList<Card> stack = toZone == ZoneType.Deck ? owner.Deck : owner.Graveyard;
            toAnchored = DeckGraveyardStackUI.GetStackedAnchoredPosition(
                stack,
                card,
                topIsIndexZero: toZone == ZoneType.Deck);
        }

        Vector2 fromAnchored = CardMoveTween.WorldToAnchored(rect, fromWorld);
        CardMoveTween.Play(cardObj.transform, fromAnchored, toAnchored, () =>
        {
            UnlockHandLayout(handLayout, ctx.Hand, ctx.CardWidth);
        });
        return true;
    }

    public static void ApplyHandSpacing(Transform hand, float cardWidth)
    {
        if (hand == null)
            return;

        HorizontalLayoutGroup layoutGroup = hand.GetComponent<HorizontalLayoutGroup>();
        RectTransform handRect = hand as RectTransform;
        if (layoutGroup == null || handRect == null)
            return;

        layoutGroup.enabled = true;
        int cardCount = CountCardObjects(hand);
        if (cardCount < 2)
        {
            layoutGroup.spacing = 0f;
        }
        else
        {
            if (cardWidth <= 0f)
                cardWidth = 120f;

            float panelWidth = handRect.rect.width;
            float totalCardWidth = cardWidth * cardCount;
            layoutGroup.spacing = totalCardWidth > panelWidth
                ? (panelWidth - totalCardWidth) / (cardCount - 1)
                : -20f;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(handRect);
    }

    private static void LockHandLayout(HorizontalLayoutGroup layout)
    {
        if (layout == null)
            return;

        HandLayoutLocks.TryGetValue(layout, out int count);
        HandLayoutLocks[layout] = count + 1;
        layout.enabled = false;
    }

    private static void UnlockHandLayout(HorizontalLayoutGroup layout, Transform hand, float cardWidth)
    {
        if (layout == null)
            return;

        if (!HandLayoutLocks.TryGetValue(layout, out int count))
        {
            ApplyHandSpacing(hand, cardWidth);
            return;
        }

        count--;
        if (count <= 0)
        {
            HandLayoutLocks.Remove(layout);
            ApplyHandSpacing(hand, cardWidth);
            return;
        }

        HandLayoutLocks[layout] = count;
    }

    public static void PlaceHidden(GameObject cardObj, Transform hiddenPool)
    {
        if (cardObj == null || hiddenPool == null)
            return;

        CardMoveTween.Complete(cardObj.transform);
        ResetVisualState(cardObj);
        cardObj.transform.SetParent(hiddenPool, false);
        cardObj.SetActive(false);

        CardInteraction interaction = cardObj.GetComponent<CardInteraction>();
        if (interaction != null)
            interaction.enabled = false;
    }

    public static void ResetVisualState(GameObject cardObj)
    {
        if (cardObj == null)
            return;

        RectTransform rect = cardObj.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        CardInteraction interaction = cardObj.GetComponent<CardInteraction>();
        if (interaction != null)
            interaction.DeselectCard();

        Canvas canvas = cardObj.GetComponent<Canvas>();
        if (canvas != null)
            canvas.overrideSorting = false;

        CanvasGroup group = cardObj.GetComponent<CanvasGroup>();
        if (group != null)
            group.alpha = 1f;
    }

    public static int CountCardObjects(Transform root)
    {
        if (root == null)
            return 0;

        int count = 0;
        foreach (Transform child in root)
        {
            if (child != null && child.GetComponent<CardUI>() != null)
                count++;
        }

        return count;
    }

    public static void HideStrayCards(Transform root, string keepInstanceId, Transform hiddenPool)
    {
        if (root == null || hiddenPool == null)
            return;

        var extras = new List<GameObject>();
        foreach (Transform child in root)
        {
            CardUI ui = child.GetComponent<CardUI>();
            if (ui == null)
                continue;

            if (string.IsNullOrEmpty(keepInstanceId) || ui.myInstanceId != keepInstanceId)
                extras.Add(child.gameObject);
        }

        foreach (GameObject extra in extras)
            PlaceHidden(extra, hiddenPool);
    }

    private static bool ShouldHideInPool(ZoneType zone, Card card)
    {
        if (zone == ZoneType.Graveyard)
            return false;

        if (card != null && card.Type == CardType.Resource)
            return true;

        return zone == ZoneType.ResourceDeck
               || zone == ZoneType.ResourceZone
               || zone == ZoneType.PlayBuffer;
    }

    private static Vector3 CaptureFromWorld(
        GameObject cardObj,
        bool wasActive,
        ZoneType fromZone,
        ZoneMoveContext ctx)
    {
        if (wasActive && cardObj != null)
            return cardObj.transform.position;

        Transform origin = ResolveOriginRoot(fromZone, ctx);
        if (origin != null)
            return origin.position;

        if (ctx.HiddenPool != null)
            return ctx.HiddenPool.position;

        return cardObj != null ? cardObj.transform.position : Vector3.zero;
    }

    private static Transform ResolveOriginRoot(ZoneType zone, ZoneMoveContext ctx)
    {
        return zone switch
        {
            ZoneType.Deck => ctx.Deck,
            ZoneType.ResourceZone => ctx.ResourceZone != null ? ctx.ResourceZone : ctx.Deck,
            ZoneType.Graveyard => ctx.Graveyard,
            ZoneType.Hand => ctx.Hand,
            ZoneType.SetZone => ctx.SetZone,
            ZoneType.StackZone => ctx.StackBar,
            ZoneType.BattlefieldZone => ctx.Battlefield,
            _ => null
        };
    }

    private static bool TryTweenIntoHiddenZone(
        GameObject cardObj,
        Card card,
        ZoneType fromZone,
        ZoneType toZone,
        ZoneMoveContext ctx)
    {
        if (toZone != ZoneType.ResourceZone || ctx.ResourceZone == null || cardObj == null)
            return false;

        bool wasActive = cardObj.activeInHierarchy;
        if (!wasActive && fromZone != ZoneType.SetZone && fromZone != ZoneType.Graveyard)
            return false;

        CardMoveTween.Complete(cardObj.transform);
        Vector3 fromWorld = CaptureFromWorld(cardObj, wasActive, fromZone, ctx);

        cardObj.SetActive(true);
        if (cardObj.transform.parent != ctx.ResourceZone)
            cardObj.transform.SetParent(ctx.ResourceZone, wasActive);

        CardUI ui = cardObj.GetComponent<CardUI>();
        if (ui != null)
            ui.BindEngineCard(card);

        RectTransform rect = cardObj.GetComponent<RectTransform>();
        LayoutCardSize(rect, ctx.CardWidth);

        Vector2 fromAnchored = CardMoveTween.WorldToAnchored(rect, fromWorld);
        if (rect != null)
            rect.anchoredPosition = Vector2.zero;

        CardMoveTween.Play(cardObj.transform, fromAnchored, rect != null ? rect.anchoredPosition : Vector2.zero, () =>
        {
            PlaceHidden(cardObj, ctx.HiddenPool);
        });
        return true;
    }

    private static Transform ResolveZoneRoot(ZoneType zone, ZoneMoveContext ctx)
    {
        return zone switch
        {
            ZoneType.Hand => ctx.Hand,
            ZoneType.SetZone => ctx.SetZone,
            ZoneType.Graveyard => ctx.Graveyard,
            ZoneType.Deck => ctx.Deck,
            ZoneType.StackZone => ctx.StackBar,
            ZoneType.BattlefieldZone => ctx.Battlefield,
            _ => null
        };
    }

    private static void ApplyZonePresentation(
        Card card,
        ZoneType toZone,
        ZoneType fromZone,
        GameObject cardObj,
        CardUI ui,
        CardInteraction interaction,
        ZoneMoveContext ctx)
    {
        ResetVisualState(cardObj);

        RectTransform rect = cardObj.GetComponent<RectTransform>();
        float cardWidth = ctx.CardWidth > 0f ? ctx.CardWidth : 120f;

        CanvasGroup group = cardObj.GetComponent<CanvasGroup>();
        GraphicRaycaster raycaster = cardObj.GetComponent<GraphicRaycaster>();

        switch (toZone)
        {
            case ZoneType.Hand:
                LayoutCardSize(rect, cardWidth);
                if (ui != null)
                    ui.SetFaceDown(ctx.UseCardBackInHand);

                if (interaction != null)
                {
                    interaction.enabled = !ctx.UseCardBackInHand;
                    interaction.isInSetZone = false;
                }

                SetRaycast(group, raycaster, !ctx.UseCardBackInHand);
                break;

            case ZoneType.SetZone:
                LayoutCardSize(rect, cardWidth);
                if (ui != null)
                    ui.SetFaceDown(!card.IsFaceUp);

                if (interaction != null)
                {
                    interaction.enabled = !ctx.UseCardBackInHand;
                    interaction.isInSetZone = !ctx.UseCardBackInHand;
                }

                SetRaycast(group, raycaster, !ctx.UseCardBackInHand);
                break;

            case ZoneType.Graveyard:
                if (ui != null)
                    ui.SetFaceDown(!card.IsFaceUp);

                if (interaction != null)
                {
                    interaction.enabled = false;
                    interaction.isInSetZone = false;
                }

                SetRaycast(group, raycaster, false);
                break;

            case ZoneType.Deck:
                if (ui != null)
                    ui.SetFaceDown(true);

                if (interaction != null)
                {
                    interaction.enabled = false;
                    interaction.isInSetZone = false;
                }

                SetRaycast(group, raycaster, false);
                break;

            case ZoneType.BattlefieldZone:
                LayoutCardSize(rect, cardWidth);
                if (ui != null)
                    ui.SetFaceDown(false);

                if (interaction != null)
                {
                    interaction.enabled = false;
                    interaction.isInSetZone = false;
                }

                SetRaycast(group, raycaster, false);
                break;

            case ZoneType.StackZone:
                if (interaction != null)
                {
                    interaction.enabled = false;
                    interaction.isInSetZone = false;
                }

                SetRaycast(group, raycaster, false);
                break;
        }
    }

    private static void SetRaycast(CanvasGroup group, GraphicRaycaster raycaster, bool enabled)
    {
        if (group != null)
        {
            group.blocksRaycasts = enabled;
            group.interactable = enabled;
        }

        if (raycaster != null)
            raycaster.enabled = enabled;
    }

    private static void LayoutCardSize(RectTransform rect, float cardWidth)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(cardWidth, 168f);
        rect.localScale = Vector3.one;
    }
}
