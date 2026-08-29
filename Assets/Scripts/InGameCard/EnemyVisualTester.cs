using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnemyVisualTester : MonoBehaviour
{
    [Header("Opponent UI Zones (typically P2 / top)")]
    public Transform enemyHandTransform;
    public Transform enemySetZoneTransform;
    public Transform enemyGraveyardTransform;
    public Transform enemyDeckTransform;
    public Transform enemyStackZoneRoot;
    public Transform enemyBattlefieldTransform;
    public Transform enemyResourceZoneTransform;

    [Header("Opponent Status UI")]
    public TextMeshProUGUI enemyLifeText;
    public TextMeshProUGUI enemyResourceText;

    [Header("BotVsBot: P1 uses PlayerUI bottom zones when both sides are bots")]
    [SerializeField] private bool usePlayerUiZonesForP1Bot = true;

    [Header("BotVsBot Spectator")]
    [SerializeField] private bool revealAllHandsInBotVsBot = true;

    [Header("Prefabs")]
    public GameObject cardFrontPrefab;

    [Header("Deck Counts")]
    public TextMeshProUGUI enemyDeckCountText;
    public TextMeshProUGUI enemyResourceDeckCountText;

    public float enemyCardWidth = 200f;

    private Player _p1;
    private Player _p2;
    private readonly Dictionary<Player, GameObject> _currentSetCards = new Dictionary<Player, GameObject>();
    private readonly Dictionary<Player, Dictionary<string, GameObject>> _botStackCards =
        new Dictionary<Player, Dictionary<string, GameObject>>();
    private readonly Dictionary<Player, Transform> _botHiddenPools = new Dictionary<Player, Transform>();

    private void OnEnable()
    {
        EventManager.OnGameStart += HandleGameStart;
        EventManager.OnGameSet += HandleGameEnd;
        EventManager.OnCardMove += HandleEnemyCardMove;
        EventManager.OnCardDraw += HandleEnemyCardDraw;
        EventManager.OnPlayCard += HandleEnemyPlayCard;
        EventManager.OnCardStateChanged += HandleCardStateChanged;
        EventManager.OnLifeChange += HandleEnemyLifeChange;
        EventManager.OnResourceChange += HandleEnemyResourceChange;
        EventManager.OnTurnEnd += HandleTurnEnd;
        EventManager.OnEndPhase += HandleEndPhase;
    }

    private void OnDisable()
    {
        EventManager.OnGameStart -= HandleGameStart;
        EventManager.OnGameSet -= HandleGameEnd;
        EventManager.OnCardMove -= HandleEnemyCardMove;
        EventManager.OnCardDraw -= HandleEnemyCardDraw;
        EventManager.OnPlayCard -= HandleEnemyPlayCard;
        EventManager.OnCardStateChanged -= HandleCardStateChanged;
        EventManager.OnLifeChange -= HandleEnemyLifeChange;
        EventManager.OnResourceChange -= HandleEnemyResourceChange;
        EventManager.OnTurnEnd -= HandleTurnEnd;
        EventManager.OnEndPhase -= HandleEndPhase;
    }

    private void HandleGameStart(Player p1, Player p2)
    {
        _p1 = p1;
        _p2 = p2;
        _currentSetCards.Clear();
        _botStackCards.Clear();
        _botHiddenPools.Clear();

        CardBoardRegistry.ClearPool();

        if (PlayerUIManager.Instance != null)
            PlayerUIManager.Instance.InitializeHumanCardPool(PlayerUIManager.ResolveHumanPlayer(p1, p2));

        enemyDeckTransform = DeckGraveyardStackUI.EnsureDeckCardAnchor(enemyDeckTransform);

        TryBuildBotPool(p1);
        TryBuildBotPool(p2);
        SyncPhysicalStacks(p1);
        SyncPhysicalStacks(p2);
        RefreshBotDeckCounts(p1);
        RefreshBotDeckCounts(p2);
        RefreshBotStatus(p1);
        RefreshBotStatus(p2);

        Debug.Log($"[EnemyVisual] 카드 풀 생성: {CardBoardRegistry.Count}장");
    }

    private void RefreshBotStatus(Player player)
    {
        if (!LocalPlayerContext.IsOpponent(player))
            return;

        HandleEnemyLifeChange(player, player.LifeTokens);
        HandleEnemyResourceChange(player, player.GetResourceCount());
    }

    private void HandleGameEnd(Player winner)
    {
        CardBoardRegistry.ClearPool();
        _currentSetCards.Clear();
        _botStackCards.Clear();
        _botHiddenPools.Clear();
    }

    public void ConfigureBotVsBotSpectator(bool revealHands)
    {
        revealAllHandsInBotVsBot = revealHands;
    }

    private bool IsBotVsBot =>
        _p1 != null && _p2 != null &&
        _p1.Type == UserType.Bot && _p2.Type == UserType.Bot;

    private void TryBuildBotPool(Player player)
    {
        if (!LocalPlayerContext.IsOpponent(player) || cardFrontPrefab == null)
            return;

        var poolCards = new List<Card>();
        poolCards.AddRange(player.Deck);
        poolCards.AddRange(player.ResourceDeck);

        CardBoardRegistry.BuildPool(player, poolCards, cardFrontPrefab, GetBotHiddenPool(player));
    }

    private Transform GetBotHiddenPool(Player player)
    {
        if (player == null)
            return null;

        if (!_botHiddenPools.TryGetValue(player, out Transform pool) || pool == null)
        {
            Transform hintHand = enemyHandTransform;
            Transform hintSet = enemySetZoneTransform;
            Transform hintDeck = enemyDeckTransform;
            Transform hintGrave = enemyGraveyardTransform;
            if (TryGetBotBoard(player, out BotBoardView board))
            {
                hintHand = board.Hand;
                hintSet = board.SetZone;
                hintDeck = board.Deck;
                hintGrave = board.Graveyard;
            }

            pool = CardBoardRegistry.CreateHiddenPool(
                $"BotHiddenCardPool_{player.Name}",
                transform,
                hintHand,
                hintSet,
                hintDeck,
                hintGrave);
            _botHiddenPools[player] = pool;
        }

        return pool;
    }

    private struct BotBoardView
    {
        public Transform Hand;
        public Transform SetZone;
        public Transform Graveyard;
        public Transform Deck;
        public Transform StackBar;
        public Transform Battlefield;
        public Transform ResourceZone;
        public TextMeshProUGUI LifeText;
        public TextMeshProUGUI ResourceText;
    }

    private bool TryGetBotBoard(Player player, out BotBoardView board)
    {
        board = default;

        if (!LocalPlayerContext.IsOpponent(player))
            return false;

        if (_p1 != null && player == _p1 && _p2 != null && _p1.Type == UserType.Bot && _p2.Type == UserType.Bot
            && usePlayerUiZonesForP1Bot && PlayerUIManager.Instance != null)
        {
            PlayerUIManager ui = PlayerUIManager.Instance;
            board = new BotBoardView
            {
                Hand = ui.myHandTransform,
                SetZone = ui.mySetZoneTransform,
                Graveyard = ui.myGraveyardTransform,
                Deck = ui.myDeckTransform,
                StackBar = ui.myStackZoneRoot,
                Battlefield = ui.myBattlefieldTransform,
                ResourceZone = ui.myResourceZoneTransform,
                LifeText = ui.myLifeText,
                ResourceText = ui.myResourceText
            };
            return board.Hand != null;
        }

        board = new BotBoardView
        {
            Hand = enemyHandTransform,
            SetZone = enemySetZoneTransform,
            Graveyard = enemyGraveyardTransform,
            Deck = enemyDeckTransform,
            StackBar = enemyStackZoneRoot,
            Battlefield = enemyBattlefieldTransform,
            ResourceZone = enemyResourceZoneTransform,
            LifeText = enemyLifeText,
            ResourceText = enemyResourceText
        };
        return board.Hand != null;
    }

    private ZoneMoveContext CreateBotZoneContext(BotBoardView board, Player owner)
    {
        return new ZoneMoveContext
        {
            Hand = board.Hand,
            SetZone = board.SetZone,
            Graveyard = board.Graveyard,
            Deck = board.Deck,
            StackBar = board.StackBar,
            Battlefield = board.Battlefield,
            HiddenPool = GetBotHiddenPool(owner),
            ResourceZone = board.ResourceZone,
            CardWidth = enemyCardWidth,
            UseCardBackInHand = !(revealAllHandsInBotVsBot && IsBotVsBot),
            OnHandLayoutRequested = null,
            OnPhysicalStackSync = skipId => SyncPhysicalStacks(owner, skipId)
        };
    }

    private void HandleEnemyLifeChange(Player player, int currentLife)
    {
        if (!TryGetBotBoard(player, out BotBoardView board) || board.LifeText == null)
            return;

        board.LifeText.text = $"♥ : {currentLife}";
    }

    private void HandleEnemyResourceChange(Player player, int currentResource)
    {
        if (!TryGetBotBoard(player, out BotBoardView board) || board.ResourceText == null)
            return;

        board.ResourceText.text = $"♣ : {currentResource}";
        RefreshBotDeckCounts(player);
    }

    private void HandleEnemyCardMove(Card card, Player owner, ZoneType fromZone, Player target, ZoneType toZone)
    {
        if (!TryGetBotBoard(owner, out BotBoardView board))
            return;

        if (toZone == ZoneType.StackZone)
        {
            CardBoardRegistry.TryGet(card, out GameObject adoptCard);
            if (adoptCard == null)
                _currentSetCards.TryGetValue(owner, out adoptCard);

            Vector2 fromAnchored = Vector2.zero;
            bool hasFrom = false;
            if (adoptCard != null)
            {
                CardMoveTween.Complete(adoptCard.transform);
                CardBoardRegistry.ResetVisualState(adoptCard);

                // ★ 숨김 풀에서 돌아온 카드는 꺼져 있다. 내 보드 쪽과 같은 이유다.
                adoptCard.SetActive(true);

                if (board.StackBar != null && adoptCard.transform.parent != board.StackBar)
                    adoptCard.transform.SetParent(board.StackBar, true);

                RectTransform adoptRect = adoptCard.GetComponent<RectTransform>();
                if (adoptRect != null)
                {
                    fromAnchored = adoptRect.anchoredPosition;
                    hasFrom = true;
                }
            }

            SyncBotStack(owner, board, adoptCard);
            CardBoardRegistry.HideStrayCards(board.SetZone, null, GetBotHiddenPool(owner));
            if (adoptCard != null && hasFrom)
                CardMoveTween.PlayFromCaptured(adoptCard.transform, fromAnchored);

            _currentSetCards.Remove(owner);
            RefreshBotDeckCounts(owner);
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
                board.StackBar,
                board.Graveyard,
                card,
                GetBotStackMap(owner),
                null);

            SyncBotStack(owner, board);
            SyncPhysicalStacks(owner, card.InstanceId);
            if (moving != null)
            {
                RectTransform movingRect = moving.GetComponent<RectTransform>();
                DeckGraveyardStackUI.ApplyStackedCardFrame(movingRect, enemyCardWidth);
                Vector2 fromAnchored = hasFrom
                    ? CardMoveTween.WorldToAnchored(movingRect, fromWorld)
                    : CardMoveTween.WorldToAnchored(
                        movingRect,
                        board.StackBar != null ? board.StackBar.position : fromWorld);
                Vector2 toAnchored = DeckGraveyardStackUI.GetStackedAnchoredPosition(
                    owner.Graveyard,
                    card,
                    topIsIndexZero: false);
                CardMoveTween.Play(moving.transform, fromAnchored, toAnchored);
            }

            RefreshBotDeckCounts(owner);
            return;
        }

        ZoneMoveContext ctx = CreateBotZoneContext(board, owner);
        if (CardBoardRegistry.ApplyZoneMove(card, owner, fromZone, toZone, ctx))
        {
            if (toZone == ZoneType.SetZone && CardBoardRegistry.TryGet(card, out GameObject setGo))
                _currentSetCards[owner] = setGo;
            else if (fromZone == ZoneType.SetZone)
                _currentSetCards.Remove(owner);
        }

        RefreshBotDeckCounts(owner);
    }

    private void HandleEnemyCardDraw(Card card, Player owner, ZoneType sourceZone)
    {
        RefreshBotDeckCounts(owner);
    }

    private Dictionary<string, GameObject> GetBotStackMap(Player owner)
    {
        if (!_botStackCards.TryGetValue(owner, out Dictionary<string, GameObject> map))
        {
            map = new Dictionary<string, GameObject>();
            _botStackCards[owner] = map;
        }

        return map;
    }

    private void SyncBotStack(Player owner, BotBoardView board, GameObject adoptCard = null)
    {
        if (board.StackBar == null || owner == null)
            return;

        StackZoneRowUI.SyncFromEngineStack(new StackZoneSyncContext
        {
            barRoot = board.StackBar,
            engineStack = owner.StackZone,
            cardPrefab = cardFrontPrefab,
            instanceMap = GetBotStackMap(owner),
            adoptCard = adoptCard,
            cardWidth = enemyCardWidth,
            applyCardSize = rect => ForceCenterAndSize(rect, true)
        });
    }

    private static void ForceCenterAndSize(RectTransform rect, bool applySize = false)
    {
        if (rect == null) return;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;

        if (applySize) rect.sizeDelta = new Vector2(200f, 280f);
    }

    private void HandleCardStateChanged(Card card)
    {
        if (card == null || !card.IsFaceUp)
            return;

        TryRevealSetZoneCard(_p1, card);
        TryRevealSetZoneCard(_p2, card);
    }

    private void TryRevealSetZoneCard(Player player, Card card)
    {
        if (!LocalPlayerContext.IsOpponent(player) || player.SetZoneCard == null)
            return;

        if (!IsSameEngineCard(player.SetZoneCard, card))
            return;

        if (!TryGetBotBoard(player, out BotBoardView board) || board.SetZone == null)
            return;

        GameObject cardObj = null;
        if (_currentSetCards.TryGetValue(player, out GameObject setCardObj) && setCardObj != null)
            cardObj = setCardObj;
        else
            CardBoardRegistry.TryGet(card, out cardObj);

        if (cardObj == null)
            return;

        if (board.SetZone != null && cardObj.transform.parent != board.SetZone)
            return;

        RevealCardObject(cardObj, card);
    }

    private static void RevealCardObject(GameObject cardObj, Card card)
    {
        CardUI ui = cardObj.GetComponent<CardUI>();
        if (ui == null)
            return;

        ui.BindEngineCard(card);
        ui.SetFaceDown(false);
    }

    private static bool IsSameEngineCard(Card left, Card right)
    {
        if (left == null || right == null)
            return false;

        if (!string.IsNullOrEmpty(left.InstanceId) && !string.IsNullOrEmpty(right.InstanceId))
            return left.InstanceId == right.InstanceId;

        return left.Id == right.Id;
    }

    private void HandleEnemyPlayCard(Player player, Card card)
    {
        if (!TryGetBotBoard(player, out BotBoardView board) || card == null)
            return;

        if (!CardBoardRegistry.TryGet(card, out GameObject cardObj))
            return;

        if (board.SetZone != null && cardObj.transform.parent != board.SetZone)
            return;

        RevealCardObject(cardObj, card);
        CardInteraction interaction = cardObj.GetComponent<CardInteraction>();
        if (interaction != null)
            interaction.enabled = false;
    }

    private void RefreshBotDeckCounts(Player owner)
    {
        if (!TryGetBotBoard(owner, out _))
            return;

        if (enemyDeckCountText != null)
            enemyDeckCountText.text = owner.Deck != null ? owner.Deck.Count.ToString() : "0";

        if (enemyResourceDeckCountText != null)
            enemyResourceDeckCountText.text = owner.ResourceDeck != null ? owner.ResourceDeck.Count.ToString() : "0";
    }

    private void HandleTurnEnd(string playerName)
    {
        SyncPhysicalStacks(_p1);
        SyncPhysicalStacks(_p2);
    }

    private void HandleEndPhase(string playerName, int turn)
    {
        SyncPhysicalStacks(_p1);
        SyncPhysicalStacks(_p2);
    }

    public Transform ResolveSetRoot(Player player)
    {
        if (TryGetBotBoard(player, out BotBoardView board))
            return board.SetZone;

        return null;
    }

    public Transform ResolveDeckRoot(Player player)
    {
        if (TryGetBotBoard(player, out BotBoardView board))
            return board.Deck;

        return null;
    }

    public Transform ResolveGraveRoot(Player player)
    {
        if (TryGetBotBoard(player, out BotBoardView board))
            return board.Graveyard;

        return null;
    }

    public Transform ResolveHiddenPool(Player player)
    {
        return GetBotHiddenPool(player);
    }

    public void SyncPhysicalStacks(Player owner, string skipLayoutInstanceId = null)
    {
        if (!TryGetBotBoard(owner, out BotBoardView board))
            return;

        Transform hidden = GetBotHiddenPool(owner);
        DeckGraveyardStackUI.Sync(new DeckGraveyardSyncContext
        {
            ZoneRoot = board.Deck,
            EngineList = owner.Deck,
            HiddenPool = hidden,
            CardWidth = enemyCardWidth,
            FaceDown = true,
            TopIsIndexZero = true,
            SkipLayoutInstanceId = skipLayoutInstanceId
        });

        DeckGraveyardStackUI.Sync(new DeckGraveyardSyncContext
        {
            ZoneRoot = board.Graveyard,
            EngineList = owner.Graveyard,
            HiddenPool = hidden,
            CardWidth = enemyCardWidth,
            FaceDown = false,
            TopIsIndexZero = false,
            SkipLayoutInstanceId = skipLayoutInstanceId
        });
    }
}
