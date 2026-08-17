using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

[DefaultExecutionOrder(-50)]
public class GameSceneBoardBinder : MonoBehaviour
{
    [SerializeField] private GameObject cardPrefab;

    private void Awake()
    {
        if (FindFirstObjectByType<LocalMatchStarter>() != null)
            return;

        if (cardPrefab == null)
            cardPrefab = Resources.Load<GameObject>("Build/CardSlotInGame");

        Transform[] hands = FindNamedSorted("MyHand");
        Transform[] sets = FindNamedSorted("set");
        Transform[] graves = FindNamedSorted("delete");
        Transform[] decks = FindNamedSorted("MyDeck");
        Transform[] enemyDecks = FindNamedSorted("EnemyDeck");
        Transform[] stacks = FindNamedSorted("stack1");
        Transform[] middles = FindNamedSorted("middle");
        Transform[] resources = FindNamedSorted("resource");
        Transform[] enemyResources = FindNamedSorted("EnemyResource");

        PlayerUIManager playerUi = FindFirstObjectByType<PlayerUIManager>();
        if (playerUi == null)
            playerUi = gameObject.AddComponent<PlayerUIManager>();

        playerUi.myHandTransform = Pick(hands, 0);
        playerUi.mySetZoneTransform = Pick(sets, 0);
        playerUi.myGraveyardTransform = Pick(graves, 0);
        playerUi.myDeckTransform = Pick(decks, 0);
        playerUi.myStackZoneRoot = Pick(stacks, 0);
        playerUi.myBattlefieldTransform = Pick(middles, 0);
        playerUi.myResourceZoneTransform = Pick(resources, 0);
        playerUi.myLifeText = FindTmp("TxtMyLife");
        playerUi.myResourceText = FindTmp("TxtMyResource");
        playerUi.myCardPrefab = cardPrefab;
        playerUi.readyButtonObj = GameObject.Find("ReadyButton");

        EnemyVisualTester enemyUi = FindFirstObjectByType<EnemyVisualTester>();
        if (enemyUi == null)
            enemyUi = gameObject.AddComponent<EnemyVisualTester>();

        enemyUi.enemyHandTransform = Pick(hands, 1) ?? Pick(hands, 0);
        enemyUi.enemySetZoneTransform = Pick(sets, 1) ?? Pick(sets, 0);
        enemyUi.enemyGraveyardTransform = Pick(graves, 1) ?? Pick(graves, 0);
        enemyUi.enemyDeckTransform = Pick(enemyDecks, 0) ?? Pick(decks, 1) ?? Pick(decks, 0);
        enemyUi.enemyStackZoneRoot = Pick(stacks, 1) ?? Pick(stacks, 0);
        enemyUi.enemyBattlefieldTransform = Pick(middles, 1) ?? Pick(middles, 0);
        enemyUi.enemyResourceZoneTransform = PickLast(enemyResources);
        enemyUi.enemyLifeText = FindTmp("TxtEnemyLife");
        enemyUi.enemyResourceText = FindTmp("TxtEnemyResource");
        enemyUi.cardFrontPrefab = cardPrefab;

        if (FindFirstObjectByType<CardBoardInvariantChecker>() == null)
            gameObject.AddComponent<CardBoardInvariantChecker>();
    }

    private static Transform[] FindNamedSorted(string name)
    {
        return Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(t => t != null && t.name == name && t.gameObject.scene.IsValid())
            .OrderBy(t => t.position.y)
            .ToArray();
    }

    private static Transform Pick(Transform[] list, int index)
    {
        if (list == null || list.Length == 0)
            return null;

        return index < list.Length ? list[index] : list[0];
    }

    private static Transform PickLast(Transform[] list)
    {
        if (list == null || list.Length == 0)
            return null;

        return list[list.Length - 1];
    }

    private static TextMeshProUGUI FindTmp(string name)
    {
        GameObject go = GameObject.Find(name);
        return go != null ? go.GetComponent<TextMeshProUGUI>() : null;
    }
}
