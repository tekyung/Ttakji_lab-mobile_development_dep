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
    
    private string myID;
    private string currentSessionCode;
    private bool amIHost = false;

    /// <summary>내가 이 세션의 호스트인지. 매칭 UI가 자동 시작을 호스트에서만 걸기 위해 읽는다.</summary>
    public bool IsHost => amIHost;
    private Coroutine DestroySessionTimer;

    [SerializeField] private string nextSceneName = "TestGameScene";

    public float session_time_limit = 50f; // 세션 제한시간
    private Coroutine turnTimer;

    public Action<string> onStatusUpdate;
    public Action onMatchedAndReady;

    private void NotifyStatus(string msg)
    {
        uiManager?.UpdateStatus(msg);
        onStatusUpdate?.Invoke(msg);
    }

    private string GetOrGenerateID()
    {
        if (uiManager != null)
            return uiManager.GetID();
        return Random.Range(0, 1000000).ToString();
    }

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
        networkService.OnGameReady += HandleGameReady;
    }

    //비공개 세션 생성 함수
    public async void OnClickCreatePrivate()
    {
        await CreateSession(false);
    }

    //랜덤 매칭 함수
    public async void OnClickRandomMatch()
    {
        myID = GetOrGenerateID();
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
        if (string.IsNullOrEmpty(myID))
        {
            NotifyStatus("Enter ID");
            return;
        }

        uiManager?.ToggleUI(false);
        NotifyStatus("Creating Room...");

        string sessionCode = Random.Range(1000, 9999).ToString();
        string currentTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string secretState = isPublic ? SessionStatus.STATE_PUBLIC : SessionStatus.STATE_PRIVATE;

        session_data newSession = new session_data(myID, "", SessionStatus.STATE_WAITING, currentTime, secretState, "HOST");

        bool success = await networkService.CreateSession(sessionCode, newSession); 

        if (success)
        {
            amIHost = true;
            currentSessionCode = sessionCode;
            NotifyStatus($"Room Created: {sessionCode}");
            uiManager?.ToggleHost(true);
            networkService.ListenForGuest(currentSessionCode);
            networkService.ArmDisconnectCleanup(currentSessionCode, asHost: true);
            DestroySessionTimer = StartCoroutine(AutoDestroySession(sessionCode));
        }
        else
        {
            NotifyStatus("Create Failed");
            uiManager?.ToggleUI(true); 
        }
    }

    // 방 입장 공통 로직
    private async Task JoinSessionProcess(string sessionCode)
    {
        myID = GetOrGenerateID();
        if (string.IsNullOrEmpty(myID))
        {
            NotifyStatus("Enter ID");
            return;
        }

        uiManager?.ToggleUI(false);

        bool success = await networkService.JoinSession(sessionCode, myID);

        if (success)
        {
            amIHost = false;
            currentSessionCode = sessionCode;
            NotifyStatus($"Joined: {sessionCode}");
            uiManager?.ToggleHost(false);
            onMatchedAndReady?.Invoke();
            networkService.ArmDisconnectCleanup(currentSessionCode, asHost: false);
            StartReadyWatchdog();   // 매칭은 됐는데 시작이 안 되는 경우를 잡는다
            networkService.ListenForGameStart(currentSessionCode);
            networkService.ListenForSessionExit(currentSessionCode, () =>
            {
                NotifyStatus("Session Ended by Host");
                uiManager?.ToggleHost(true);
                uiManager?.ToggleUI(true);

                currentSessionCode = null;
                amIHost = false;

                networkService.StopListeningEvents();
                if (DestroySessionTimer != null) StopCoroutine(DestroySessionTimer);
            });
        }
        else
        {
            NotifyStatus("Join Failed / Room Not Found");
            uiManager?.ToggleUI(true);
        }
    }

    // 게스트가 들어왔을 경우
    private async void HandleGuestJoined(string guestID)
    {
        NotifyStatus($"{guestID} Joined!");
        onMatchedAndReady?.Invoke();
        await networkService.SetGameReady(currentSessionCode);
        StartReadyWatchdog();   // 여기서부터 20초 안에 PLAYING이 돼야 한다
        networkService.ListenForGameStart(currentSessionCode);
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

        string selectedName = PlayerPrefs.GetString("SelectedDeckName", "");
      
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