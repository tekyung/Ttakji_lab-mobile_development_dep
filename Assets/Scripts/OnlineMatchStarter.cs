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
using TCG_Project.Scripts.Systems;
using UnityEngine;
using UnityEngine.SceneManagement;

public class OnlineMatchStarter : MonoBehaviour
{
    private static OnlineMatchStarter _instance;

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

        var client = FindFirstObjectByType<session_game_manage>();
        if (client == null) return;         // 온라인 클라이언트가 없는 씬(메인 메뉴 등)

        // ★ 사람 입력 무제한 대기를 끈다.
        //   로컬에서는 사람이 얼마든지 생각해도 되지만, 온라인에서 한쪽이 응답하지 않으면
        //   호스트 코루틴의 WaitUntil이 영원히 풀리지 않아 양쪽 모두 정지한다.
        GameLogicHelpers.AllowUnlimitedHumanInput = false;

        bool isHost = GameData.MyRole == "HOST";
        var network = FindFirstObjectByType<firebase_network>();

        if (isHost)
        {
            EnsureHostRuntime(network);
        }

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
        var host = FindFirstObjectByType<ServerGameManager>();
        var bridge = FindFirstObjectByType<EventService>();
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

    /// <summary>
    /// 온라인 매치를 떠날 때 세션 흔적을 지운다.
    /// <see cref="GameData"/>는 static이라 씬을 옮겨도 남는다. 남겨 두면 같은 실행 안에서
    /// 로컬 봇전을 시작할 때 <see cref="LocalMatchStarter"/>가 계속 비켜서게 된다.
    /// </summary>
    public static void ClearSession()
    {
        GameData.SessionCode = null;
        GameData.MyRole = null;
        GameData.MyID = null;

        // 로컬 플레이로 돌아가므로 사람 입력 제한 시간을 다시 푼다
        GameLogicHelpers.AllowUnlimitedHumanInput = true;
    }

    private static void LinkNetwork(EventService bridge, firebase_network network)
    {
        if (bridge == null || network == null) return;
        if (bridge.networkService == null) bridge.networkService = network;
    }
}
