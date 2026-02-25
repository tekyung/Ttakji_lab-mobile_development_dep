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

    public void GoOffline() //네트워크 종료 함수
    {
        FirebaseDatabase.DefaultInstance.GoOffline();   
    }

}
