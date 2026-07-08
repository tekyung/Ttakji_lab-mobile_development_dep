using System.Collections;
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
    public Transform enemyStackZoneTransform;
    public Transform enemyBattlefieldTransform;

    [Header("Opponent Status UI")]
    public TextMeshProUGUI enemyLifeText;
    public TextMeshProUGUI enemyResourceText;

    [Header("BotVsBot: P1 uses PlayerUI bottom zones when both sides are bots")]
    [SerializeField] private bool usePlayerUiZonesForP1Bot = true;

    [Header("Prefabs")]
    public GameObject cardBackPrefab;
    public GameObject cardFrontPrefab;

    public float enemyCardWidth = 200f;

    private Player _p1;
    private Player _p2;
    private readonly Dictionary<Player, GameObject> _currentSetCards = new Dictionary<Player, GameObject>();

    private void OnEnable()
    {
        EventManager.OnGameStart += HandleGameStart;
        EventManager.OnCardMove += HandleEnemyCardMove;
        EventManager.OnPlayCard += HandleEnemyPlayCard;
        EventManager.OnLifeChange += HandleEnemyLifeChange;
        EventManager.OnResourceChange += HandleEnemyResourceChange;
    }

    private void OnDisable()
    {
        EventManager.OnGameStart -= HandleGameStart;
        EventManager.OnCardMove -= HandleEnemyCardMove;
        EventManager.OnPlayCard -= HandleEnemyPlayCard;
        EventManager.OnLifeChange -= HandleEnemyLifeChange;
        EventManager.OnResourceChange -= HandleEnemyResourceChange;
    }

    private void HandleGameStart(Player p1, Player p2)
    {
        _p1 = p1;
        _p2 = p2;
        _currentSetCards.Clear();
    }

    private struct BotBoardView
    {
        public Transform Hand;
        public Transform SetZone;
        public Transform Graveyard;
        public Transform StackZone;
        public Transform Battlefield;
        public TextMeshProUGUI LifeText;
        public TextMeshProUGUI ResourceText;
    }

    private bool TryGetBotBoard(Player player, out BotBoardView board)
    {
        board = default;

        if (player == null || player.Type != UserType.Bot)
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
                StackZone = ui.myStackZoneTransform,
                Battlefield = ui.myBattlefieldTransform,
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
            StackZone = enemyStackZoneTransform,
            Battlefield = enemyBattlefieldTransform,
            LifeText = enemyLifeText,
            ResourceText = enemyResourceText
        };
        return board.Hand != null;
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

    private void HandleEnemyCardMove(Card card, Player owner, ZoneType fromZone, Player target, ZoneType toZone)
    {
        if (!TryGetBotBoard(owner, out BotBoardView board))
            return;

        if (toZone == ZoneType.Hand)
        {
            GameObject newCard = Instantiate(cardBackPrefab, board.Hand);
            newCard.name = $"{owner.Name} Card ({card.Name})";
            CardImageLoader.ApplyDefaultCardBack(newCard);
            ForceCenterAndSize(newCard.GetComponent<RectTransform>(), true);

            Debug.Log($"🤖 [{owner.Name}] 드로우 UI 반영 — 엔진 패: {owner.Hand.Count}장, UI 자식: {board.Hand.childCount}장");
            StartCoroutine(UpdateHandSpacingRoutine(board.Hand));
            return;
        }

        if (toZone == ZoneType.SetZone && fromZone == ZoneType.Hand)
        {
            if (board.Hand.childCount > 0)
            {
                Transform cardToMove = board.Hand.GetChild(0);
                cardToMove.SetParent(board.SetZone);
                ForceCenterAndSize(cardToMove.GetComponent<RectTransform>());
            }

            return;
        }

        if (toZone == ZoneType.Graveyard || toZone == ZoneType.StackZone || toZone == ZoneType.BattlefieldZone)
        {
            GameObject cardObjToMove = null;

            if (fromZone == ZoneType.SetZone && _currentSetCards.TryGetValue(owner, out GameObject setCard))
            {
                cardObjToMove = setCard;
            }
            else if (fromZone == ZoneType.StackZone && board.StackZone != null)
            {
                Transform t = board.StackZone.Find(card.Id);
                if (t != null) cardObjToMove = t.gameObject;
            }
            else if (fromZone == ZoneType.BattlefieldZone && board.Battlefield != null)
            {
                Transform t = board.Battlefield.Find(card.Id);
                if (t != null) cardObjToMove = t.gameObject;
            }

            if (cardObjToMove != null)
            {
                if (toZone == ZoneType.Graveyard && board.Graveyard != null)
                {
                    cardObjToMove.transform.SetParent(board.Graveyard);
                    ForceCenterAndSize(cardObjToMove.GetComponent<RectTransform>());
                }
                else if (toZone == ZoneType.StackZone && board.StackZone != null)
                {
                    cardObjToMove.transform.SetParent(board.StackZone);
                    cardObjToMove.name = card.Id;
                    ForceCenterAndSize(cardObjToMove.GetComponent<RectTransform>());
                }
                else if (toZone == ZoneType.BattlefieldZone && board.Battlefield != null)
                {
                    cardObjToMove.transform.SetParent(board.Battlefield);
                    cardObjToMove.name = card.Id;
                    ForceCenterAndSize(cardObjToMove.GetComponent<RectTransform>());
                }
                else
                {
                    Destroy(cardObjToMove);
                }
            }

            if (fromZone == ZoneType.SetZone && board.SetZone != null)
            {
                foreach (Transform child in board.SetZone)
                    Destroy(child.gameObject);

                _currentSetCards.Remove(owner);
            }
        }
    }

    private void HandleEnemyPlayCard(Player player, Card card)
    {
        if (!TryGetBotBoard(player, out BotBoardView board))
            return;

        if (_currentSetCards.TryGetValue(player, out GameObject existingSetCard) && existingSetCard != null)
            Destroy(existingSetCard);

        if (board.SetZone != null)
        {
            foreach (Transform child in board.SetZone)
                Destroy(child.gameObject);
        }

        GameObject realCard = Instantiate(cardFrontPrefab, board.SetZone);
        ForceCenterAndSize(realCard.GetComponent<RectTransform>(), true);

        CardUI ui = realCard.GetComponent<CardUI>();
        if (ui != null) ui.SetupForBattle(card.Id);

        CardInteraction interaction = realCard.GetComponent<CardInteraction>();
        if (interaction != null) interaction.enabled = false;

        _currentSetCards[player] = realCard;
    }

    private IEnumerator UpdateHandSpacingRoutine(Transform handTransform)
    {
        yield return null;

        if (handTransform == null)
            yield break;

        HorizontalLayoutGroup layoutGroup = handTransform.GetComponent<HorizontalLayoutGroup>();
        if (layoutGroup == null)
            yield break;

        int cardCount = handTransform.childCount;
        if (cardCount < 2)
        {
            layoutGroup.spacing = 0;
            yield break;
        }

        RectTransform handRect = handTransform.GetComponent<RectTransform>();
        float panelWidth = handRect.rect.width;
        float totalCardWidth = enemyCardWidth * cardCount;

        if (totalCardWidth > panelWidth)
            layoutGroup.spacing = (panelWidth - totalCardWidth) / (cardCount - 1);
        else
            layoutGroup.spacing = -20f;
    }
}
