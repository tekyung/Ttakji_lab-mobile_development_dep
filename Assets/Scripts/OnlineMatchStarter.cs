// OnlineMatchStarter.cs — 온라인(사람 vs 사람) 대전 준비 부트스트랩
//
// 로컬 봇전의 LocalMatchStarter에 대응하는 온라인 쪽 짝이다.
// 매칭(session_manage)이 GameData에 세션 정보를 채우고 넘어온 경우에만 동작한다.
//
// 하는 일은 "판을 깔아 주는 것"까지다. 실제 매치 진행은 씬의 session_game_manage가
// ServerGameManager / EventService를 불러 처리한다.
//
//   1) 씬에 ServerGameManager / EventService가 없으면 붙인다 (권한 서버 = HOST 전용)
//   2) EventService.networkService를 씬의 firebase_network로 이어 준다
//   3) 준비 상태를 한 줄 로그로 남긴다 (온라인이 안 뜰 때 여기부터 본다)
//
// ⚠️ 서버 스크립트(Assets/Scripts/Server Scripts/**)는 건드리지 않는다.
//    여기서는 컴포넌트를 붙이고 참조만 연결한다. 서버 로직 수정은 담당자 몫이다.
//
// ⚠️ 씬을 따로 파지 않은 이유:
//    보드 UI가 통째로 들어 있는 TestGameScene(11,000줄 남짓)을 복제하면 보드를 고칠 때마다
//    두 벌을 맞춰야 한다. 대신 LocalMatchStarter에 "온라인이면 비켜라" 가드를 넣어
//    같은 씬이 세션 유무에 따라 로컬 봇전 / 온라인 대전으로 갈리게 했다.
//    나중에 씬을 분리하더라도 이 컴포넌트는 그대로 쓸 수 있다.
using ServerScripts.EventScripts;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;
using TCG_Project.Scripts.Systems;
using UnityEngine;
using UnityEngine.SceneManagement;

public class OnlineMatchStarter : MonoBehaviour
{
    private static OnlineMatchStarter _instance;

    /// <summary>
    /// ⚠️ 임시 테스트 스위치 (2026-08-17). 온라인에서도 사람 입력을 무제한 대기로 둔다.
    /// 출시 전 <c>false</c>로 되돌릴 것 — 상대가 응답하지 않으면 양쪽이 멈춘다.
    /// </summary>
    private const bool UnlimitedInputForTesting = true;

    /// <summary>매칭을 거쳐 들어온 온라인 대전인지.</summary>
    public static bool IsOnlineSessionActive => !string.IsNullOrEmpty(GameData.SessionCode);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;

        var go = new GameObject("OnlineMatchStarter");
        _instance = go.AddComponent<OnlineMatchStarter>();
        DontDestroyOnLoad(go);
    }

    private void OnEnable()
    {
        // RuntimeInitializeOnLoadMethod는 게임 시작 때 한 번만 돈다.
        // 매칭 후 씬을 갈아탈 때도 준비해야 하므로 씬 로드를 계속 지켜본다.
        SceneManager.sceneLoaded += HandleSceneLoaded;
        PrepareOnlineRuntime();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => PrepareOnlineRuntime();

    /// <summary>
    /// 온라인 대전에 필요한 컴포넌트를 갖춘다.
    /// sceneLoaded는 씬 오브젝트의 Awake 뒤 · 첫 Start 앞에 오므로,
    /// session_game_manage.Start()가 도는 시점에는 준비가 끝나 있다.
    /// </summary>
    private void PrepareOnlineRuntime()
    {
        if (!IsOnlineSessionActive) return; // 로컬 봇전 — 할 일 없음

        // ★ 비활성 오브젝트까지 찾아야 한다.
        //   TestGameScene의 GameManage(= session_game_manage + firebase_network + session_ui)는
        //   씬에 **비활성으로 저장돼 있다**(로컬 봇전에서 파이어베이스를 건드리지 않으려는 조치로 보인다).
        //   기본 FindFirstObjectByType은 비활성 오브젝트를 건너뛰므로 그냥 찾으면 null이 나온다.
        var client = FindFirstObjectByType<session_game_manage>(FindObjectsInactive.Include);
        if (client == null) return;         // 온라인 클라이언트가 없는 씬(메인 메뉴 등)

        // 꺼져 있으면 Start()가 돌지 않는다 → 덱 업로드·이벤트 구독·호스트 매치 시작이 전부 일어나지 않는다.
        // 온라인으로 들어온 이상 반드시 켜 준다.
        if (!client.gameObject.activeSelf)
        {
            client.gameObject.SetActive(true);
            Debug.Log("[OnlineMatchStarter] 온라인 클라이언트(GameManage)가 꺼져 있어 활성화했다.");
        }
        client.enabled = true;

        // 사람 입력 제한 시간.
        //   원칙은 false(=제한 시간 적용)다. 온라인에서 한쪽이 응답하지 않으면
        //   호스트 코루틴의 WaitUntil이 영원히 풀리지 않아 양쪽 모두 정지하기 때문이다.
        //
        // ⚠️ 지금은 UnlimitedInputForTesting = true 라서 온라인도 무제한이다 (2026-08-17, 테스트 편의).
        //    출시 전에 반드시 false로 되돌릴 것. 함께 되돌릴 것:
        //      Data/CommonConfig.json 의 choose_wait_time (지금 120000ms = 2분, 원래 10000ms)
        //
        // ⚠️ choose_wait_time을 '무한'으로 두면 안 된다.
        //    게스트가 아직 답할 수 없는 요청(스택 발동·카드 선택)이 오면 그 시간만큼 양쪽이 멈춘다.
        //    그래서 넉넉하되 반드시 끝나는 값으로 둔다.
        //    → 서버의 세트/오픈/스택 대기는 이 헬퍼가 아니라 choose_wait_time을 직접 쓴다.
        GameLogicHelpers.AllowUnlimitedHumanInput = UnlimitedInputForTesting;

        bool isHost = GameData.MyRole == "HOST";
        var network = FindFirstObjectByType<firebase_network>(FindObjectsInactive.Include);

        if (isHost)
        {
            EnsureHostRuntime(network);
        }

        WatchSessionExit(network);

        Debug.Log(
            $"[OnlineMatchStarter] 온라인 대전 준비 — 세션 {GameData.SessionCode} / 역할 {GameData.MyRole} / " +
            $"덱 {(GameData.MyDeck != null ? GameData.MyDeck.Count : 0)}종 / " +
            $"firebase_network {(network != null ? "연결" : "없음")} / " +
            $"ServerGameManager {(ServerGameManager.Instance != null ? "준비" : (isHost ? "실패" : "게스트는 불필요"))} / " +
            $"EventService {(EventService.Instance != null ? "준비" : (isHost ? "실패" : "게스트는 불필요"))}");
    }

    /// <summary>호스트만 권한 서버(ServerGameManager)와 이벤트 브리지(EventService)를 든다.</summary>
    private void EnsureHostRuntime(firebase_network network)
    {
        // 이미 씬에 배치돼 있으면(TestServerConnect 등) 그대로 쓴다
        var host = FindFirstObjectByType<ServerGameManager>(FindObjectsInactive.Include);
        var bridge = FindFirstObjectByType<EventService>(FindObjectsInactive.Include);
        if (host != null && bridge != null)
        {
            LinkNetwork(bridge, network);
            return;
        }

        var runtime = GameObject.Find(RuntimeObjectName) ?? new GameObject(RuntimeObjectName);

        if (host == null) runtime.AddComponent<ServerGameManager>();

        if (bridge == null)
        {
            // EventService.Awake는 MyRole이 HOST가 아니면 스스로 꺼진다.
            // GameData는 씬 로드 전에 채워지므로 이 시점에는 이미 올바른 값이다.
            bridge = runtime.AddComponent<EventService>();
        }

        LinkNetwork(bridge, network);
    }

    private const string RuntimeObjectName = "OnlineServerRuntime";

    // ─── 상대 이탈 감지 ─────────────────────────────────────────────────
    //
    // 호스트가 나가면 session_manage가 세션 노드를 통째로 지운다.
    // 그걸 감지하지 못하면 남은 쪽은 아무 일도 없는 빈 보드를 계속 보게 된다.
    //
    // ⚠️ 한계: 게스트가 나가면 세션은 남고 guest 칸만 비므로 이 감시로는 잡히지 않는다.
    //    그 경우 호스트는 입력 제한 시간이 지나며 자동 진행된다.

    private bool _exitWatchAttached;

    private void WatchSessionExit(firebase_network network)
    {
        if (_exitWatchAttached || network == null) return;

        try
        {
            network.ListenForSessionExit(GameData.SessionCode, HandleSessionDestroyed);
            _exitWatchAttached = true;
        }
        catch (System.Exception)
        {
            // 파이어베이스 초기화 전이면 다음 씬 로드 때 다시 시도한다
        }
    }

    private void HandleSessionDestroyed()
    {
        if (!IsOnlineSessionActive) return;

        Debug.Log("[OnlineMatchStarter] 세션이 사라졌다 — 상대가 나갔거나 방이 종료되었다.");
        EventManager.OnLogMessage?.Invoke("<color=#ffd479>상대가 대전을 떠났습니다.</color>");

        if (GameStatusPanelUI.Instance != null)
            GameStatusPanelUI.Instance.ShowNotice("상대가 나갔습니다", "대전이 종료되었습니다.");
    }

    // ─── 끝난 방 정리 ───────────────────────────────────────────────────
    //
    // 방을 지우지 않으면 state가 PLAYING인 채 DB에 영구히 쌓인다(실제로 그렇게 쌓여 있었다).
    // 호스트가 나가면 세션 전체 삭제, 게스트가 나가면 guest 칸만 비운다 — firebase_network.ExitSession이 알아서 갈라 준다.

    private static void ReleaseSession()
    {
        string code = GameData.SessionCode;
        string id = GameData.MyID;
        if (string.IsNullOrEmpty(code)) return;

        var network = FindFirstObjectByType<firebase_network>(FindObjectsInactive.Include);
        if (network == null) return;

        try
        {
            _ = network.ExitSession(code, id);
            Debug.Log($"[OnlineMatchStarter] 세션 {code} 정리 요청");
        }
        catch (System.Exception e)
        {
            Debug.Log($"[OnlineMatchStarter] 세션 정리 생략: {e.Message}");
        }
    }

    /// <summary>
    /// 온라인 매치를 떠날 때 세션 흔적을 지운다.
    /// <see cref="GameData"/>는 static이라 씬을 옮겨도 남는다. 남겨 두면 같은 실행 안에서
    /// 로컬 봇전을 시작할 때 <see cref="LocalMatchStarter"/>가 계속 비켜서게 된다.
    /// </summary>
    public static void ClearSession()
    {
        ReleaseSession(); // 나가기 전에 방을 정리한다 (세션 코드가 아직 살아 있어야 한다)

        GameData.SessionCode = null;
        GameData.MyRole = null;
        GameData.MyID = null;

        // 로컬 플레이로 돌아가므로 사람 입력 제한 시간을 다시 푼다
        GameLogicHelpers.AllowUnlimitedHumanInput = true;
    }

    // ─── UI → 네트워크 전송 ─────────────────────────────────────────────
    //
    // ★ 온라인에서는 호스트도 자기 입력을 네트워크로 보낸다.
    //   ServerGameManager는 OnRequireSetPhaseAction / OnRequireOpenPhaseAction을 쏠 때
    //   **콜백에 null을 넣고**(L520, L661) 응답은 파이어베이스 요청으로 받도록 만들어져 있다.
    //   그래서 로컬처럼 콜백을 부르면 안 되고, 아래 경로로 보내야 한다.
    //   (게스트는 OnlineGuestBoardAdapter가 진짜 콜백을 주므로 이 경로를 타지 않는다)

    private static session_game_manage _clientCache;

    private static session_game_manage ResolveClient()
    {
        if (_clientCache == null)
            _clientCache = FindFirstObjectByType<session_game_manage>(FindObjectsInactive.Include);

        return _clientCache;
    }

    /// <summary>세트할 카드를 호스트(서버)에 보낸다. 온라인이 아니면 false.</summary>
    public static bool SendSetChoice(Card card)
    {
        if (!IsOnlineSessionActive || card == null) return false;

        session_game_manage client = ResolveClient();
        if (client == null) return false;

        Debug.Log($"[OnlineMatchStarter] 세트 전송: {card.Name}");
        client.SendSetPhaseChoice(card.InstanceId, true);
        return true;
    }

    /// <summary>공개/폐기 선택을 호스트(서버)에 보낸다. 온라인이 아니면 false.</summary>
    public static bool SendOpenChoice(Card setCard, bool reveal)
    {
        if (!IsOnlineSessionActive || setCard == null) return false;

        session_game_manage client = ResolveClient();
        if (client == null) return false;

        Debug.Log($"[OnlineMatchStarter] 오픈 전송: {setCard.Name} → {(reveal ? "Open" : "Abandon")}");
        client.SendOpenPhaseChoice(setCard.InstanceId, reveal ? "Open" : "Abandon");
        return true;
    }

    private static void LinkNetwork(EventService bridge, firebase_network network)
    {
        if (bridge == null || network == null) return;
        if (bridge.networkService == null) bridge.networkService = network;
    }
}
