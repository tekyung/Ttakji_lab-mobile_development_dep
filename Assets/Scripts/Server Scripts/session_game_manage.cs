using UnityEngine;
using static session_manage;
using System.Collections;

public class session_game_manage : MonoBehaviour
{
    public firebase_network networkService;
    public session_ui uiManager;
    private string sessionRoom;
    private string myRole;

    public float turn_time_limit = 10f;
    private Coroutine turnTimer;

    private ActionType? pendingAction = null;
    private string pendingStatus = null;


    //async void Start()
    //{
    //    await networkService.Initialize();

    //    sessionRoom = GameData.SessionCode;
    //    myRole = GameData.MyRole;
    //    networkService.ListenForEvent(sessionRoom, HandleActionEvent);
    //    networkService.ListenForTurn(sessionRoom, HandleTurnChange);

    //    if (GameData.MyDeck != null && GameData.MyDeck.Count > 0)
    //    { 
    //        DeckSaveData myData = new DeckSaveData();
    //        myData.cardIdList = GameData.MyDeck;

    //        // 2. JSON 문자열로 변환
    //        string jsonDeck = JsonUtility.ToJson(myData);

    //        // 3. 파이어베이스로 전송
    //        await networkService.UploadDeck(sessionRoom, myRole, jsonDeck);
    //    }
    //    else
    //    {
    //        Debug.LogError("Deck not exitst");
    //    }

    //    if (myRole == "HOST")
    //    {
    //        StartMyTurn();
    //    }
    //    else if (myRole == "GUEST")
    //    {
    //        EndMyTurn();
    //    }
    //}
    async void Start()
    {
        await networkService.Initialize();

        sessionRoom = GameData.SessionCode;
        myRole = GameData.MyRole;
        networkService.ListenForEvent(sessionRoom, HandleActionEvent);
        networkService.ListenForTurn(sessionRoom, HandleTurnChange);

        Debug.Log("=====================================");
        Debug.Log($"[게임 씬 진입] {myRole}의 덱 전송 준비...");

        // 가방(GameData.MyDeck)을 열어봅니다.
        if (GameData.MyDeck != null && GameData.MyDeck.Count > 0)
        {
            Debug.Log($"[전송 시작] 가방 안에 {GameData.MyDeck.Count}장의 카드가 있습니다. 서버로 보냅니다!");

            DeckSaveData myData = new DeckSaveData();
            myData.cardIdList = GameData.MyDeck;
            string jsonDeck = JsonUtility.ToJson(myData);

            await networkService.UploadDeck(sessionRoom, myRole, jsonDeck);
            Debug.Log("[전송 완료] 파이어베이스 업로드 성공!");
        }
        else
        {
            Debug.LogError("[전송 실패] Deck not exist! 가방(GameData.MyDeck)이 텅 비어있습니다.");
        }
        Debug.Log("=====================================");

        if (myRole == "HOST") StartMyTurn();
        else if (myRole == "GUEST") EndMyTurn();
    }

    private void HandleActionEvent(string action, string sender)
    {
        pendingStatus = ($"{sender}: {action}");

        if (System.Enum.TryParse(action, out ActionType receivedType))
        {
            pendingAction = receivedType;
        }
    }

    // 턴 변경 수신 처리
    private void HandleTurnChange(string newTurn)
    {
        uiManager.ResetButtons();
        bool isMyTurn = (myRole == newTurn);
        if (isMyTurn) StartMyTurn();
        else EndMyTurn();
    }

    private void StartMyTurn()
    {
        uiManager.SetActionButtonsState(true); // 버튼 켜기
        turnTimer = StartCoroutine(TurnTimeoutRoutine());

    }

    // 내 턴 종료
    private void EndMyTurn()
    {
        uiManager.SetActionButtonsState(false); // 버튼 끄기
        if (turnTimer != null)
        {
            StopCoroutine(turnTimer);
        }
    }
    private IEnumerator TurnTimeoutRoutine()
    {
        float timer = turn_time_limit;
        while (timer > 0)
        {
            timer -= Time.deltaTime;
            yield return null;
        }
        OnBtnClick_C(); // 강제 턴 넘김
    }

    public async void OnBtnClick_A()
    {
        if (string.IsNullOrEmpty(sessionRoom))
        {
            return;
        }
        if (turnTimer != null) StopCoroutine(turnTimer);
        turnTimer = StartCoroutine(TurnTimeoutRoutine());

        await networkService.SendAction(sessionRoom, ActionType.A.ToString(), myRole);
    }

    public async void OnBtnClick_B()
    {
        if (string.IsNullOrEmpty(sessionRoom))
        {
            return;
        }
        if (turnTimer != null) StopCoroutine(turnTimer);
        turnTimer = StartCoroutine(TurnTimeoutRoutine());

        await networkService.SendAction(sessionRoom, ActionType.B.ToString(), myRole);
    }

    public async void OnBtnClick_C() // 턴 넘기기
    {
        if (string.IsNullOrEmpty(sessionRoom))
        {
            return;
        }
        if (turnTimer != null) StopCoroutine(turnTimer);
        uiManager.SetActionButtonsState(false); // ui잠금

        
        string nextTurn = (myRole == "HOST") ? "GUEST" : "HOST";

        // 1. 이벤트 전송
        await networkService.SendAction(sessionRoom, "Pass Turn", myRole);
        // 2. 턴 상태 변경
        await networkService.ChangeTurn(sessionRoom, nextTurn);
    }
    void Update()
    {
        if (pendingStatus != null)
        {
            uiManager.UpdateStatus(pendingStatus);
            pendingStatus = null; 
        }

        if (pendingAction.HasValue)
        {
            uiManager.CheckButton(pendingAction.Value);
            pendingAction = null; 
        }
    }
}
