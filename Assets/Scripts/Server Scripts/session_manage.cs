using System;
using System.Collections; 
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

public class session_manage : MonoBehaviour
{
    [SerializeField] private firebase_network networkService;
    [SerializeField] private session_ui uiManager;
    
    /// <summary>내 <b>신원</b>. 서버가 호스트/게스트를 가르는 데 쓰는 값이라 겹치면 안 된다.</summary>
    private string myID;

    /// <summary>내 <b>표시 이름</b>. 화면에만 쓴다.</summary>
    private string myName;

    private string currentSessionCode;
    private bool amIHost = false;

    /// <summary>내가 이 세션의 호스트인지. 매칭 UI가 자동 시작을 호스트에서만 걸기 위해 읽는다.</summary>
    public bool IsHost => amIHost;

    /// <summary>지금 들어가 있는 방 번호. 커스텀 방은 이 번호를 화면에 띄워 상대에게 불러 준다.</summary>
    public string CurrentSessionCode => currentSessionCode;

    /// <summary>내 표시 이름. 방 화면의 내 칸에 쓴다.</summary>
    public string MyDisplayName => string.IsNullOrEmpty(myName) ? PlayerProfile.DisplayName : myName;

    /// <summary>맞은편 사람의 표시 이름. 아무도 없으면 빈 문자열이다.</summary>
    public string OpponentName => opponentName ?? "";

    private string opponentName;

    /// <summary>
    /// 맞은편 사람의 <b>신원</b>. [퇴장]은 이 값으로 서버를 부른다.
    ///
    /// ★ 표시 이름으로 부르면 안 된다 — 이름이 겹치는 순간 서버의 ExitSession이
    ///   엉뚱한 사람을 가리켜 <b>방을 통째로 지운다.</b>
    /// </summary>
    private string opponentId;

    /// <summary>
    /// 커스텀(비공개) 방인가. 랜덤 매칭과 <b>수명 규칙이 다르다</b> —
    /// 커스텀 방은 호스트가 사람을 기다리는 동안 스스로 사라지면 안 된다.
    /// 자세한 것은 <see cref="AutoDestroySession"/>과 <see cref="StartReadyWatchdog"/> 참조.
    /// </summary>
    private bool isCustomRoom;

    /// <summary>
    /// 이 방에 게임 시작 감시를 이미 걸었는가.
    ///
    /// ★ ListenForGameStart는 ValueChanged 핸들러를 <b>더할 뿐 지우지 않는다.</b>
    ///   퇴장 → 재입장을 반복하면 HandleGuestJoined가 그때마다 다시 걸어
    ///   OnGameReady가 여러 번 발화하고, 결국 <b>씬을 두 번 로드</b>한다.
    /// </summary>
    private bool gameStartListenerArmed;
    private Coroutine DestroySessionTimer;

    [SerializeField] private string nextSceneName = "TestGameScene";

    public float session_time_limit = 50f; // 세션 제한시간
    private Coroutine turnTimer;

    public Action<string> onStatusUpdate;
    public Action onMatchedAndReady;

    /// <summary>게스트가 들어왔다. 인자는 그 사람의 표시 이름 — 호스트가 보고 판단한다.</summary>
    public Action<string> onGuestJoinedName;

    /// <summary>호스트 쪽: 맞은편 자리가 비었다(상대가 나갔거나 내가 퇴장시켰다).</summary>
    public Action onGuestLeft;

    /// <summary>게스트 쪽: 내가 방에서 퇴장당했다.</summary>
    public Action onKickedFromRoom;

    /// <summary>입장에 실패했다. 이유에 따라 다른 안내를 띄우기 위해 결과를 그대로 넘긴다.</summary>
    public Action<JoinResult> onJoinFailed;

    private void NotifyStatus(string msg)
    {
        uiManager?.UpdateStatus(msg);
        onStatusUpdate?.Invoke(msg);
    }

    /// <summary>
    /// 서버에 올릴 <b>신원</b>. 사람이 읽는 값이 아니다.
    ///
    /// ★ 두 번 갈아탔다.
    ///   ① 처음엔 <c>Random.Range(0, 1000000)</c> 폴백이었다 — session_ui가 'server ui' 디버그 씬에만
    ///      있어 메인 메뉴는 늘 이 폴백을 탔고, 상대에게 "482913" 같은 숫자로 보였다.
    ///   ② 그래서 표시 이름(<c>이름#1234</c>)으로 바꿨는데 이번엔 <b>신원으로 쓰기에 위험</b>했다 —
    ///      서버의 ExitSession이 <c>myID == hostId</c>로 역할을 가르므로,
    ///      이름이 겹치면 게스트가 나갈 때 방이 통째로 지워진다.
    ///   → 지금은 설치본마다 만들어지는 GUID를 쓰고, 이름은 따로 나른다.
    /// </summary>
    private string GetOrGenerateID()
    {
        if (uiManager != null)
            return uiManager.GetID();

        return PlayerProfile.PlayerId;
    }

    /// <summary>화면에 띄울 내 이름. 신원과 함께 세션에 올려 상대가 읽게 한다.</summary>
    private string ResolveMyName()
        => uiManager != null ? uiManager.GetID() : PlayerProfile.DisplayName;

    async void Start()
    {
        bool isConnected = await networkService.Initialize();

        if (isConnected)
        {
            NotifyStatus("Firebase Connected");
        }
        else
        {
            NotifyStatus("Firebase Connect Failed");
        }

        networkService.OnGuestJoined += HandleGuestJoined;
        networkService.OnGuestLeft += HandleGuestLeft;
        networkService.OnGameReady += HandleGameReady;
    }

    //비공개 세션 생성 함수
    public async void OnClickCreatePrivate()
    {
        await CreateSession(false);
    }

    /// <summary>
    /// 커스텀 방을 연다. 비공개 방이면서 <b>스스로 사라지지 않는다</b> —
    /// 호스트가 방 번호를 불러 주고 상대가 들어올 때까지 기다려야 하기 때문이다.
    /// </summary>
    public async void OnClickCreateCustomRoom()
    {
        isCustomRoom = true;
        await CreateSession(false);
    }

    /// <summary>
    /// 방 번호를 직접 받아 입장한다.
    ///
    /// ★ <see cref="OnClickJoin"/>은 쓸 수 없다 — 첫 줄이 <c>if (uiManager == null) return;</c>인데
    ///   <b>메인 메뉴에는 session_ui가 없다.</b> 그래서 그 경로는 조용히 아무 일도 하지 않았다.
    /// </summary>
    public async void JoinRoomByCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return;

        isCustomRoom = true;
        await JoinSessionProcess(code.Trim());
    }

    /// <summary>
    /// 호스트가 게스트만 내보낸다. <b>방은 남는다</b> —
    /// 서버의 ExitSession이 게스트 ID로 불리면 guest 칸을 비우고 상태를 WAITING으로 되돌리므로,
    /// 그대로 다음 사람을 기다릴 수 있다.
    /// </summary>
    public async void KickGuest()
    {
        if (!amIHost || string.IsNullOrEmpty(currentSessionCode)) return;
        if (string.IsNullOrEmpty(opponentId))
        {
            NotifyStatus("내보낼 상대가 없습니다");
            return;
        }

        StopReadyWatchdog();

        // ★ 표시 이름이 아니라 <b>신원</b>으로 부른다. 이름으로 부르면 서버가 사람을 잘못 가른다.
        bool success = await networkService.ExitSession(currentSessionCode, opponentId);
        NotifyStatus(success ? "상대를 내보냈습니다" : "내보내지 못했습니다");
    }

    //랜덤 매칭 함수
    public async void OnClickRandomMatch()
    {
        myID = GetOrGenerateID();
        myName = ResolveMyName();
        if (string.IsNullOrEmpty(myID))
        {
            NotifyStatus("Enter ID");
            return;
        }

        NotifyStatus("Searching...");
        uiManager?.ToggleUI(false);

        // 대기 중인 방 목록 가져오기
        List<string> publicRooms = await networkService.GetPublicSession();
        
        if (publicRooms.Count > 0)
        {
            // PUBLIC 상태인 방이 있으면 랜덤으로 하나 골라서 입장
            int randomIndex = Random.Range(0, publicRooms.Count);
            string targetRoom = publicRooms[randomIndex];
            Debug.Log($"Matched: {targetRoom}");
            await JoinSessionProcess(targetRoom);
        }
        else
        {
            // 방이 없으면 자신이 세션을 생성
            await CreateSession(true);
        }
        
    }

    public async void OnClickJoin() // 세션 참가 함수
    {
        if (uiManager == null) return;
        string inputCode = uiManager.GetSessionCode();
        if (string.IsNullOrEmpty(inputCode)) return;

        await JoinSessionProcess(inputCode);
    }

    public async void OnClickExitSession() //세션에서 나가는 함수
    {
        if (string.IsNullOrEmpty(currentSessionCode))
        {
            NotifyStatus("No session to exit");
            return;
        }
    
        if (string.IsNullOrEmpty(myID))
        {
            myID = GetOrGenerateID();
        }

        StopReadyWatchdog();

        // ★ 예약을 먼저 취소한다. 남겨 두면 나중에 재접속했을 때 엉뚱하게 발동한다.
        networkService.CancelDisconnectCleanup(currentSessionCode);

        bool success = await networkService.ExitSession(currentSessionCode, myID); 

        if (success)
        {
            if (DestroySessionTimer != null)
            {
                StopCoroutine(DestroySessionTimer);
                uiManager?.DestroySessionTimer(0);
            }
            NotifyStatus("session exit");
            uiManager?.ToggleHost(true);
            uiManager?.ToggleUI(true);
            currentSessionCode = null;
            amIHost = false;
            opponentName = null;
            opponentId = null;
            gameStartListenerArmed = false;
            isCustomRoom = false;
            if (turnTimer != null) StopCoroutine(turnTimer);
        }
        else
        {
            NotifyStatus("session exit failed");
        }
    }

    // ─── 매칭 후 시작 감시 ──────────────────────────────────────────────
    //
    // 매칭이 성사되면(READY) 곧바로 PLAYING으로 넘어가야 한다. 그 사이에 한쪽이 멈추거나
    // 연결이 끊기면 방이 READY인 채로 영영 남는다 — 사람이 없는데 목록에도 안 잡히는 방이다.
    // 서버에 남아야 할 방은 **사람을 찾는 방(WAITING)과 진행 중인 방(PLAYING)뿐**이다.

    /// <summary>매칭 후 이 시간 안에 게임이 시작되지 않으면 연결이 끊긴 것으로 본다.</summary>
    private const float ReadyStartTimeoutSeconds = 20f;

    private Coroutine readyWatchdog;

    private void StartReadyWatchdog()
    {
        StopReadyWatchdog();

        // ★ 커스텀 방은 감시하지 않는다.
        //   이 감시는 "매칭됐는데 20초 안에 시작되지 않으면 연결이 끊긴 것"이라는 가정 위에 있다.
        //   커스텀 방에서는 <b>호스트가 상대를 보고 판단하는 시간</b>이므로 20초는 근거가 없다.
        if (isCustomRoom) return;

        readyWatchdog = StartCoroutine(WatchReadyStart(currentSessionCode));
    }

    private void StopReadyWatchdog()
    {
        if (readyWatchdog == null) return;

        StopCoroutine(readyWatchdog);
        readyWatchdog = null;
    }

    private IEnumerator WatchReadyStart(string roomCode)
    {
        float remain = ReadyStartTimeoutSeconds;
        while (remain > 0f)
        {
            remain -= Time.deltaTime;
            yield return null;
        }

        readyWatchdog = null;

        // 그새 방을 떠났거나 다른 방에 들어갔으면 남의 방을 건드리면 안 된다.
        if (currentSessionCode != roomCode) yield break;

        // 서버에 한 번 더 물어본다 — 알림을 놓쳤을 뿐 이미 시작됐을 수도 있다.
        var probe = networkService.GetSessionStatus(roomCode);
        yield return new WaitUntil(() => probe.IsCompleted);

        string state = probe.Status == TaskStatus.RanToCompletion ? probe.Result : null;
        if (state == SessionStatus.STATE_PLAYING) yield break;

        Debug.Log($"[세션] {roomCode} 매칭 후 {ReadyStartTimeoutSeconds}초 동안 시작되지 않았다 " +
                  $"(상태 {state ?? "확인 실패"}) — 연결이 끊긴 것으로 보고 정리한다.");

        networkService.CancelDisconnectCleanup(roomCode);

        var exit = networkService.ExitSession(roomCode, myID);
        yield return new WaitUntil(() => exit.IsCompleted);

        NotifyStatus("상대의 응답이 없어 매칭을 취소했습니다");
        uiManager?.ToggleHost(true);
        uiManager?.ToggleUI(true);

        currentSessionCode = null;
        amIHost = false;
    }

    private IEnumerator AutoDestroySession(string roomCode)
    {
        float timer = session_time_limit;

        while (timer > 0)
        {
            timer -= Time.deltaTime;
            uiManager?.DestroySessionTimer(timer); 
            yield return null; 
        }

        if (currentSessionCode == roomCode && amIHost)
        {
            var task = networkService.ExitSession(roomCode, myID);

            NotifyStatus("Session Timeout Deleted");
            uiManager?.ToggleHost(true);
            uiManager?.ToggleUI(true);

            currentSessionCode = null;
            amIHost = false;
        }
        else
        {
            Debug.Log("session not deleted");
        }
    }

    public async void OnClickSessionStart() //세션 시작 함수
    {
        if (string.IsNullOrEmpty(currentSessionCode))
        {
            NotifyStatus("No Session Code");
            return;
        }

        await networkService.SetGameStart(currentSessionCode);
    }

    private async Task CreateSession(bool isPublic) //세션 생성 함수
    {
        myID = GetOrGenerateID();
        myName = ResolveMyName();
        if (string.IsNullOrEmpty(myID))
        {
            NotifyStatus("Enter ID");
            return;
        }

        uiManager?.ToggleUI(false);
        NotifyStatus("Creating Room...");

        string sessionCode = await GenerateUnusedSessionCode();
        if (string.IsNullOrEmpty(sessionCode))
        {
            NotifyStatus("빈 방 번호를 찾지 못했습니다");
            uiManager?.ToggleUI(true);
            return;
        }

        string currentTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string secretState = isPublic ? SessionStatus.STATE_PUBLIC : SessionStatus.STATE_PRIVATE;

        session_data newSession = new session_data(myID, "", SessionStatus.STATE_WAITING, currentTime, secretState, "HOST");
        newSession.hostName = myName;   // 게스트 화면이 이걸 읽어 호스트 이름을 띄운다
        newSession.guestName = "";

        bool success = await networkService.CreateSession(sessionCode, newSession); 

        if (success)
        {
            amIHost = true;
            currentSessionCode = sessionCode;
            opponentName = null;
            opponentId = null;
            gameStartListenerArmed = false;
            NotifyStatus($"Room Created: {sessionCode}");
            uiManager?.ToggleHost(true);
            networkService.ListenForGuest(currentSessionCode);
            networkService.ArmDisconnectCleanup(currentSessionCode, asHost: true);

            // ★ 커스텀 방에는 제한 시간을 걸지 않는다.
            //   호스트가 번호를 불러 주고 상대가 들어올 때까지 기다리는 방인데
            //   session_time_limit(기본 50초)이 지나면 방이 통째로 사라진다.
            //   연결이 끊긴 경우는 ArmDisconnectCleanup이 이미 맡고 있으므로 유령 방은 남지 않는다.
            if (!isCustomRoom)
                DestroySessionTimer = StartCoroutine(AutoDestroySession(sessionCode));
        }
        else
        {
            NotifyStatus("Create Failed");
            uiManager?.ToggleUI(true); 
        }
    }

    /// <summary>
    /// 아무도 쓰지 않는 방 번호를 뽑는다.
    ///
    /// ★ 예전에는 <c>Random.Range(1000, 9999)</c>를 그냥 썼다. CreateSession은 덮어쓰기라
    ///   같은 번호가 나오는 순간 <b>남의 방이 조용히 사라졌다.</b> 네 자리는 9000가지뿐이다.
    /// </summary>
    private async Task<string> GenerateUnusedSessionCode()
    {
        const int MaxAttempts = 10;

        for (int i = 0; i < MaxAttempts; i++)
        {
            string candidate = Random.Range(1000, 10000).ToString();
            if (!await networkService.SessionExists(candidate)) return candidate;

            Debug.Log($"[세션] 방 번호 {candidate}가 이미 쓰이고 있어 다시 뽑습니다.");
        }

        Debug.LogError($"[세션] {MaxAttempts}번 시도했지만 빈 방 번호를 찾지 못했습니다.");
        return null;
    }

    // 방 입장 공통 로직
    private async Task JoinSessionProcess(string sessionCode)
    {
        myID = GetOrGenerateID();
        myName = ResolveMyName();
        if (string.IsNullOrEmpty(myID))
        {
            NotifyStatus("Enter ID");
            return;
        }

        uiManager?.ToggleUI(false);

        JoinResult result = await networkService.TryJoinSession(sessionCode, myID, myName);

        if (result == JoinResult.Success)
        {
            amIHost = false;
            currentSessionCode = sessionCode;
            gameStartListenerArmed = false;

            // 맞은편은 호스트다. 화면에 띄울 이름을 방에서 읽어 온다.
            opponentName = await networkService.GetPlayerName(sessionCode, "hostName");
            NotifyStatus($"Joined: {sessionCode}");
            uiManager?.ToggleHost(false);
            onMatchedAndReady?.Invoke();
            networkService.ArmDisconnectCleanup(currentSessionCode, asHost: false);
            StartReadyWatchdog();   // 매칭은 됐는데 시작이 안 되는 경우를 잡는다

            // ★ 게스트도 guest 칸을 지켜본다. 호스트가 나를 내보내면 그 칸이 비는데,
            //   ListenForSessionExit은 <b>방이 통째로 사라질 때만</b> 발화하므로 퇴장을 못 잡는다.
            networkService.ListenForGuest(currentSessionCode);

            ArmGameStartListener();
            networkService.ListenForSessionExit(currentSessionCode, () =>
            {
                NotifyStatus("Session Ended by Host");
                uiManager?.ToggleHost(true);
                uiManager?.ToggleUI(true);

                currentSessionCode = null;
                amIHost = false;
                opponentName = null;
                opponentId = null;
                gameStartListenerArmed = false;
                isCustomRoom = false;

                networkService.StopListeningEvents();
                if (DestroySessionTimer != null) StopCoroutine(DestroySessionTimer);

                // 화면에서 보면 퇴장당한 것과 같다 — 방을 떠나 원래 자리로 돌아가야 한다.
                // 무엇 때문인지는 위 NotifyStatus가 알린다.
                onKickedFromRoom?.Invoke();
            });
        }
        else
        {
            NotifyStatus(result == JoinResult.NotFound
                ? "그 번호의 방이 없습니다"
                : "이미 자리가 찼습니다");

            uiManager?.ToggleUI(true);
            onJoinFailed?.Invoke(result);
        }
    }

    /// <summary>
    /// 게임 시작 감시를 <b>이 방에 한 번만</b> 건다.
    ///
    /// ★ ListenForGameStart는 ValueChanged 핸들러를 더하기만 한다. 퇴장 → 재입장을 반복하면
    ///   HandleGuestJoined가 그때마다 다시 걸어 OnGameReady가 여러 번 발화하고,
    ///   HandleGameStarted 코루틴이 겹쳐 <b>씬을 두 번 로드</b>한다.
    /// </summary>
    private void ArmGameStartListener()
    {
        if (gameStartListenerArmed || string.IsNullOrEmpty(currentSessionCode)) return;

        gameStartListenerArmed = true;
        networkService.ListenForGameStart(currentSessionCode);
    }

    // 게스트가 들어왔을 경우
    private async void HandleGuestJoined(string guestID)
    {
        // 게스트 쪽에서도 이 알림이 온다(자기 자리가 채워졌으므로). 그때는 내 이름이니 상대가 아니다.
        if (!amIHost) return;

        opponentId = guestID;

        // guestID는 GUID라 사람이 읽을 수 없다. 표시 이름은 방에 따로 적혀 있다.
        opponentName = await networkService.GetPlayerName(currentSessionCode, "guestName");
        if (string.IsNullOrEmpty(opponentName)) opponentName = guestID;

        NotifyStatus($"{opponentName} Joined!");
        onMatchedAndReady?.Invoke();
        await networkService.SetGameReady(currentSessionCode);

        // ★ 상태를 READY로 올린 <b>뒤에</b> 알린다.
        //   화면이 이 알림을 받고 [시작]을 켜는데, SetGameReady가 끝나기 전에 시작을 걸면
        //   PLAYING이 READY로 덮여 양쪽이 그대로 멈춘다.
        onGuestJoinedName?.Invoke(opponentName);

        StartReadyWatchdog();   // 여기서부터 20초 안에 PLAYING이 돼야 한다 (커스텀 방은 건너뛴다)
        ArmGameStartListener();
    }

    /// <summary>
    /// guest 칸이 비었다. 호스트에게는 "상대가 나갔다", 게스트에게는 "내가 퇴장당했다"는 뜻이다.
    /// </summary>
    private void HandleGuestLeft()
    {
        if (string.IsNullOrEmpty(currentSessionCode)) return;

        if (amIHost)
        {
            // ★ ValueChanged는 리스너를 <b>붙이는 순간에도 한 번 발화한다.</b>
            //   방을 갓 만든 시점에는 guest가 빈 값이라, 이 가드가 없으면
            //   생성 직후 "상대가 방을 나갔습니다"가 뜬다 — 아무도 온 적이 없는데.
            if (string.IsNullOrEmpty(opponentId)) return;

            opponentName = null;
            opponentId = null;
            StopReadyWatchdog();   // 다시 사람을 기다리는 상태로 돌아간다
            NotifyStatus("상대가 방을 나갔습니다");
            onGuestLeft?.Invoke();
            return;
        }

        // 게스트: 내 자리가 비워졌다 = 쫓겨났다.
        StopReadyWatchdog();
        networkService.CancelDisconnectCleanup(currentSessionCode);
        networkService.StopListeningEvents();

        currentSessionCode = null;
        opponentName = null;
        opponentId = null;
        gameStartListenerArmed = false;
        isCustomRoom = false;

        NotifyStatus("방에서 내보내졌습니다");
        onKickedFromRoom?.Invoke();
    }

    private void HandleGameReady()
    {
        // state가 PLAYING이 됐다는 뜻이다(ListenForGameStart). 시작 감시는 여기서 끝난다.
        StopReadyWatchdog();

        // 대전 중에는 양쪽 다 "끊기면 방 삭제"로 바꾼다.
        // 게스트가 빠진 채 WAITING으로 남으면 진행 중인 방에 제3자가 들어올 수 있다.
        networkService.ArmDisconnectRemoveRoom(currentSessionCode);
        StartCoroutine(HandleGameStarted());
    }

    private IEnumerator HandleGameStarted()
    {
        NotifyStatus("3...");
        yield return new WaitForSeconds(1f);
        NotifyStatus("2...");
        yield return new WaitForSeconds(1f);
        NotifyStatus("1...");
        yield return new WaitForSeconds(1f);
        NotifyStatus("Game Start!");
        yield return new WaitForSeconds(1f);

        GameData.SessionCode = currentSessionCode;
        GameData.MyID = myID;
        GameData.MyRole = amIHost ? "HOST" : "GUEST";

        // ★ 이 칸이 클라이언트마다 갈려 있어야 한다. 예전엔 PlayerPrefs를 직접 읽었는데,
        //   PlayerPrefs는 <b>기기 + 제품</b> 단위라 한 PC의 에디터와 빌드가 같은 칸을 봤다 —
        //   그래서 두 사람이 <b>같은 덱으로 시작</b>했다(나중에 고른 쪽이 양쪽 값이 됐다).
        string selectedName = PlayerStorage.GetSelectedDeck();
      
        if (!string.IsNullOrEmpty(selectedName) && selectedName != "덱 없음" && selectedName != "덱이 없습니다.")
        {
            string filePath = DeckStorage.GetDeckPath(selectedName);

            if (System.IO.File.Exists(filePath))
            {
                string jsonText = System.IO.File.ReadAllText(filePath);
                DeckSaveData parsedData = JsonUtility.FromJson<DeckSaveData>(jsonText);
                GameData.MyDeck = new List<string>(parsedData.cardIdList);

                // 덱 빌더가 명시적으로 남긴 용병 목록. 구버전 덱이면 비어 있다.
                GameData.MyCharacters = parsedData.characterIdList != null
                    ? new List<string>(parsedData.characterIdList)
                    : new List<string>();
            }
            else
            {
                Debug.LogError($"파일을 찾을 수 없습니다: {filePath}");
            }
        }
        else
        {
            Debug.LogError("선택된 덱 이름이 비어있거나 유효하지 않습니다!");
        }

        SceneManager.LoadScene(nextSceneName);
    }

    void OnApplicationQuit()
    {
        networkService.GoOffline();
    }
}