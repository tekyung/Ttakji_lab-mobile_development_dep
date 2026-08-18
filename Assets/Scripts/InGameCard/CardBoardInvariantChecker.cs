using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;
using UnityEngine;

public class CardBoardInvariantChecker : MonoBehaviour
{
    [SerializeField] private bool repairOnMismatch = true;

    private Player _p1;
    private Player _p2;

    private void OnEnable()
    {
        EventManager.OnGameStart += HandleGameStart;
        EventManager.OnTurnEnd += HandleTurnEnd;
        EventManager.OnEndPhase += HandleEndPhase;
        EventManager.OnGameSet += HandleGameEnd;
    }

    private void OnDisable()
    {
        EventManager.OnGameStart -= HandleGameStart;
        EventManager.OnTurnEnd -= HandleTurnEnd;
        EventManager.OnEndPhase -= HandleEndPhase;
        EventManager.OnGameSet -= HandleGameEnd;
    }

    private void HandleGameStart(Player p1, Player p2)
    {
        _p1 = p1;
        _p2 = p2;
    }

    private void HandleGameEnd(Player winner)
    {
        _p1 = null;
        _p2 = null;
    }

    private void HandleTurnEnd(string playerName)
    {
        ValidatePlayers();
    }

    private void HandleEndPhase(string playerName, int turn)
    {
        ValidatePlayers();
    }

    private void ValidatePlayers()
    {
        ValidatePlayer(_p1);
        ValidatePlayer(_p2);
    }

    private void ValidatePlayer(Player player)
    {
        if (player == null)
            return;

        ValidateSetZone(player);
        ValidateListZone(player, player.Deck, ResolveDeckRoot(player), "Deck", true);
        ValidateListZone(player, player.Graveyard, ResolveGraveRoot(player), "Graveyard", false);
    }

    private void ValidateSetZone(Player player)
    {
        string engineId = player.SetZoneCard?.InstanceId;
        Transform setRoot = ResolveSetRoot(player);
        if (setRoot == null)
            return;

        var uiIds = new List<string>();
        var extras = new List<GameObject>();
        foreach (Transform child in setRoot)
        {
            CardUI ui = child.GetComponent<CardUI>();
            if (ui == null)
                continue;

            uiIds.Add(ui.myInstanceId);
            if (string.IsNullOrEmpty(engineId) || ui.myInstanceId != engineId)
                extras.Add(child.gameObject);
        }

        if (string.IsNullOrEmpty(engineId) && extras.Count == 0)
            return;

        if (!string.IsNullOrEmpty(engineId) && uiIds.Contains(engineId) && extras.Count == 0)
            return;

        Debug.LogWarning(
            $"[CardBoardInvariant] SetZone mismatch {player.Name}: engine={engineId ?? "null"} ui=[{string.Join(",", uiIds)}]");

        if (!repairOnMismatch)
            return;

        Transform repairPool = ResolveHiddenPool(player);
        foreach (GameObject extra in extras)
            CardBoardRegistry.PlaceHidden(extra, repairPool);
    }

    private void ValidateListZone(
        Player player,
        IReadOnlyList<Card> engineList,
        Transform zoneRoot,
        string zoneName,
        bool topIsIndexZero)
    {
        if (zoneRoot == null || engineList == null)
            return;

        var engineIds = new List<string>();
        foreach (Card card in engineList)
        {
            if (card != null && !string.IsNullOrEmpty(card.InstanceId))
                engineIds.Add(card.InstanceId);
        }

        var uiBySibling = new List<string>();
        var extras = new List<GameObject>();
        foreach (Transform child in zoneRoot)
        {
            CardUI ui = child.GetComponent<CardUI>();
            if (ui == null)
                continue;

            uiBySibling.Add(ui.myInstanceId);
            if (string.IsNullOrEmpty(ui.myInstanceId) || !engineIds.Contains(ui.myInstanceId))
                extras.Add(child.gameObject);
        }

        var visualEngineOrder = new List<string>(engineIds);
        if (topIsIndexZero)
            visualEngineOrder.Reverse();

        bool countMismatch = uiBySibling.Count != engineIds.Count;
        bool orderMismatch = false;
        if (!countMismatch)
        {
            for (int i = 0; i < visualEngineOrder.Count; i++)
            {
                if (uiBySibling[i] != visualEngineOrder[i])
                {
                    orderMismatch = true;
                    break;
                }
            }
        }

        if (!countMismatch && !orderMismatch && extras.Count == 0)
            return;

        Debug.LogWarning(
            $"[CardBoardInvariant] {zoneName} mismatch {player.Name}: engine=[{string.Join(",", engineIds)}] ui=[{string.Join(",", uiBySibling)}]");

        if (!repairOnMismatch)
            return;

        Transform repairPool = ResolveHiddenPool(player);
        foreach (GameObject extra in extras)
            CardBoardRegistry.PlaceHidden(extra, repairPool);

        if (LocalPlayerContext.IsMine(player) && PlayerUIManager.Instance != null)
            PlayerUIManager.Instance.SyncPhysicalStacks(player);
        else
            FindFirstObjectByType<EnemyVisualTester>()?.SyncPhysicalStacks(player);
    }

    private static Transform ResolveSetRoot(Player player)
    {
        if (player == null)
            return null;

        if (LocalPlayerContext.IsMine(player) && PlayerUIManager.Instance != null)
            return PlayerUIManager.Instance.mySetZoneTransform;

        EnemyVisualTester tester = FindFirstObjectByType<EnemyVisualTester>();
        return tester != null ? tester.ResolveSetRoot(player) : null;
    }

    private static Transform ResolveDeckRoot(Player player)
    {
        if (player == null)
            return null;

        if (LocalPlayerContext.IsMine(player) && PlayerUIManager.Instance != null)
            return PlayerUIManager.Instance.myDeckTransform;

        EnemyVisualTester tester = FindFirstObjectByType<EnemyVisualTester>();
        return tester != null ? tester.ResolveDeckRoot(player) : null;
    }

    private static Transform ResolveGraveRoot(Player player)
    {
        if (player == null)
            return null;

        if (LocalPlayerContext.IsMine(player) && PlayerUIManager.Instance != null)
            return PlayerUIManager.Instance.myGraveyardTransform;

        EnemyVisualTester tester = FindFirstObjectByType<EnemyVisualTester>();
        return tester != null ? tester.ResolveGraveRoot(player) : null;
    }

    private static Transform ResolveHiddenPool(Player player)
    {
        if (LocalPlayerContext.IsMine(player) && PlayerUIManager.Instance != null)
            return PlayerUIManager.Instance.HiddenPool;

        EnemyVisualTester tester = FindFirstObjectByType<EnemyVisualTester>();
        if (tester != null && player != null && player.Type == UserType.Bot)
            return tester.ResolveHiddenPool(player);

        return PlayerUIManager.Instance != null ? PlayerUIManager.Instance.HiddenPool : null;
    }
}
