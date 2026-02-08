using System.Collections; 
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class session_manage : MonoBehaviour
{
    [SerializeField] private firebase_network networkService;
    [SerializeField] private session_ui uiManager;
    
    private string myID;
    private string currentSessionCode;

    async void Start()
    {
        bool isConnected = await networkService.Initialize();

        if (isConnected)
        {
            uiManager.UpdateStatus("Firebase Connected");
        }
        else
        {
            uiManager.UpdateStatus("Firebase Connect Failed");
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

        myID = uiManager.GetID();
        if (string.IsNullOrEmpty(myID))
        {
            uiManager.UpdateStatus("Enter ID");
            return;
        }

        uiManager.UpdateStatus("Searching...");
        uiManager.ToggleUI(false);

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
        string inputCode = uiManager.GetSessionCode();
        if (string.IsNullOrEmpty(inputCode)) return;

        await JoinSessionProcess(inputCode);
    }
    public async void OnClickExitSession() //세션에서 나가는 함수
    {
    if (string.IsNullOrEmpty(currentSessionCode)) // 세션에 들어가 있지 않다면 무시
        {
        uiManager.UpdateStatus("No session to exit");
        return;
    }

    // myID가 비어 있으면 UI에서 다시 가져오기
    if (string.IsNullOrEmpty(myID))
    {
        myID = uiManager.GetID();
    }

    bool success = await networkService.ExitSession(currentSessionCode, myID); 
    //세션 나가기를 성공했는지 

    if (success)
    {
        
        uiManager.UpdateStatus("session exit");
        uiManager.ToggleHost(true);
        uiManager.ToggleUI(true);          // 로비 UI 다시 열기
        currentSessionCode = null;         // 현재 세션 코드 초기화
    }
    else
    {
        uiManager.UpdateStatus("session exit failed");
    }
    }

    public async void OnClickSessionStart() //세션 시작 함수
    {
        if (string.IsNullOrEmpty(currentSessionCode)) //세션 코드가 없을 경우 시작 x
        {
            uiManager.UpdateStatus("No Session Code");
            return;
        }

        await networkService.SetGameStart(currentSessionCode);  //게임 상태를 playing으로 변경
    }

    private async Task CreateSession(bool isPublic) //세션 생성 함수
    {
        
        myID = uiManager.GetID();
        if (string.IsNullOrEmpty(myID)) //id가 비어있을 경우 세션 생성x
        {
            uiManager.UpdateStatus("Enter ID");
            return;
        }

        uiManager.ToggleUI(false); // UI 잠금
        uiManager.UpdateStatus("Creating Room...");

        string sessionCode = Random.Range(1000, 9999).ToString();  // 세션 코드는 1000~9999 랜덤 생성
        string currentTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");   // 세션 생성 시간
        string secretState = isPublic ? SessionStatus.STATE_PUBLIC : SessionStatus.STATE_PRIVATE;   // 공개방 여부에 따라 state가 PUBLIC 또는 PRIVATE로 나뉨

       session_data newSession = new session_data(myID, "", SessionStatus.STATE_WAITING, currentTime, secretState); // 세션 데이터를 생성

        bool success = await networkService.CreateSession(sessionCode, newSession); 

        if (success) //세션 생성에 성공한 경우
        {
            
            currentSessionCode = sessionCode;
            uiManager.UpdateStatus($"Room Created: {sessionCode}");
            uiManager.ToggleHost(true);
            networkService.ListenForGuest(currentSessionCode);
        }
        else //세션 생성에 실패한 경우
        {
            uiManager.UpdateStatus("Create Failed");
            uiManager.ToggleUI(true); 
        }
    }

    // 방 입장 공통 로직
    private async Task JoinSessionProcess(string sessionCode)
    {
        myID = uiManager.GetID();
        if (string.IsNullOrEmpty(myID)) //id가 비어있을 경우 세션 입장x
        {
            uiManager.UpdateStatus("Enter ID");
            return;
        }

        uiManager.ToggleUI(false);

        bool success = await networkService.JoinSession(sessionCode, myID);

        if (success)
        {
            currentSessionCode = sessionCode;
            uiManager.UpdateStatus($"Joined: {sessionCode}");
            uiManager.ToggleHost(false);
            // 게스트 게임이 시작 감지
            networkService.ListenForGameStart(currentSessionCode);
        }



        else
        {
            uiManager.UpdateStatus("Join Failed / Room Not Found");
            uiManager.ToggleUI(true);
        }
    }

    // 게스트가 들어왔을 경우
    private async void HandleGuestJoined(string guestID)
    {
        uiManager.UpdateStatus($"{guestID} Joined!");
        await networkService.SetGameReady(currentSessionCode); //게임 상태를 READY로 변경
        // 게임 시작 신호를 감지
        networkService.ListenForGameStart(currentSessionCode);
    }
    private void HandleGameReady()
    {
        StartCoroutine(HandleGameStarted());
    }

    private IEnumerator HandleGameStarted() // 게임 시작 신호가 왔을 때 (임시)
    {
        uiManager.UpdateStatus("3...");
        yield return new WaitForSeconds(1f);

        uiManager.UpdateStatus("2...");
        yield return new WaitForSeconds(1f);

        uiManager.UpdateStatus("1...");
        yield return new WaitForSeconds(1f);

        uiManager.UpdateStatus("Game Start!");
    }

    void OnApplicationQuit()
    {
        networkService.GoOffline();
    }
}