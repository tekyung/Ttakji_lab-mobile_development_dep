using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using static UnityEngine.EventSystems.StandaloneInputModule;

public class firebase_network : MonoBehaviour
{
    private static DatabaseReference dbRef;

    public event Action<string> OnGuestJoined;
    public event Action OnGameReady;

    private EventHandler<ChildChangedEventArgs> eventHandler;
    private DatabaseReference eventRef;
    public async Task<bool> Initialize()    //네트워크 초기화
    {
        var dependencystatus = await FirebaseApp.CheckAndFixDependenciesAsync();

        if (dependencystatus == DependencyStatus.Available)
        {

            dbRef = FirebaseDatabase.DefaultInstance.RootReference; //DB 연결
            return true;
        }
        else
        {
            Debug.Log("error : " + dependencystatus);
            return false;
        }
    }

    public async Task<bool> CreateSession(string sessioncode, session_data newSession)  //세션 생성 함수
    {
        string json = JsonUtility.ToJson(newSession);   //JSON형태로
        var task = dbRef.Child("sessions").Child(sessioncode).SetRawJsonValueAsync(json);
        await task;
        return task.IsCompleted;
    }

    public async Task<List<string>> GetPublicSession()  //공개된 세션 가져옴
    {
        if (dbRef == null)
        {
            return new List<string>();
        }

        DataSnapshot snapshot = await dbRef.Child("sessions").GetValueAsync();  
        List<string> PublicRooms = new List<string>();  
        //session안에 있는 데이터를 List에 넣음

        if (snapshot.Exists) 
        {
            //데이터가 존재할 경우
            foreach (var child in snapshot.Children)
            {
                if (!child.HasChild("state") || !child.HasChild("secret")) continue; //state와 secret상태가 없을경우 무시
                string stateWaiting = child.Child("state").Value.ToString();
                string statePrivate = child.Child("secret").Value.ToString();

                if (stateWaiting == SessionStatus.STATE_WAITING && statePrivate == SessionStatus.STATE_PUBLIC)  //state가 WAITING이고 secret이 PUBLIC인 경우에만 List에 추가
                {
                    PublicRooms.Add(child.Key);
                }
            }
        }
        return PublicRooms;
    }

    public async Task<bool> JoinSession(string sessioncode, string myID)    //세션에 들어가는 함수
    {
        // 세션 존재 여부 확인
        DataSnapshot snapshot = await dbRef.Child("sessions").Child(sessioncode).GetValueAsync();
        if (!snapshot.Exists) return false;

        var JoinSession = dbRef.Child("sessions").Child(sessioncode).Child("guest").SetValueAsync(myID);
        await JoinSession;
        return JoinSession.IsCompleted;
    }

    public async Task<bool> ExitSession(string sessioncode, string myID)    //세션에서 나가는 함수
{
    // 세션 존재 여부 확인
    var snapshot = await dbRef.Child("sessions").Child(sessioncode).GetValueAsync();
    if (!snapshot.Exists)
    {
        // 이미 삭제된 세션이면 그냥 나간 걸로 처리
        return true;
    }

    string hostId = snapshot.Child("host").Value?.ToString();
    string guestId = snapshot.Child("guest").Value?.ToString();

    //내가 호스트인 경우, 세션 전체 삭제
    if (myID == hostId)
    {
        var task = dbRef.Child("sessions").Child(sessioncode).RemoveValueAsync();
        await task;
        return task.IsCompleted;
    }
    // 내가 게스트인 경우,guest 비우고 상태 WAITING으로
    else if (myID == guestId)
    {
        var updates = new Dictionary<string, object>();
        updates["guest"] = "";
        updates["state"] = SessionStatus.STATE_WAITING;

        var task = dbRef.Child("sessions").Child(sessioncode).UpdateChildrenAsync(updates);
        await task;
        return task.IsCompleted;
    }
    // 세션에 등록된 host 또는 guest가 아니면 실패
    return false;
}
    public async Task SendAction(string sessioncode, string actionType, string senderRole)
    {
        var actionData = new Dictionary<string, string>();
        actionData["action"] = actionType;
        actionData["sender"] = senderRole;
        await dbRef.Child("sessions").Child(sessioncode).Child("events").Push().SetValueAsync(actionData);
    }
    public async Task ChangeTurn(string sessioncode, string nextTurn)
    {
        await dbRef.Child("sessions").Child(sessioncode).Child("turn").SetValueAsync(nextTurn);
    }

    // ─── 연결이 끊겼을 때의 뒷정리 예약 ─────────────────────────────────
    //
    // 앱이 강제 종료되거나 네트워크가 끊기면 "나갑니다"를 보낼 기회가 없다.
    // 그래서 **들어갈 때 미리** 서버에 부탁해 둔다 — 내 연결이 끊기면 이걸 대신 해 달라고.
    // 이게 없어서 사람이 없는 방이 서버에 쌓였다(한때 15개가 방치돼 있었다).

    /// <summary>
    /// 연결이 끊기면 자동으로 방을 정리하도록 예약한다.
    /// 호스트면 방 전체를 지우고, 게스트면 자기 자리만 비우고 다시 사람을 찾는 상태로 되돌린다.
    /// </summary>
    public void ArmDisconnectCleanup(string sessioncode, bool asHost)
    {
        if (dbRef == null || string.IsNullOrEmpty(sessioncode)) return;

        DatabaseReference session = dbRef.Child("sessions").Child(sessioncode);

        if (asHost)
        {
            session.OnDisconnect().RemoveValue();
        }
        else
        {
            var updates = new Dictionary<string, object>
            {
                ["guest"] = "",
                ["state"] = SessionStatus.STATE_WAITING
            };
            session.OnDisconnect().UpdateChildren(updates);
        }

        Debug.Log($"[firebase] 연결 끊김 대비 정리 예약 — {sessioncode} ({(asHost ? "HOST" : "GUEST")})");
    }

    /// <summary>
    /// 대전이 시작된 뒤의 예약. <b>역할과 무관하게 방을 지운다.</b>
    ///
    /// 진행 중인 방은 두 사람이 다 있어야 의미가 있다. 게스트가 끊겼다고 방을 WAITING으로
    /// 되돌리면, 이미 판이 돌고 있는 방에 제3자가 매칭될 수 있다.
    /// </summary>
    public void ArmDisconnectRemoveRoom(string sessioncode)
    {
        if (dbRef == null || string.IsNullOrEmpty(sessioncode)) return;

        dbRef.Child("sessions").Child(sessioncode).OnDisconnect().RemoveValue();
        Debug.Log($"[firebase] 대전 중 연결 끊김 대비 — {sessioncode} 방 삭제로 예약 변경");
    }

    /// <summary>
    /// 예약을 취소한다. <b>정상적으로 나갈 때는 반드시 먼저 부른다.</b>
    /// 취소하지 않으면 나중에 재접속했을 때 남아 있던 예약이 엉뚱하게 발동할 수 있다.
    /// </summary>
    public void CancelDisconnectCleanup(string sessioncode)
    {
        if (dbRef == null || string.IsNullOrEmpty(sessioncode)) return;

        dbRef.Child("sessions").Child(sessioncode).OnDisconnect().Cancel();
    }

    public async Task SetGameReady(string sessioncode)  //게임상태를 READY로 변환
    {
        await dbRef.Child("sessions").Child(sessioncode).Child("state").SetValueAsync(SessionStatus.STATE_READY);
    }
    public async Task SetGameStart(string sessioncode)  //게임 상태를 PLAYING으로 변환
    {
        await dbRef.Child("sessions").Child(sessioncode).Child("state").SetValueAsync(SessionStatus.STATE_PLAYING);
    }

    public void ListenForGuest(string sessioncode)  //게스트 입장 감지
    {
        dbRef.Child("sessions").Child(sessioncode).Child("guest").ValueChanged += (sender, args) =>
        {
            if (args.Snapshot.Exists && args.Snapshot.Value != null)
            {
                string guestID = args.Snapshot.Value.ToString();
                if (!string.IsNullOrEmpty(guestID))
                {
                    OnGuestJoined?.Invoke(guestID);
                }

            }
        };
    }

    public void ListenForGameStart(string sessioncode) //게임 시작 감지
    {
        dbRef.Child("sessions").Child(sessioncode).Child("state").ValueChanged += (sender, args) =>
        {
            if (args.Snapshot.Exists && args.Snapshot.Value.ToString() == SessionStatus.STATE_PLAYING)
            {
                OnGameReady?.Invoke();
            }
        };
    }

    // ⚠️ 함정: ListenForEvent와 ListenForEventDTO는 **같은 eventHandler/eventRef 필드 하나**를 공유한다.
    //   둘 다 StopListeningEvents()로 시작하므로, 나중에 부른 쪽이 먼저 부른 쪽을 **조용히 끊는다.**
    //   현재는 ListenForEventDTO만 쓰여 문제가 없지만, 둘을 동시에 써야 한다면
    //   필드를 분리하거나 board_state처럼 토큰 방식으로 바꿔야 한다.
    //이벤트리스너
    public void ListenForEvent(string sessioncode, Action<string,string> eventreceive)
    {
        StopListeningEvents();
        eventRef = dbRef.Child("sessions").Child(sessioncode).Child("events");
        eventHandler = (sender, args) =>
        {
            if (args.Snapshot.Exists)
            {
                var action = args.Snapshot.Child("action").Value?.ToString();
                var who = args.Snapshot.Child("sender").Value?.ToString();
                if (action != null && who != null) eventreceive?.Invoke(action, who);
            }
        };
        eventRef.ChildAdded += eventHandler;
    }
    //이벤트 리스너 삭제
    public void StopListeningEvents()
    {
        if (eventRef != null && eventHandler != null)
        {
            eventRef.ChildAdded -= eventHandler;
            eventHandler = null;
            eventRef = null;
        }
    }

    //턴 변경 감지
    public void ListenForTurn(string sessioncode, Action<string> onTurnChanged)
    {
        dbRef.Child("sessions").Child(sessioncode).Child("turn").ValueChanged += (sender, args) =>
        {
            if (args.Snapshot.Exists) onTurnChanged?.Invoke(args.Snapshot.Value.ToString());
        };
    }
    public void ListenForSessionExit(string sessioncode, Action onDestroyed)
    {
        dbRef.Child("sessions").Child(sessioncode).ValueChanged += (sender, args) =>
        {
            // 방 존재 검사
            if (!args.Snapshot.Exists)
            {
                onDestroyed?.Invoke();
            }
        };
    }
    public async Task<String> GetSessionStatus(String sessioncode)  //세션 상태를 반환
    {
        var task= await dbRef.Child("sessions").Child(sessioncode).Child("state").GetValueAsync();
        return task.Value.ToString();
    }

    public async Task UploadDeck(string sessioncode, string role, string Deck)
    {
        await dbRef.Child("sessions").Child(sessioncode).Child("decks").Child(role).SetRawJsonValueAsync(Deck);
    }

    /// <summary>
    /// 업로드된 덱을 읽어 카드 ID 목록으로 돌려준다. 아직 올라오지 않았으면 null.
    /// (호스트가 매치를 구성할 때 양쪽 덱을 가져오는 데 쓴다)
    /// </summary>
    /// <summary>
    /// 업로드된 덱을 통째로 읽는다. 아직 올라오지 않았으면 null.
    ///
    /// ★ 예전에는 cardIdList만 돌려줬는데, 그러면 덱이 명시한 <b>용병 목록</b>이 버려진다.
    ///   호스트가 매치를 구성할 때 둘 다 필요하므로 DeckSaveData를 그대로 넘긴다.
    /// </summary>
    public async Task<DeckSaveData> GetDeck(string sessioncode, string role)
    {
        if (dbRef == null) return null;

        DataSnapshot snapshot = await dbRef.Child("sessions").Child(sessioncode)
                                           .Child("decks").Child(role).GetValueAsync();

        if (snapshot == null || !snapshot.Exists) return null;

        string json = snapshot.GetRawJsonValue();
        if (string.IsNullOrEmpty(json)) return null;

        return JsonUtility.FromJson<DeckSaveData>(json);
    }

    public async Task SyncBoardState(string sessioncode, string jsonState)
    {
        // sessions/{sessioncode}/board_state 경로에 JSON 데이터를 통째로 덮어씁니다.
        await dbRef.Child("sessions").Child(sessioncode).Child("board_state").SetRawJsonValueAsync(jsonState);
    }

    /// <summary>
    /// board_state 값이 바뀔 때마다 불린다.
    ///
    /// ★ 해제하려면 돌려받은 토큰을 <see cref="StopListeningBoardState"/>에 넘겨야 한다.
    ///   예전에는 익명 람다를 더하기만 하고 떼어 낼 수단이 없어서,
    ///   씬을 재진입할 때마다 리스너가 눈덩이처럼 불어나 같은 스냅샷을
    ///   여러 번 처리했다(게스트 화면이 느리고 덱이 깜빡이던 원인).
    ///   dbRef가 static이라 씬을 바꿔도 살아남는다.
    /// </summary>
    /// <returns>해제에 쓸 토큰. 필요 없으면 무시해도 된다.</returns>
    public BoardStateListener ListenForBoardState(string sessioncode, Action<string> onStateChanged)
    {
        DatabaseReference reference = dbRef.Child("sessions").Child(sessioncode).Child("board_state");

        EventHandler<ValueChangedEventArgs> handler = (sender, args) =>
        {
            if (args.Snapshot.Exists)
            {
                // 파이어베이스에 저장된 JSON 문자열을 그대로 꺼내서 밖으로 전달해 줍니다.
                string jsonString = args.Snapshot.GetRawJsonValue();
                onStateChanged?.Invoke(jsonString);
            }
        };

        reference.ValueChanged += handler;
        return new BoardStateListener(reference, handler);
    }

    /// <summary>ListenForBoardState가 돌려준 토큰으로 구독을 떼어낸다. null이면 아무 일도 하지 않는다.</summary>
    public void StopListeningBoardState(BoardStateListener listener)
    {
        listener?.Detach();
    }

    /// <summary>board_state 구독 해제용 토큰.</summary>
    public class BoardStateListener
    {
        private DatabaseReference _reference;
        private EventHandler<ValueChangedEventArgs> _handler;

        internal BoardStateListener(DatabaseReference reference, EventHandler<ValueChangedEventArgs> handler)
        {
            _reference = reference;
            _handler = handler;
        }

        internal void Detach()
        {
            if (_reference == null || _handler == null) return;

            _reference.ValueChanged -= _handler;
            _reference = null;
            _handler = null;
        }
    }

    // 1. JSON DTO 보내기
    public async Task SendRequestDTO<T>(string sessioncode, string eventType, T requestObj)
    {
        string jsonPayload = JsonUtility.ToJson(requestObj);
        System.Type requestType = requestObj.GetType();
        string senderName =
            requestType.GetProperty("PlayerName")?.GetValue(requestObj, null)?.ToString()
            ?? requestType.GetField("PlayerName")?.GetValue(requestObj)?.ToString()
            ?? string.Empty;

        //Debug.Log($"[firebase_network] SendRequestDTO eventType={eventType}, sender={senderName}, session={sessioncode}");

        var actionData = new Dictionary<string, object> {
            { "action", eventType },
            { "sender", senderName },
            { "jsonData", jsonPayload }
        };
        await dbRef.Child("sessions").Child(sessioncode).Child("events").Push().SetValueAsync(actionData);
    }

    // 2. JSON DTO 받기 (리스너)
    public void ListenForEventDTO(string sessioncode, Action<string, string, string> onEventReceived)
    {
        StopListeningEvents();
        eventRef = dbRef.Child("sessions").Child(sessioncode).Child("events");
        Debug.Log($"[firebase_network] ListenForEventDTO 등록 session={sessioncode}");
        eventHandler = (sender, args) =>
        {
            if (args.Snapshot.Exists)
            {
                var action = args.Snapshot.Child("action").Value?.ToString();
                var who = args.Snapshot.Child("sender").Value?.ToString();
                var data = args.Snapshot.Child("jsonData").Value?.ToString(); // JSON 데이터 추출

                if (action != null && who != null)
                    onEventReceived?.Invoke(action, who, data);
            }
        };
        eventRef.ChildAdded += eventHandler;
    }
    public void GoOffline() //네트워크 종료 함수
    {
        FirebaseDatabase.DefaultInstance.GoOffline();   
    }

}
