using System.Collections;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using UnityEngine;

public enum LocalMatchMode
{
    BotVsBot,
    HumanVsBot
}

[DefaultExecutionOrder(100)]
public class LocalMatchStarter : MonoBehaviour
{
    [SerializeField] private LocalMatchMode mode = LocalMatchMode.BotVsBot;
    [SerializeField] private bool autoStart = true;

    [Header("BotVsBot Spectator")]
    [SerializeField] private bool revealAllHandsInBotVsBot = true;
    [SerializeField] private EnemyVisualTester enemyVisualTester;

    private bool _started;

    private static readonly string[] P1UniqueCardIds =
    {
        "ELLI-02", "ELLI-03", "ELLI-04", "ELLI-05", "ELLI-06", "ELLI-07",
        "DAIN-02", "DAIN-03", "DAIN-07", "DAIN-09"
    };

    private static readonly string[] P2UniqueCardIds =
    {
        "VERO-02", "VERO-03", "VERO-05", "VERO-07", "VERO-11",
        "SONI-02", "SONI-03", "SONI-05", "SONI-06", "SONI-07"
    };

    /// <summary>
    /// 매칭을 거쳐 들어온 온라인 대전인지. `session_manage`가 매칭 성사 시 `GameData`를 채운다.
    /// </summary>
    public static bool IsOnlineSessionActive => !string.IsNullOrEmpty(GameData.SessionCode);

    private void Awake()
    {
        // ★ 온라인 대전으로 진입한 경우에는 로컬 봇전이 절대 끼어들면 안 된다.
        //   이 가드가 없으면 매칭이 성사돼 이 씬으로 넘어왔을 때 아래에서 session_game_manage를 꺼 버려
        //   온라인 세션이 죽고 봇전이 자동 시작된다. (온라인 준비는 OnlineMatchStarter가 맡는다)
        if (IsOnlineSessionActive)
        {
            enabled = false;
            Debug.Log($"[LocalMatchStarter] 온라인 세션({GameData.SessionCode}) 진행 중 — 로컬 봇전을 시작하지 않는다.");
            return;
        }

        session_game_manage sessionGameManage = FindFirstObjectByType<session_game_manage>();
        if (sessionGameManage != null)
        {
            sessionGameManage.enabled = false;
            Debug.Log("[LocalMatchStarter] session_game_manage disabled for local play.");
        }
    }

    private void Start()
    {
        if (IsOnlineSessionActive) return;

        if (autoStart)
            StartCoroutine(StartMatchAfterBattleManagerReady());
    }

    private IEnumerator StartMatchAfterBattleManagerReady()
    {
        yield return null;

        if (_started)
            yield break;

        if (BattleManager.Instance == null)
        {
            Debug.LogError("[LocalMatchStarter] BattleManager.Instance is null. Attach BattleManager to the scene.");
            yield break;
        }

        _started = true;

        // 로컬에서는 사람이 얼마든지 생각할 수 있어야 한다.
        // 정적 값이라 앞선 온라인 매치(또는 도메인 리로드를 끈 에디터의 이전 플레이)의 설정이 남을 수 있어 명시적으로 되돌린다.
        TCG_Project.Scripts.Systems.GameLogicHelpers.AllowUnlimitedHumanInput = true;

        PlayerSetupData p1Setup = BuildPlayerSetup(
            playerName: mode == LocalMatchMode.HumanVsBot ? "Player" : "Bot_Red",
            type: mode == LocalMatchMode.HumanVsBot ? UserType.Human : UserType.Bot,
            mainCharacterId: "ELLI-01",
            subCharacterId: "DAIN-01",
            uniqueCardIds: P1UniqueCardIds);

        PlayerSetupData p2Setup = BuildPlayerSetup(
            playerName: "Bot_Blue",
            type: UserType.Bot,
            mainCharacterId: "VERO-01",
            subCharacterId: "SONI-01",
            uniqueCardIds: P2UniqueCardIds);

        EnemyVisualTester visualTester = enemyVisualTester != null
            ? enemyVisualTester
            : FindFirstObjectByType<EnemyVisualTester>();

        if (visualTester != null)
        {
            visualTester.ConfigureBotVsBotSpectator(
                mode == LocalMatchMode.BotVsBot && revealAllHandsInBotVsBot);
        }

        Debug.Log($"[LocalMatchStarter] Starting local match ({mode}).");
        BattleManager.Instance.StartMatch(p1Setup, p2Setup);
    }

    private static PlayerSetupData BuildPlayerSetup(
        string playerName,
        UserType type,
        string mainCharacterId,
        string subCharacterId,
        string[] uniqueCardIds)
    {
        return new PlayerSetupData
        {
            PlayerName = playerName,
            Type = type,
            MainCharacterId = mainCharacterId,
            SubCharacterId = subCharacterId,
            DeckCardIds = BuildDeckList(uniqueCardIds)
        };
    }

    private static List<string> BuildDeckList(string[] uniqueIds)
    {
        var deck = new List<string>(uniqueIds.Length * 2);
        foreach (string id in uniqueIds)
        {
            deck.Add(id);
            deck.Add(id);
        }

        return deck;
    }
}
