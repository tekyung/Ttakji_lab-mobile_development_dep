using System;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;
using TCG_Project.Scripts.Systems; // GameRules.DefaultCardBackPath
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerUIManager : MonoBehaviour
{
    public static PlayerUIManager Instance;

    [Header("Player UI Zones")]
    public Transform myHandTransform;
    public Transform mySetZoneTransform;
    public Transform myGraveyardTransform;
    public Transform myDeckTransform;
    public Transform myStackZoneRoot;
    public float myStackCardWidth = 200f;
    public Transform myBattlefieldTransform;
    public Transform myResourceZoneTransform;

    [Header("Player Status UI")]
    public TextMeshProUGUI myLifeText;
    public TextMeshProUGUI myResourceText;
    public TextMeshProUGUI myDeckCountText;
    public TextMeshProUGUI myResourceDeckCountText;

    [Header("Prefabs")]
    public GameObject myCardPrefab;

    public GameObject readyButtonObj;

    // ─── 세트 확정 전 미리보기 (잔상) ───────────────────────────────────
    // 카드 GO는 손패를 떠나지 않는다. 선택 표시는 "손패 카드 흐리게 + 세트존에 흐린 뒷면 잔상"으로만 한다.
    // 이렇게 하면 엔진 존과 hierarchy가 계속 일치하고, OnCardMove도 발행되지 않아
    // 온라인(사람 vs 사람)에서 확정 전 정보가 새지 않는다.
    [Header("Set Zone Preview (확정 전 잔상)")]
    public Vector2 setGhostSize = new Vector2(200f, 280f);
    public float setGhostBorderThickness = 6f;
    [Range(0f, 1f)] public float pendingHandCardAlpha = 0.4f;
    [Range(0f, 1f)] public float setGhostAlpha = 0.45f;
    [Range(0f, 1f)] public float setGhostConfirmedAlpha = 0.8f;
    [Tooltip("공개 선택 시 잔상 테두리 색")]
    public Color revealBorderColor = new Color(0.25f, 0.55f, 1f, 1f);
    [Tooltip("폐기 선택 시 잔상 테두리 색")]
    public Color abandonBorderColor = new Color(0.92f, 0.30f, 0.30f, 1f);

    private Action<Card> pendingSetCallback;
    private Player localPlayer;
    private Card pendingCardToSet;
    private GameObject pendingCardObj;
    private bool pendingIsReveal = true;

    private GameObject _setGhost;
    private CanvasGroup _setGhostGroup;
    private Image _setGhostBorder;
    private Image _setGhostBack;
    private TextMeshProUGUI _setGhostLabel;
    private CanvasGroup _dimmedHandCard;
    private readonly Dictionary<string, GameObject> _myStackCardObjects = new Dictionary<string, GameObject>();
    private Transform _humanHiddenPool;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void OnEnable()
    {
        EventManager.OnGameStart += HandleGameStart;
        EventManager.OnGameSet += HandleGameEnd;
        EventManager.OnCardMove += HandleCardMove;
        EventManager.OnCardDraw += HandleCardDraw;
        EventManager.OnCardStacked += HandleCardStacked;
        EventManager.OnCardStateChanged += HandleCardStateChanged;
        EventManager.OnRequireSetPhaseAction += HandleRequireSet;
        EventManager.OnRequireOpenPhaseAction += HandleRequireOpen;
        EventManager.OnCardSet += HandleCardSet;
        EventManager.OnLifeChange += HandleLifeChange;
        EventManager.OnResourceChange += HandleResourceChange;
        EventManager.OnTurnEnd += HandleTurnEnd;
        EventManager.OnEndPhase += HandleEndPhase;
    }

    private void OnDisable()
    {
        EventManager.OnGameStart -= HandleGameStart;
        EventManager.OnGameSet -= HandleGameEnd;
        EventManager.OnCardMove -= HandleCardMove;
        EventManager.OnCardDraw -= HandleCardDraw;
        EventManager.OnCardStacked -= HandleCardStacked;
        EventManager.OnCardStateChanged -= HandleCardStateChanged;
        EventManager.OnRequireSetPhaseAction -= HandleRequireSet;
        EventManager.OnRequireOpenPhaseAction -= HandleRequireOpen;
        EventManager.OnCardSet -= HandleCardSet;
        EventManager.OnLifeChange -= HandleLifeChange;
        EventManager.OnResourceChange -= HandleResourceChange;
        EventManager.OnTurnEnd -= HandleTurnEnd;
        EventManager.OnEndPhase -= HandleEndPhase;
    }

    public void InitializeHumanCardPool(Player human)
    {
        myDeckTransform = DeckGraveyardStackUI.EnsureDeckCardAnchor(myDeckTransform);

        if (human == null || human.Type != UserType.Human || myCardPrefab == null)
            return;

        localPlayer = human;
        var poolCards = new List<Card>();
        poolCards.AddRange(human.Deck);
        poolCards.AddRange(human.ResourceDeck);

        CardBoardRegistry.BuildPool(human, poolCards, myCardPrefab, GetHumanHiddenPool());
        SyncPhysicalStacks(human);
        RefreshDeckCounts(human);
        RefreshHumanStatus(human);
    }

    private void HandleGameStart(Player p1, Player p2)
    {
        RefreshHumanStatus(ResolveHumanPlayer(p1, p2));
    }

    private void RefreshHumanStatus(Player human)
    {
        if (human == null || human.Type != UserType.Human)
            return;

        HandleLifeChange(human, human.LifeTokens);
        HandleResourceChange(human, human.GetResourceCount());
    }

    public Transform HiddenPool => GetHumanHiddenPool();

    private void HandleGameEnd(Player winner)
    {
        CardBoardRegistry.ClearPool();
        _myStackCardObjects.Clear();
        ClearPendingSelection();
    }

    public static Player ResolveHumanPlayer(Player p1, Player p2)
    {
        if (p1 != null && p1.Type == UserType.Human)
            return p1;

        if (p2 != null && p2.Type == UserType.Human)
            return p2;

        return null;
    }

    public bool CanPlaceCardInSetZone()
    {
        if (localPlayer != null && localPlayer.SetZoneCard != null)
            return false;

        return CardBoardRegistry.CountCardObjects(mySetZoneTransform) == 0;
    }

    private Transform GetHumanHiddenPool()
    {
        if (_humanHiddenPool == null)
        {
            _humanHiddenPool = CardBoardRegistry.CreateHiddenPool(
                "HumanHiddenCardPool",
                transform,
                myHandTransform,
                mySetZoneTransform,
                myDeckTransform,
                myGraveyardTransform);
        }

        return _humanHiddenPool;
    }

    private ZoneMoveContext CreateHumanZoneContext()
    {
        return new ZoneMoveContext
        {
            Hand = myHandTransform,
            SetZone = mySetZoneTransform,
            Graveyard = myGraveyardTransform,
            Deck = myDeckTransform,
            StackBar = myStackZoneRoot,
            Battlefield = myBattlefieldTransform,
            HiddenPool = GetHumanHiddenPool(),
            ResourceZone = myResourceZoneTransform,
            CardWidth = myStackCardWidth,
            UseCardBackInHand = false,
            OnHandLayoutRequested = null,
            OnPhysicalStackSync = skipId => SyncPhysicalStacks(localPlayer, skipId)
        };
    }

    private void HandleLifeChange(Player player, int currentLife)
    {
        if (player.Type == UserType.Human && myLifeText != null)
            myLifeText.text = $"♥ : {currentLife}";
    }

    private void HandleResourceChange(Player player, int currentResource)
    {
        if (player.Type == UserType.Human && myResourceText != null)
            myResourceText.text = $"♣ : {currentResource}";

        if (player.Type == UserType.Human)
            RefreshDeckCounts(player);
    }

    private void HandleCardDraw(Card card, Player owner, ZoneType sourceZone)
    {
        if (owner == null || owner.Type != UserType.Human)
            return;

        RefreshDeckCounts(owner);
    }

    private void HandleCardMove(Card card, Player owner, ZoneType fromZone, Player target, ZoneType toZone)
    {
        if (owner.Type != UserType.Human)
            return;

        localPlayer = owner;

        if (toZone == ZoneType.StackZone)
        {
            CardBoardRegistry.TryGet(card, out GameObject adopt);
            Vector2 fromAnchored = Vector2.zero;
            bool hasFrom = false;
            if (adopt != null)
            {
                CardMoveTween.Complete(adopt.transform);
                CardBoardRegistry.ResetVisualState(adopt);
                if (myStackZoneRoot != null && adopt.transform.parent != myStackZoneRoot)
                    adopt.transform.SetParent(myStackZoneRoot, true);

                RectTransform adoptRect = adopt.GetComponent<RectTransform>();
                if (adoptRect != null)
                {
                    fromAnchored = adoptRect.anchoredPosition;
                    hasFrom = true;
                }
            }

            SyncMyStack(owner, adopt);
            CardBoardRegistry.HideStrayCards(mySetZoneTransform, null, GetHumanHiddenPool());
            if (adopt != null && hasFrom)
                CardMoveTween.PlayFromCaptured(adopt.transform, fromAnchored);

            ClearPendingIfMatch(card);
            RefreshDeckCounts(owner);
            return;
        }

        if (toZone == ZoneType.Graveyard && fromZone == ZoneType.StackZone)
        {
            CardBoardRegistry.TryGet(card, out GameObject moving);
            Vector3 fromWorld = Vector3.zero;
            bool hasFrom = false;
            if (moving != null)
            {
                CardMoveTween.Complete(moving.transform);
                fromWorld = moving.transform.position;
                hasFrom = moving.activeInHierarchy;
            }

            StackZoneRowUI.MoveCardToGraveyard(
                myStackZoneRoot,
                myGraveyardTransform,
                card,
                _myStackCardObjects,
                null);

            SyncMyStack(owner, null);
            SyncPhysicalStacks(owner, card.InstanceId);
            if (moving != null)
            {
                RectTransform movingRect = moving.GetComponent<RectTransform>();
                DeckGraveyardStackUI.ApplyStackedCardFrame(movingRect, myStackCardWidth);
                Vector2 fromAnchored = hasFrom
                    ? CardMoveTween.WorldToAnchored(movingRect, fromWorld)
                    : CardMoveTween.WorldToAnchored(movingRect, myStackZoneRoot != null ? myStackZoneRoot.position : fromWorld);
                Vector2 toAnchored = DeckGraveyardStackUI.GetStackedAnchoredPosition(
                    owner.Graveyard,
                    card,
                    topIsIndexZero: false);
                CardMoveTween.Play(moving.transform, fromAnchored, toAnchored);
            }

            RefreshDeckCounts(owner);
            return;
        }

        CardBoardRegistry.ApplyZoneMove(card, owner, fromZone, toZone, CreateHumanZoneContext());
        if (fromZone == ZoneType.SetZone)
            ClearPendingIfMatch(card);

        RefreshDeckCounts(owner);
    }

    private void HandleRequireSet(Player player, GameContext context, Action<Card> callback)
    {
        if (player.Type != UserType.Human) return;

        // 새 세트 페이즈 시작 — 지난 턴의 잔상/흐림이 남아 있으면 정리한다
        ClearPendingSelection();

        pendingSetCallback = callback;
        if (readyButtonObj != null) readyButtonObj.SetActive(false);
    }

    /// <summary>
    /// [공개] 또는 [폐기] 버튼을 눌렀을 때. 아직 확정이 아니다.
    /// 카드는 손패에 그대로 두고 흐리게만 만들고, 세트존에는 흐린 뒷면 잔상을 띄운다.
    /// 다른 카드를 다시 고르면 이전 카드의 흐림이 풀리고 잔상만 갱신된다.
    /// 실제 세트는 [레디] → 엔진 Player.SetCard 시점에 일어난다.
    /// </summary>
    public void ConfirmSetCard(string instanceId, GameObject cardObj, bool isReveal)
    {
        if (pendingSetCallback == null || localPlayer == null)
            return;

        Card selected = ResolveHandCard(instanceId, cardObj);
        if (selected == null)
            return;

        // 이전에 골랐던 카드의 흐림을 먼저 되돌린다 (잔상은 지우지 않고 갱신만 한다)
        RestoreDimmedHandCard();

        pendingCardToSet = selected;
        pendingCardObj = cardObj;
        pendingIsReveal = isReveal;

        DimHandCard(cardObj);
        ShowSetGhost(isReveal);

        if (readyButtonObj != null) readyButtonObj.SetActive(true);
    }

    public void OnClickReadyButton()
    {
        if (pendingSetCallback != null && pendingCardToSet != null)
        {
            if (readyButtonObj != null) readyButtonObj.SetActive(false);

            // 잔상은 여기서 지우지 않는다.
            // 엔진의 실제 세트는 양측이 모두 준비된 뒤에 일어나므로, 지금 지우면 그 사이 세트존이 비어 보인다.
            // 대신 "확정됨"을 알리도록 잔상을 조금 더 진하게 만들고, 실제 카드가 도착할 때(OnCardSet) 지운다.
            MarkSetGhostConfirmed();

            var callback = pendingSetCallback;
            pendingSetCallback = null;
            callback.Invoke(pendingCardToSet);
        }
    }

    /// <summary>엔진이 실제로 카드를 세트존에 올린 시점. 잔상을 지우고 진짜 카드에 자리를 넘긴다.</summary>
    private void HandleCardSet(Card card, Player owner)
    {
        if (owner == null || owner.Type != UserType.Human) return;

        DestroySetGhost();

        // 손패 카드의 흐림은 CardBoardRegistry.ApplyZoneMove → ResetVisualState가 알파를 1로 되돌려 주지만,
        // 참조만 남아 다음 턴에 엉뚱한 카드를 되돌리지 않도록 여기서 놓아 준다.
        _dimmedHandCard = null;
        pendingCardToSet = null;
        pendingCardObj = null;
    }

    // ─── 흐림 처리 ──────────────────────────────────────────────────────

    private void DimHandCard(GameObject cardObj)
    {
        if (cardObj == null) return;

        CanvasGroup group = cardObj.GetComponent<CanvasGroup>();
        if (group == null) group = cardObj.AddComponent<CanvasGroup>();

        group.alpha = pendingHandCardAlpha;
        _dimmedHandCard = group;
    }

    private void RestoreDimmedHandCard()
    {
        if (_dimmedHandCard != null)
            _dimmedHandCard.alpha = 1f;

        _dimmedHandCard = null;
    }

    // ─── 세트존 잔상 ────────────────────────────────────────────────────

    private void ShowSetGhost(bool isReveal)
    {
        if (mySetZoneTransform == null) return;

        EnsureSetGhost();
        if (_setGhost == null) return;

        _setGhost.SetActive(true);
        if (_setGhostGroup != null) _setGhostGroup.alpha = setGhostAlpha;

        Color border = isReveal ? revealBorderColor : abandonBorderColor;
        if (_setGhostBorder != null) _setGhostBorder.color = border;

        if (_setGhostLabel != null)
        {
            _setGhostLabel.text = isReveal ? "공개" : "폐기";
            _setGhostLabel.color = Color.white;
        }

        var labelBg = _setGhostLabel != null
            ? _setGhostLabel.transform.parent.GetComponent<Image>()
            : null;
        if (labelBg != null) labelBg.color = border;
    }

    private void MarkSetGhostConfirmed()
    {
        if (_setGhostGroup != null)
            _setGhostGroup.alpha = setGhostConfirmedAlpha;
    }

    private void DestroySetGhost()
    {
        if (_setGhost != null) Destroy(_setGhost);

        _setGhost = null;
        _setGhostGroup = null;
        _setGhostBorder = null;
        _setGhostBack = null;
        _setGhostLabel = null;
    }

    /// <summary>
    /// 잔상 오브젝트를 만든다.
    /// ※ CardUI를 붙이지 않는다 — CardBoardRegistry.CountCardObjects가 CardUI로 카드를 세기 때문에,
    ///   CardUI를 붙이면 CanPlaceCardInSetZone()이 "세트존이 찼다"고 판단해 카드 교체가 막힌다.
    /// </summary>
    private void EnsureSetGhost()
    {
        if (_setGhost != null) return;

        _setGhost = new GameObject("SetZoneGhost", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        var rect = (RectTransform)_setGhost.transform;
        rect.SetParent(mySetZoneTransform, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = setGhostSize;
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;

        _setGhostGroup = _setGhost.GetComponent<CanvasGroup>();
        _setGhostGroup.blocksRaycasts = false;
        _setGhostGroup.interactable = false;

        // 바깥 테두리 (공개=파랑 / 폐기=빨강)
        _setGhostBorder = _setGhost.GetComponent<Image>();
        _setGhostBorder.raycastTarget = false;

        // 안쪽 카드 뒷면
        var backGo = new GameObject("Back", typeof(RectTransform), typeof(Image));
        var backRect = (RectTransform)backGo.transform;
        backRect.SetParent(rect, false);
        backRect.anchorMin = Vector2.zero;
        backRect.anchorMax = Vector2.one;
        backRect.offsetMin = Vector2.one * setGhostBorderThickness;
        backRect.offsetMax = Vector2.one * -setGhostBorderThickness;

        _setGhostBack = backGo.GetComponent<Image>();
        _setGhostBack.raycastTarget = false;
        if (!CardImageLoader.ApplyToImage(_setGhostBack, GameRules.DefaultCardBackPath))
            _setGhostBack.color = new Color(0.16f, 0.16f, 0.2f, 1f); // 뒷면 이미지 로드 실패 시 단색

        // 우측 상단 라벨 ("공개" / "폐기")
        var labelBgGo = new GameObject("LabelBackground", typeof(RectTransform), typeof(Image));
        var labelBgRect = (RectTransform)labelBgGo.transform;
        labelBgRect.SetParent(rect, false);
        labelBgRect.anchorMin = new Vector2(1f, 1f);
        labelBgRect.anchorMax = new Vector2(1f, 1f);
        labelBgRect.pivot = new Vector2(1f, 1f);
        labelBgRect.sizeDelta = new Vector2(66f, 32f);
        labelBgRect.anchoredPosition = new Vector2(-6f, -6f);
        labelBgGo.GetComponent<Image>().raycastTarget = false;

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(labelBgRect, false);

        _setGhostLabel = labelGo.AddComponent<TextMeshProUGUI>();
        _setGhostLabel.font = UiFontResolver.Resolve(); // 기본 폰트로 두면 한글이 ㅁ로 깨진다
        _setGhostLabel.fontSize = 20f;
        _setGhostLabel.alignment = TextAlignmentOptions.Center;
        _setGhostLabel.raycastTarget = false;

        var labelRect = _setGhostLabel.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
    }

    private void HandleRequireOpen(Player player, Card card, int cost, GameContext context, Action<OpenPhaseChoice> callback)
    {
        if (player.Type != UserType.Human) return;

        callback.Invoke(pendingIsReveal ? OpenPhaseChoice.Open : OpenPhaseChoice.Abandon);
    }

    public void CancelSet()
    {
        ClearPendingSelection();
        if (readyButtonObj != null) readyButtonObj.SetActive(false);
    }

    private Card ResolveHandCard(string instanceId, GameObject cardObj)
    {
        if (localPlayer?.Hand == null)
            return null;

        if (!string.IsNullOrEmpty(instanceId))
        {
            Card byInstance = localPlayer.Hand.Find(c => c != null && c.InstanceId == instanceId);
            if (byInstance != null)
                return byInstance;
        }

        CardUI ui = cardObj != null ? cardObj.GetComponent<CardUI>() : null;
        if (ui != null && !string.IsNullOrEmpty(ui.myInstanceId))
        {
            Card byUi = localPlayer.Hand.Find(c => c != null && c.InstanceId == ui.myInstanceId);
            if (byUi != null)
                return byUi;
        }

        return null;
    }

    private void ClearPendingIfMatch(Card card)
    {
        if (pendingCardToSet != null && IsSameEngineCard(pendingCardToSet, card))
            ClearPendingSelection();
    }

    private void ClearPendingSelection()
    {
        RestoreDimmedHandCard();
        DestroySetGhost();

        pendingCardToSet = null;
        pendingCardObj = null;
    }

    private void HandleCardStacked(Card card, Player player)
    {
        if (player.Type != UserType.Human)
            return;

        SyncMyStack(player, null);
    }

    private void SyncMyStack(Player owner, GameObject adoptCard)
    {
        if (myStackZoneRoot == null || owner == null)
            return;

        StackZoneRowUI.SyncFromEngineStack(new StackZoneSyncContext
        {
            barRoot = myStackZoneRoot,
            engineStack = owner.StackZone,
            cardPrefab = myCardPrefab,
            instanceMap = _myStackCardObjects,
            adoptCard = adoptCard,
            cardWidth = myStackCardWidth,
            applyCardSize = LayoutStackCard
        });
    }

    private static bool IsSameEngineCard(Card left, Card right)
    {
        if (left == null || right == null)
            return false;

        if (!string.IsNullOrEmpty(left.InstanceId) && !string.IsNullOrEmpty(right.InstanceId))
            return left.InstanceId == right.InstanceId;

        return false;
    }

    private void HandleCardStateChanged(Card card)
    {
        if (localPlayer == null || card == null || !card.IsFaceUp)
            return;

        if (localPlayer.Type != UserType.Human)
            return;

        if (!CardBoardRegistry.TryGet(card, out GameObject cardObj))
            cardObj = pendingCardObj;

        if (cardObj == null)
            return;

        CardUI ui = cardObj.GetComponent<CardUI>();
        if (ui != null)
            ui.SetFaceDown(false);
    }

    private void RefreshDeckCounts(Player owner)
    {
        if (owner == null)
            return;

        if (myDeckCountText != null)
            myDeckCountText.text = owner.Deck != null ? owner.Deck.Count.ToString() : "0";

        if (myResourceDeckCountText != null)
            myResourceDeckCountText.text = owner.ResourceDeck != null ? owner.ResourceDeck.Count.ToString() : "0";
    }

    private void HandleTurnEnd(string playerName)
    {
        SyncPhysicalStacks(localPlayer);
    }

    private void HandleEndPhase(string playerName, int turn)
    {
        SyncPhysicalStacks(localPlayer);
    }

    public void SyncPhysicalStacks(Player owner, string skipLayoutInstanceId = null)
    {
        if (owner == null || owner.Type != UserType.Human)
            return;

        DeckGraveyardStackUI.Sync(new DeckGraveyardSyncContext
        {
            ZoneRoot = myDeckTransform,
            EngineList = owner.Deck,
            HiddenPool = GetHumanHiddenPool(),
            CardWidth = myStackCardWidth,
            FaceDown = true,
            TopIsIndexZero = true,
            SkipLayoutInstanceId = skipLayoutInstanceId
        });

        DeckGraveyardStackUI.Sync(new DeckGraveyardSyncContext
        {
            ZoneRoot = myGraveyardTransform,
            EngineList = owner.Graveyard,
            HiddenPool = GetHumanHiddenPool(),
            CardWidth = myStackCardWidth,
            FaceDown = false,
            TopIsIndexZero = false,
            SkipLayoutInstanceId = skipLayoutInstanceId
        });
    }

    private void LayoutStackCard(RectTransform rect)
    {
        if (rect == null) return;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(myStackCardWidth, 280f);
        rect.localScale = Vector3.one;
    }
}
