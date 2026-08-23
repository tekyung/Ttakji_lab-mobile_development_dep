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
        networkService.ListenForGameStart(currentSessionCode);
    }

    private void HandleGameReady() 
    {
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