using System;
using ServerScripts.EventScripts;
using System.Collections;
using System.Threading.Tasks;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;
using TCG_Project.Scripts.Systems;
using TCG_Project.Scripts.Utils;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using static session_manage;
using TCG_Project.Scripts.Interfaces;
public class session_game_manage : MonoBehaviour
{
    public firebase_network networkService;
    public session_ui uiManager;
    private string sessionRoom;
    private string myRole;
    private BoardState latestBoardState;
    // ─── 알림 도착 훅 (UI 연결용) ────────────────────────────────────────
    // 서버가 "카드를 고르라 / 예·아니오로 답하라"고 보낸 알림이 도착했음을 화면 쪽에 알린다.
    // 응답 전송 API(SubmitCardPickFromUI / SendPendingOptionalResponse / SendPendingStackResponse)는
    // 이미 공개돼 있으므로, 이 훅만 있으면 UI가 붙을 수 있다.
    public event System.Action<RequireCardPickNotification> OnCardPickRequested;
    public event System.Action<RequireOptionalNotification> OnOptionalRequested;
    public event System.Action<RequireStackNotification> OnStackRequested;

    private RequireStackNotification pendingStackNotification;
    private RequireOptionalNotification pendingOptionalNotification;
    private RequireCardPickNotification pendingCardPickNotification;
    private RequireCardChoiceNotification pendingCardChoiceNotification;

    public float turn_time_limit = 10f;
    private Coroutine turnTimer;

    private ActionType? pendingAction = null;
    private string pendingStatus = null;

    async void Start()
    {
        await networkService.Initialize();

        sessionRoom = GameData.SessionCode;
        myRole = GameData.MyRole;

        if (GameData.MyDeck != null && GameData.MyDeck.Count > 0)
        {
            DeckSaveData myData = new DeckSaveData();
            myData.cardIdList = GameData.MyDeck;
            myData.characterIdList = GameData.MyCharacters; // 고른 용병도 함께 올린다

            // 2. JSON 문자열로 변환
            string jsonDeck = JsonUtility.ToJson(myData);

            // 3. 파이어베이스로 전송
            await networkService.UploadDeck(sessionRoom, myRole, jsonDeck);
        }
        else
        {
            Debug.LogError("Deck not exitst");
        }
        networkService.ListenForEventDTO(sessionRoom, HandleActionEvent);

        // B. 방장이 최신 전광판(BoardState)을 칠판에 덮어씌웠을 때 (화면 갱신용)
        // ★ 토큰을 보관해 OnDestroy에서 떼어낸다.
        //   예전엔 붙이기만 해서 씬을 재진입할 때마다 리스너가 하나씩 늘었고,
        //   board_state 한 번 쓸 때마다 같은 스냅샷을 여러 번 처리했다.
        _boardListener = networkService.ListenForBoardState(sessionRoom, HandleBoardStateChange);
        // A/B/C 버튼과 수동 턴 넘기기 UI는 현재 룰 흐름에서 사용하지 않으므로 비활성화합니다.
        // networkService.ListenForTurn(sessionRoom, HandleTurnChange);
        // if (myRole == "HOST")
        // {
        //     StartMyTurn();
        //     StartCoroutine(HostGameSetupRoutine());
        // }
        // else if (myRole == "GUEST")
        // {
        //     EndMyTurn();
        // }

        if (myRole == "HOST")
        {
            StartCoroutine(HostGameSetupRoutine());
        }

    }

    /// <summary>board_state 구독 해제용 토큰.</summary>
    private firebase_network.BoardStateListener _boardListener;

    private void OnDestroy()
    {
        if (networkService != null && _boardListener != null)
            networkService.StopListeningBoardState(_boardListener);

        _boardListener = null;
    }

    private async void HandleActionEvent(string actionType, string sender, string jsonData)
    {
        bool shouldLogAction = actionType != "Pass Turn";
        bool isServerNotification =
            actionType == "VisualEventNotification" ||
            actionType.EndsWith("Notification");

        // 임시: Pass Turn 이벤트는 상태창 로그가 너무 시끄러워서 숨깁니다.
        if (shouldLogAction)
            pendingStatus = ($"[{sender}] {actionType} 수신");
        if (shouldLogAction)
            Debug.Log($"[클라:{myRole}] 이벤트 수신 action={actionType}, sender={sender}, hasJson={!string.IsNullOrEmpty(jsonData)}");

        // 내가 방장이라면 클라이언트의 요청만 중앙 처리소로 넘깁니다.
        if (myRole == "HOST" && sender != "ALL" && !isServerNotification)
        {
            if (shouldLogAction)
                Debug.Log($"[클라:{myRole}] HOST 라우터가 이벤트를 서버 처리기로 전달합니다. action={actionType}, sender={sender}");
            await EventService.Instance.ProcessEvent(sessionRoom, actionType, sender, jsonData);
        }

        // Notification의 sender에는 대상 플레이어 이름이 담기므로 내 알림만 처리합니다.
        if (isServerNotification && (sender == myRole || sender == "ALL"))
        {
            HandleServerNotification(actionType, jsonData);
        }
    }

    // 턴 변경 수신 처리
    private void HandleTurnChange(string newTurn)
    {
        // A/B/C 버튼 기반 턴 UI는 현재 사용하지 않습니다.
        // uiManager.ResetButtons();
        // bool isMyTurn = (myRole == newTurn);
        // if (isMyTurn) StartMyTurn();
        // else EndMyTurn();
    }
    private void HandleBoardStateChange(string jsonState)
    {
        // 1. 방장이 파이어베이스에 올린 JSON 문자열을 진짜 BoardState 객체로 변환
        BoardState latestState = JsonUtility.FromJson<BoardState>(jsonState);
        latestBoardState = latestState;

        // 2. 현재 턴인 사람이 '나'인지 확인해서 UI 버튼(조작) 켜고 끄기
        // Set/Open 페이즈는 양측이 동시에 선택해야 하므로 ActivePlayer 기반 게이팅을 해제합니다.
        bool isSetOrOpenPhase =
            latestState.CurrentPhase == GamePhase.SetPhase.ToString() ||
            latestState.CurrentPhase == GamePhase.OpenPhase.ToString();

        bool isMyTurn = isSetOrOpenPhase || (latestState.ActivePlayer == myRole);
        //uiManager.SetActionButtonsState(isMyTurn);
        uiManager.UpdateBoardStateUI(myRole, latestState);

        int myResourceCount = (myRole == "HOST") ? latestState.HostState.ResourceZoneCount : latestState.GuestState.ResourceZoneCount;

        pendingStatus = $"[페이즈 갱신] 턴: {latestState.CurrentTurn} / 현재 턴: {latestState.ActivePlayer}";

        Debug.Log(
            $"[BoardSync][{myRole}] turn={latestState.CurrentTurn}, phase={latestState.CurrentPhase}, active={latestState.ActivePlayer}, myTurn={isMyTurn}");

        Debug.Log(
            $"[BoardSync-Res][{myRole}] " +
            $"HOST(resDeck={latestState.HostState.ResourceDeckCount}, resZone={latestState.HostState.ResourceZoneCount}, hand={latestState.HostState.HandCount}, deck={latestState.HostState.DeckCount}) | " +
            $"GUEST(resDeck={latestState.GuestState.ResourceDeckCount}, resZone={latestState.GuestState.ResourceZoneCount}, hand={latestState.GuestState.HandCount}, deck={latestState.GuestState.DeckCount}) | " +
            $"myResZone={myResourceCount}");
    }

    public void Test_RequestSetFirstHandCard()
    {
        Test_RequestSetHandCardByIndex(0);
    }

    public void Test_RequestSetSecondHandCard()
    {
        Test_RequestSetHandCardByIndex(1);
    }
    public void Test_RequestSetThirdHandCard()
    {
        Test_RequestSetHandCardByIndex(2);
    }
    public void Test_RequestSetFourthHandCard()
    {
        Test_RequestSetHandCardByIndex(3);
    }
    public void Test_RequestSetFifthHandCard()
    {
        Test_RequestSetHandCardByIndex(4);
    }
    public void Test_RequestSetSixthHandCard()
    {
        Test_RequestSetHandCardByIndex(5);
    }

    public void Test_RequestSetHandCardByIndex(int handIndex)
    {
        if (latestBoardState == null)
        {
            Debug.LogWarning("[테스트] 아직 board_state를 받지 못했습니다.");
            return;
        }

        if (latestBoardState.CurrentPhase != GamePhase.SetPhase.ToString())
        {
            Debug.LogWarning($"[테스트] 현재 페이즈가 SetPhase가 아닙니다. ({latestBoardState.CurrentPhase})");
            return;
        }

        PlayerState myState = GetMyPlayerState();
        if (myState == null || myState.HandCardInstanceIds == null || myState.HandCardInstanceIds.Length == 0)
        {
            Debug.LogWarning("[테스트] 현재 손패 정보가 없습니다.");
            return;
        }

        if (handIndex < 0 || handIndex >= myState.HandCardInstanceIds.Length)
        {
            Debug.LogWarning($"[테스트] 손패 인덱스가 범위를 벗어났습니다. index={handIndex}, handCount={myState.HandCardInstanceIds.Length}");
            return;
        }

        string selectedInstanceId = myState.HandCardInstanceIds[handIndex];
        string selectedDataId =
            myState.HandCardDataIds != null && handIndex < myState.HandCardDataIds.Length
                ? myState.HandCardDataIds[handIndex]
                : "UNKNOWN";

        Debug.Log($"[테스트] SetPhase 카드 제출: role={myRole}, index={handIndex}, dataId={selectedDataId}, instanceId={selectedInstanceId}");
        SendSetPhaseChoice(selectedInstanceId, true);
    }

    [ContextMenu("Test/Open Selected Set Card")]
    public void Test_RequestOpenSelectedSetCard()
    {
        Test_RequestOpenPhaseChoice("Open");
    }

    [ContextMenu("Test/Abandon Selected Set Card")]
    public void Test_RequestAbandonSelectedSetCard()
    {
        Test_RequestOpenPhaseChoice("Abandon");
    }

    public void Test_RequestOpenPhaseChoice(string choiceString)
    {
        if (latestBoardState == null)
        {
            Debug.LogWarning("[테스트] 아직 board_state를 받지 못했습니다.");
            return;
        }

        if (latestBoardState.CurrentPhase != GamePhase.OpenPhase.ToString())
        {
            Debug.LogWarning($"[테스트] 현재 페이즈가 OpenPhase가 아닙니다. ({latestBoardState.CurrentPhase})");
            return;
        }

        CardState mySetCard = GetMySetZoneCardState();
        if (mySetCard == null || string.IsNullOrEmpty(mySetCard.InstanceId))
        {
            Debug.LogWarning("[테스트] 현재 내 SetZone 카드 정보를 찾을 수 없습니다.");
            return;
        }

        Debug.Log(
            $"[테스트] OpenPhase 선택 전송: role={myRole}, choice={choiceString}, " +
            $"setInstanceId={mySetCard.InstanceId}, dataId={mySetCard.CardDataId}");

        SendOpenPhaseChoice(mySetCard.InstanceId, choiceString);
    }

    private PlayerState GetMyPlayerState()
    {
        if (latestBoardState == null) return null;
        return myRole == "HOST" ? latestBoardState.HostState : latestBoardState.GuestState;
    }

    private CardState GetMySetZoneCardState()
    {
        if (latestBoardState == null || latestBoardState.FieldCards == null) return null;

        foreach (var cardState in latestBoardState.FieldCards)
        {
            if (cardState == null) continue;
            if (cardState.OwnerRole != myRole) continue;
            if (cardState.Zone != "SetZone") continue;
            return cardState;
        }

        return null;
    }

    private void StartMyTurn()
    {
        // A/B/C 버튼과 턴 타이머는 현재 룰 흐름에서 사용하지 않습니다.
        // uiManager.SetActionButtonsState(true);
        // turnTimer = StartCoroutine(TurnTimeoutRoutine());
    }

    // 내 턴 종료
    private void EndMyTurn()
    {
        // A/B/C 버튼과 턴 타이머는 현재 룰 흐름에서 사용하지 않습니다.
        // uiManager.SetActionButtonsState(false);
        // if (turnTimer != null)
        // {
        //     StopCoroutine(turnTimer);
        // }
    }
    private IEnumerator TurnTimeoutRoutine()
    {
        // A/B/C 버튼 기반 타이머 턴 종료는 현재 사용하지 않습니다.
        // float timer = turn_time_limit;
        // while (timer > 0)
        // {
        //     timer -= Time.deltaTime;
        //     yield return null;
        // }
        // OnBtnClick_C();
        yield break;
    }
    public async void RequestPlayCard(string cardId)
    {
        var req = new PlayCardRequest
        {
            MatchId = GameData.SessionCode, //
            PlayerName = GameData.MyRole,    //
            CardInstanceId = cardId         //
        };

        //  객체를 텍스트(JSON)로 변환
        string json = JsonUtility.ToJson(req);

        // 파이어베이스 엔진을 통해 전송
        await networkService.SendAction(GameData.SessionCode, "PlayCardRequest", json);
    }

    public void OnBtnClick_A()
    {
        // if (string.IsNullOrEmpty(sessionRoom))
        // {
        //     return;
        // }
        // if (turnTimer != null) StopCoroutine(turnTimer);
        // turnTimer = StartCoroutine(TurnTimeoutRoutine());
        // await networkService.SendAction(sessionRoom, ActionType.A.ToString(), myRole);
        Debug.Log("[세션] A 버튼은 현재 룰 흐름에서 사용하지 않습니다.");
    }

    public void OnBtnClick_B()
    {
        // if (string.IsNullOrEmpty(sessionRoom))
        // {
        //     return;
        // }
        // if (turnTimer != null) StopCoroutine(turnTimer);
        // turnTimer = StartCoroutine(TurnTimeoutRoutine());
        // await networkService.SendAction(sessionRoom, ActionType.B.ToString(), myRole);
        Debug.Log("[세션] B 버튼은 현재 룰 흐름에서 사용하지 않습니다.");
    }

    public void OnBtnClick_C() // 턴 넘기기
    {
        // if (string.IsNullOrEmpty(sessionRoom))
        // {
        //     return;
        // }
        // if (turnTimer != null) StopCoroutine(turnTimer);
        // uiManager.SetActionButtonsState(false);
        // string nextTurn = (myRole == "HOST") ? "GUEST" : "HOST";
        // await networkService.SendAction(sessionRoom, "Pass Turn", myRole);
        // await networkService.ChangeTurn(sessionRoom, nextTurn);
        Debug.Log("[세션] 수동 턴 넘기기는 현재 룰 흐름에서 사용하지 않습니다.");
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
            // A/B/C 버튼 강조 표시는 현재 사용하지 않습니다.
            // uiManager.CheckButton(pendingAction.Value);
            pendingAction = null;
        }
    }


    /// <summary>테스트 2: 턴 종료 요청 (합법적인 절차)</summary>
    public async void Test_RequestTurnEnd()
    {
        // 1. 편지(DTO) 작성
        TurnEndRequest req = new TurnEndRequest
        {
            MatchId = sessionRoom,
            PlayerName = myRole
        };

        // 2. JSON 압축 및 전송
        string json = JsonUtility.ToJson(req);
        await networkService.SendAction(sessionRoom, "TurnEndRequest", json);

        Debug.Log($"[클라이언트] 턴 종료 요청 보냄: {json}");
    }
    private void HandleServerNotification(string actionType, string jsonData)
    {
        switch (actionType)
        {
            case "RequireSetPhaseNotification":
                var setNoti = JsonUtility.FromJson<RequireSetPhaseNotification>(jsonData);
                uiManager.UpdateStatus(setNoti.Message); // "세트할 카드를 고르세요"
                // 프로토타입: 유저가 카드를 누를 수 있게 내 손패 조작 활성화
                //uiManager.SetActionButtonsState(true);
                break;

            case "RequireOpenPhaseNotification":
                var openNoti = JsonUtility.FromJson<RequireOpenPhaseNotification>(jsonData);
                uiManager.UpdateStatus(openNoti.Message); // "공개하시겠습니까?"
                // TODO: 유니티 씬에 대충 만든 [공개] / [폐기] 버튼 2개를 화면에 보이게 켜줍니다.
                // 예: OpenPhasePanel.SetActive(true);
                break;

            case "RequireStackNotification":
                var stackNoti = JsonUtility.FromJson<RequireStackNotification>(jsonData);
                pendingStackNotification = stackNoti;
                uiManager.UpdateStatus(stackNoti.Message); // "스택 방어 발동할까요?"
                OnStackRequested?.Invoke(stackNoti);
                Debug.Log(
                    $"[클라:{myRole}] Stack 알림 수신 stackCardId={stackNoti.StackCardInstanceId}, " +
                    $"stackDataId={stackNoti.StackCardDataId}, opponentCardId={stackNoti.OpponentCardInstanceId}");

                break;

            case "RequireOptionalNotification":
                var optionalNoti = JsonUtility.FromJson<RequireOptionalNotification>(jsonData);
                pendingOptionalNotification = optionalNoti;
                uiManager.UpdateStatus(optionalNoti.Message);
                OnOptionalRequested?.Invoke(optionalNoti);
                Debug.Log(
                    $"[클라:{myRole}] Optional 알림 수신 actionId={optionalNoti.ActionId}, message={optionalNoti.Message}");
                break;

            case "RequireCardPickNotification":
                var pickNoti = JsonUtility.FromJson<RequireCardPickNotification>(jsonData);
                pendingCardPickNotification = pickNoti;
                uiManager.UpdateStatus(pickNoti.Message);
                OnCardPickRequested?.Invoke(pickNoti);
                Debug.Log(
                    $"[클라:{myRole}] CardPick 알림 수신 requestId={pickNoti.RequestId}, required={pickNoti.RequiredCount}, " +
                    $"candidates={(pickNoti.PresentedCardInstanceIds != null ? string.Join(", ", pickNoti.PresentedCardInstanceIds) : "none")}");
                break;

            case "RequireCardChoiceNotification":
                var choiceNoti = JsonUtility.FromJson<RequireCardChoiceNotification>(jsonData);
                pendingCardChoiceNotification = choiceNoti;
                uiManager.UpdateStatus(choiceNoti.Message);
                Debug.Log(
                    $"[클라:{myRole}] CardChoice 알림 수신 requestId={choiceNoti.RequestId}, zone={choiceNoti.Zone}, required={choiceNoti.RequiredCount}, " +
                    $"candidates={(choiceNoti.PresentedCardInstanceIds != null ? string.Join(", ", choiceNoti.PresentedCardInstanceIds) : "none")}");
                break;

            case "LifeChangeNotification":
                var lifeNoti = JsonUtility.FromJson<LifeChangeNotification>(jsonData);
                HandleLifeChangeNotification(lifeNoti);
                break;

            case "VisualEventNotification":
                var visualNoti = JsonUtility.FromJson<VisualEventNotification>(jsonData);
                HandleVisualEventNotification(visualNoti);
                break;
        }
    }

    private void HandleLifeChangeNotification(LifeChangeNotification lifeNoti)
    {
        if (lifeNoti == null)
        {
            Debug.LogWarning($"[클라:{myRole}] LifeChangeNotification 파싱 실패");
            return;
        }

        string deltaText = lifeNoti.Delta > 0
            ? $"+{lifeNoti.Delta}"
            : lifeNoti.Delta < 0
                ? lifeNoti.Delta.ToString()
                : "0";

        string statusText = $"[Life] {lifeNoti.TargetPlayerName} -> {lifeNoti.NewLife} ({deltaText})";
        pendingStatus = statusText;
        Debug.Log($"[클라:{myRole}] {statusText}, reason={lifeNoti.Reason}");
    }

    private void HandleVisualEventNotification(VisualEventNotification visualNoti)
    {
        if (visualNoti == null)
        {
            Debug.LogWarning($"[클라:{myRole}] VisualEventNotification 파싱 실패");
            return;
        }

        Debug.Log(
            $"[클라:{myRole}] 연출 수신 type={visualNoti.EventType}, owner={visualNoti.OwnerRole}, " +
            $"instance={visualNoti.CardInstanceId}, data={visualNoti.CardDataId}, from={visualNoti.FromZone}, to={visualNoti.ToZone}");

        // HOST는 같은 프로세스의 서버 EventManager 이벤트를 직접 받으므로 중복 재생을 막습니다.
        if (myRole == "HOST")
            return;

        Player owner = BuildVisualPlayer(visualNoti.OwnerRole);
        Card card = BuildVisualCard(visualNoti);

        switch (visualNoti.EventType)
        {
            case "CardMove":
                if (TryParseZoneType(visualNoti.FromZone, out ZoneType fromZone) &&
                    TryParseZoneType(visualNoti.ToZone, out ZoneType toZone))
                {
                    EventManager.OnCardMove?.Invoke(card, owner, fromZone, owner, toZone);
                }
                break;

            case "CardDraw":
                if (TryParseZoneType(visualNoti.FromZone, out ZoneType drawFromZone))
                {
                    EventManager.OnCardDraw?.Invoke(card, owner, drawFromZone);
                }
                break;

            case "PlayCard":
                EventManager.OnPlayCard?.Invoke(owner, card);
                break;

            default:
                Debug.Log($"[클라:{myRole}] 미처리 연출 타입: {visualNoti.EventType}");
                break;
        }
    }

    private Player BuildVisualPlayer(string ownerRole)
    {
        string role = string.IsNullOrEmpty(ownerRole) ? "UNKNOWN" : ownerRole;
        UserType type = role == myRole ? UserType.Human : UserType.Bot;

        return new Player
        {
            Name = role,
            Type = type
        };
    }

    private Card BuildVisualCard(VisualEventNotification visualNoti)
    {
        string cardDataId = !string.IsNullOrEmpty(visualNoti.CardDataId)
            ? visualNoti.CardDataId
            : visualNoti.CardInstanceId;

        return new Card
        {
            InstanceId = visualNoti.CardInstanceId,
            DataId = cardDataId,
            Id = cardDataId,
            Name = cardDataId
        };
    }

    private bool TryParseZoneType(string zoneText, out ZoneType zone)
    {
        zone = default;
        return !string.IsNullOrEmpty(zoneText) && System.Enum.TryParse(zoneText, out zone);
    }

    // =======================================================
    // 3. UI 버튼 클릭 시 서버로 대답 발송 (유니티 버튼의 OnClick에 연결)
    // =======================================================

    // 세트할 카드를 눌렀을 때 호출
    public async void SendSetPhaseChoice(string selectedCardId, bool isConfirmed)
    {
        Debug.Log(
            $"[클라:{myRole}] Set 요청 전송 시작 session={sessionRoom}, " +
            $"instanceId={selectedCardId}, confirmed={isConfirmed}, " +
            $"phase={(latestBoardState != null ? latestBoardState.CurrentPhase : "unknown")}");

        var req = new SetPhaseActionRequest
        {
            MatchId = sessionRoom,
            PlayerName = myRole,
            PickedCardInstanceId = selectedCardId,
            IsConfirmed = isConfirmed
        };
        await networkService.SendRequestDTO(sessionRoom, "SetPhaseActionRequest", req);
        Debug.Log($"[클라:{myRole}] Set 요청 전송 완료 instanceId={selectedCardId}");
    }

    // [공개] 또는 [폐기] 버튼을 눌렀을 때 호출 (choiceString: "Open" 또는 "Abandon")
    public async void SendOpenPhaseChoice(string setCardId, string choiceString)
    {
        Debug.Log(
            $"[클라:{myRole}] Open 요청 전송 시작 session={sessionRoom}, " +
            $"setCardId={setCardId}, choice={choiceString}, " +
            $"phase={(latestBoardState != null ? latestBoardState.CurrentPhase : "unknown")}");

        var req = new OpenPhaseActionRequest
        {
            MatchId = sessionRoom,
            PlayerName = myRole,
            SetCardInstanceId = setCardId,
            Choice = choiceString
        };
        await networkService.SendRequestDTO(sessionRoom, "OpenPhaseActionRequest", req);
        Debug.Log($"[클라:{myRole}] Open 요청 전송 완료 setCardId={setCardId}, choice={choiceString}");

        // 버튼 누른 뒤엔 팝업창 닫기
        // 예: OpenPhasePanel.SetActive(false);
    }

    // 스택 [발동] 또는 [취소] 버튼을 눌렀을 때 호출 (isUsing: true/false)
    public async void SendStackResponse(string myStackCardId, string opponentCardId, bool isUsing)
    {
        Debug.Log(
            $"[클라:{myRole}] Stack 응답 전송 시작 session={sessionRoom}, " +
            $"stackCardId={myStackCardId}, opponentCardId={opponentCardId}, use={isUsing}, " +
            $"phase={(latestBoardState != null ? latestBoardState.CurrentPhase : "unknown")}");

        var req = new StackResponseRequest
        {
            MatchId = sessionRoom,
            PlayerName = myRole,
            StackCardInstanceId = myStackCardId,
            OpponentCardInstanceId = opponentCardId,
            IsUsingStack = isUsing
        };
        await networkService.SendRequestDTO(sessionRoom, "StackResponseRequest", req);
        pendingStackNotification = null;
        Debug.Log($"[클라:{myRole}] Stack 응답 전송 완료 use={isUsing}");
    }

    public async void SendOptionalActionResponse(string actionId, bool choice)
    {
        Debug.Log(
            $"[클라:{myRole}] Optional 응답 전송 시작 session={sessionRoom}, " +
            $"actionId={actionId}, choice={choice}, phase={(latestBoardState != null ? latestBoardState.CurrentPhase : "unknown")}");

        var req = new OptionalActionRequest
        {
            MatchId = sessionRoom,
            PlayerName = myRole,
            ActionId = actionId,
            Choice = choice
        };

        await networkService.SendRequestDTO(sessionRoom, "OptionalActionRequest", req);
        pendingOptionalNotification = null;
        Debug.Log($"[클라:{myRole}] Optional 응답 전송 완료 choice={choice}");
    }

    public async void SendCardPickResponse(string requestId, string[] pickedCardInstanceIds)
    {
        Debug.Log(
            $"[클라:{myRole}] CardPick 응답 전송 시작 session={sessionRoom}, requestId={requestId}, " +
            $"picked={(pickedCardInstanceIds != null ? string.Join(", ", pickedCardInstanceIds) : "none")}, " +
            $"phase={(latestBoardState != null ? latestBoardState.CurrentPhase : "unknown")}");

        var req = new CardPickRequest
        {
            MatchId = sessionRoom,
            PlayerName = myRole,
            RequestId = requestId,
            PickedCardInstanceIds = pickedCardInstanceIds ?? Array.Empty<string>()
        };

        await networkService.SendRequestDTO(sessionRoom, "CardPickRequest", req);
        pendingCardPickNotification = null;
        Debug.Log($"[클라:{myRole}] CardPick 응답 전송 완료 requestId={requestId}");
    }

    public RequireCardPickNotification GetPendingCardPickNotification() => pendingCardPickNotification;

    /// <summary>
    /// 항복을 호스트에 알린다 (게스트 전용).
    /// 새 DTO를 만들지 않고 기존 GameSetRequest를 그대로 쓴다 — 상대를 승자로 지명한다.
    /// </summary>
    public void SendSurrender()
    {
        string myRole = string.IsNullOrEmpty(GameData.MyRole) ? "GUEST" : GameData.MyRole;
        string opponent = myRole == "HOST" ? "GUEST" : "HOST";

        var req = new GameSetRequest
        {
            MatchId = GameData.SessionCode,
            PlayerName = myRole,
            WinnerName = opponent
        };

        Debug.Log($"[게스트] 항복 전송 — 승자={opponent}");
        _ = networkService.SendRequestDTO(GameData.SessionCode, "GameSetRequest", req);
    }

    public void SubmitCardPickFromUI(string[] pickedInstanceIds)
    {
        if (pendingCardPickNotification == null)
        {
            Debug.LogWarning("[session_game_manage] 대기 중인 CardPick이 없습니다.");
            return;
        }

        int required = pendingCardPickNotification.RequiredCount;
        if (pickedInstanceIds == null || pickedInstanceIds.Length != required)
        {
            Debug.LogWarning(
                $"[session_game_manage] 선택 장수 불일치. required={required}, actual={pickedInstanceIds?.Length ?? 0}");
            return;
        }

        var allowed = new HashSet<string>(pendingCardPickNotification.PresentedCardInstanceIds ?? Array.Empty<string>());
        foreach (string id in pickedInstanceIds)
        {
            if (string.IsNullOrEmpty(id) || !allowed.Contains(id))
            {
                Debug.LogWarning($"[session_game_manage] 후보에 없는 instanceId: {id}");
                return;
            }
        }

        SendCardPickResponse(pendingCardPickNotification.RequestId, pickedInstanceIds);
    }

    public void OnCardPickConfirmDefaultFromUI()
    {
        SendPendingCardPickResponse(0);
    }

    public async void SendCardChoiceResponse(string requestId, string zone, string[] pickedCardInstanceIds)
    {
        Debug.Log(
            $"[클라:{myRole}] CardChoice 응답 전송 시작 session={sessionRoom}, requestId={requestId}, zone={zone}, " +
            $"picked={(pickedCardInstanceIds != null ? string.Join(", ", pickedCardInstanceIds) : "none")}, " +
            $"phase={(latestBoardState != null ? latestBoardState.CurrentPhase : "unknown")}");

        var req = new CardChoiceRequest
        {
            MatchId = sessionRoom,
            PlayerName = myRole,
            RequestId = requestId,
            PickedCardInstanceIds = pickedCardInstanceIds ?? Array.Empty<string>(),
            Zone = zone
        };

        await networkService.SendRequestDTO(sessionRoom, "CardChoiceRequest", req);
        pendingCardChoiceNotification = null;
        Debug.Log($"[클라:{myRole}] CardChoice 응답 전송 완료 requestId={requestId}");
    }

    [ContextMenu("Test/Accept Pending Optional Action")]
    public void Test_AcceptPendingOptionalAction()
    {
        SendPendingOptionalResponse(true);
    }

    [ContextMenu("Test/Decline Pending Optional Action")]
    public void Test_DeclinePendingOptionalAction()
    {
        SendPendingOptionalResponse(false);
    }

    public void SendPendingOptionalResponse(bool choice)
    {
        if (pendingOptionalNotification == null)
        {
            Debug.LogWarning("[테스트] 현재 대기 중인 선택적 행동 알림이 없습니다.");
            return;
        }

        SendOptionalActionResponse(pendingOptionalNotification.ActionId, choice);
    }

    [ContextMenu("Test/Choose First Pending Card Pick")]
    public void Test_ChooseFirstPendingCardPick()
    {
        SendPendingCardPickResponse(0);
    }

    public void SendPendingCardPickResponse(int startIndex = 0)
    {
        if (pendingCardPickNotification == null)
        {
            Debug.LogWarning("[테스트] 현재 대기 중인 카드 픽 알림이 없습니다.");
            return;
        }

        if (pendingCardPickNotification.PresentedCardInstanceIds == null ||
            pendingCardPickNotification.PresentedCardInstanceIds.Length == 0)
        {
            Debug.LogWarning("[테스트] 카드 픽 후보가 비어 있습니다.");
            return;
        }

        int requiredCount = Mathf.Max(1, pendingCardPickNotification.RequiredCount);
        string[] pickedIds = pendingCardPickNotification.PresentedCardInstanceIds
            .Skip(Mathf.Max(0, startIndex))
            .Take(requiredCount)
            .ToArray();

        if (pickedIds.Length == 0)
        {
            Debug.LogWarning("[테스트] 선택 가능한 카드가 부족합니다.");
            return;
        }

        SendCardPickResponse(pendingCardPickNotification.RequestId, pickedIds);
    }

    [ContextMenu("Test/Choose First Pending Card Choice")]
    public void Test_ChooseFirstPendingCardChoice()
    {
        SendPendingCardChoiceResponse(0);
    }

    public void SendPendingCardChoiceResponse(int startIndex = 0)
    {
        if (pendingCardChoiceNotification == null)
        {
            Debug.LogWarning("[테스트] 현재 대기 중인 카드 초이스 알림이 없습니다.");
            return;
        }

        if (pendingCardChoiceNotification.PresentedCardInstanceIds == null ||
            pendingCardChoiceNotification.PresentedCardInstanceIds.Length == 0)
        {
            Debug.LogWarning("[테스트] 카드 초이스 후보가 비어 있습니다.");
            return;
        }

        int requiredCount = Mathf.Max(1, pendingCardChoiceNotification.RequiredCount);
        string[] pickedIds = pendingCardChoiceNotification.PresentedCardInstanceIds
            .Skip(Mathf.Max(0, startIndex))
            .Take(requiredCount)
            .ToArray();

        if (pickedIds.Length == 0)
        {
            Debug.LogWarning("[테스트] 선택 가능한 카드가 부족합니다.");
            return;
        }

        SendCardChoiceResponse(
            pendingCardChoiceNotification.RequestId,
            pendingCardChoiceNotification.Zone,
            pickedIds);
    }

    [ContextMenu("Test/Use Pending Stack Card")]
    public void Test_UsePendingStackCard()
    {
        SendPendingStackResponse(true);
    }

    [ContextMenu("Test/Decline Pending Stack Card")]
    public void Test_DeclinePendingStackCard()
    {
        SendPendingStackResponse(false);
    }

    public void SendPendingStackResponse(bool isUsing)
    {
        if (pendingStackNotification == null)
        {
            Debug.LogWarning("[테스트] 현재 대기 중인 스택 알림이 없습니다.");
            return;
        }

        SendStackResponse(
            pendingStackNotification.StackCardInstanceId,
            pendingStackNotification.OpponentCardInstanceId,
            isUsing);
    }
    // 매칭된 두 사람이 각자 고른 덱을 올릴 때까지 기다리는 한도
    private const float DeckWaitTimeoutSeconds = 15f;
    private const float DeckPollIntervalSeconds = 0.5f;

    /// <summary>폴백용 기본 덱 (업로드된 덱을 못 읽었을 때만 쓴다). 유니크 10종 → 2장씩 20장</summary>
    private static readonly string[] FallbackHostDeckIds =
    {
        "ELLI-02","ELLI-03","ELLI-04","ELLI-05","ELLI-06","ELLI-07",
        "DAIN-02","DAIN-03","DAIN-07","DAIN-09"
    };
    private static readonly string[] FallbackGuestDeckIds =
    {
        "VERO-02","VERO-03","VERO-05","VERO-07","VERO-11",
        "SONI-02","SONI-03","SONI-05","SONI-06","SONI-07"
    };

    private IEnumerator HostGameSetupRoutine()
    {
        // 서버 로직 키는 고정(HOST/GUEST)이어야 검증/동기화가 안전합니다.

        Player p1 = new Player { Name = "HOST", Type = UserType.Human };
        Player p2 = new Player { Name = "GUEST", Type = UserType.Human };

        // ConsoleRunner와 동일한 방식으로 카드 데이터를 로드해 덱을 생성합니다.
        GameDataManager dataManager = BuildServerDataManager();

        // ★ 양쪽이 각자 고른 덱을 올릴 때까지 기다린다.
        //   예전에는 WaitForSeconds(0.5f) 한 번이었는데, 상대 업로드가 조금만 늦어도 그대로 실패했다.
        DeckSaveData hostDeck = null;
        DeckSaveData guestDeck = null;
        float waited = 0f;

        while (waited < DeckWaitTimeoutSeconds && (hostDeck == null || guestDeck == null))
        {
            if (hostDeck == null)
            {
                var hostTask = networkService.GetDeck(sessionRoom, "HOST");
                yield return new WaitUntil(() => hostTask.IsCompleted);
                if (hostTask.Status == TaskStatus.RanToCompletion) hostDeck = hostTask.Result;
            }

            if (guestDeck == null)
            {
                var guestTask = networkService.GetDeck(sessionRoom, "GUEST");
                yield return new WaitUntil(() => guestTask.IsCompleted);
                if (guestTask.Status == TaskStatus.RanToCompletion) guestDeck = guestTask.Result;
            }

            if (hostDeck != null && guestDeck != null) break;

            yield return new WaitForSeconds(DeckPollIntervalSeconds);
            waited += DeckPollIntervalSeconds;
        }

        List<Card> p1MainDeck = BuildMainDeck(dataManager, hostDeck?.cardIdList, FallbackHostDeckIds, "HOST");
        List<Card> p2MainDeck = BuildMainDeck(dataManager, guestDeck?.cardIdList, FallbackGuestDeckIds, "GUEST");
        List<Card> p1ResourceDeck = CreateResourceDeckFromDataManager(dataManager);
        List<Card> p2ResourceDeck = CreateResourceDeckFromDataManager(dataManager);

        p1.ResetForNewGame(p1MainDeck, p1ResourceDeck);
        p2.ResetForNewGame(p2MainDeck, p2ResourceDeck);

        // ★ 덱에 담긴 카드로 용병 2종을 정한다.
        //   이걸 넣지 않으면 CharacterAbilityRegistry가 빈 목록을 돌려줘 용병 능력이 통째로 죽는다.
        ApplyCharactersFromDeck(p1, p1MainDeck, dataManager, hostDeck?.characterIdList);
        ApplyCharactersFromDeck(p2, p2MainDeck, dataManager, guestDeck?.characterIdList);

        Debug.Log($"[서버 초기화] HOST main={p1.Deck.Count}, resource={p1.ResourceDeck.Count}, 용병={p1.CharacterCardId}/{p1.SecondaryCharacterId}");
        Debug.Log($"[서버 초기화] GUEST main={p2.Deck.Count}, resource={p2.ResourceDeck.Count}, 용병={p2.CharacterCardId}/{p2.SecondaryCharacterId}");

        if (ServerGameManager.Instance != null && EventService.Instance != null)
        {
            // 2. 게임판(GameContext) 생성!
            ServerGameManager.Instance.StartMultiplayerGame(p1, p2);

            // ★ 용병 슬롯 4칸 방송 (BattleManager도 매치 초기화에서 같은 두 줄을 부른다)
            //   이게 없으면 용병 카드가 화면에 아예 그려지지 않는다.
            CharacterFieldBroadcast.Register(p1, dataManager);
            CharacterFieldBroadcast.SyncAll(p1, p2, dataManager);

            // 3. EventService에 게임판(context) 등록! (이제 null이 아닙니다)
            EventService.Instance.InitializeGame(ServerGameManager.Instance.context, p1, p2);

            //  4. 파이어베이스에 첫 전광판(board_state) 덮어쓰기! 
            _ = EventService.Instance.SyncGameStateToFirebase(sessionRoom);

            Debug.Log("[서버] 세팅 완료! 파이어베이스에 board_state가 생성됩니다!");
        }
        else
        {
            Debug.LogError(" ServerGameManager 또는 EventService가 씬에 없습니다!");
        }
    }

    /// <summary>
    /// 업로드된 덱 ID 목록으로 메인덱을 만든다. 실패하면 폴백 덱으로 대체한다.
    ///
    /// 두 가지 형태가 들어올 수 있다:
    ///  · 덱 빌더가 저장한 <b>중복 포함 20장</b> → 그대로 1:1로 만든다
    ///  · 콘솔·구버전 방식의 <b>유니크 10종</b> → 2장씩 전개한다(CreateDeckFromIds)
    /// 둘을 구분하지 않으면 20장짜리를 40장으로 부풀리게 된다.
    /// </summary>
    private List<Card> BuildMainDeck(GameDataManager manager, List<string> uploadedIds, string[] fallbackIds, string role)
    {
        const int RequiredDeckSize = 20;

        if (uploadedIds != null && uploadedIds.Count > 0)
        {
            List<Card> deck = uploadedIds.Count >= RequiredDeckSize
                ? CreateDeckFromExactIds(manager, uploadedIds)   // 중복 포함 목록
                : CreateDeckFromIds(manager, uploadedIds);       // 유니크 목록 → 2장씩

            if (deck.Count == RequiredDeckSize)
            {
                Debug.Log($"[서버 초기화] {role} 업로드 덱 사용 ({uploadedIds.Count}개 항목 → {deck.Count}장)");
                return deck;
            }

            Debug.LogWarning($"[서버 초기화] {role} 업로드 덱이 {deck.Count}장이라 쓸 수 없다. 기본 덱으로 대체한다.");
        }
        else
        {
            Debug.LogWarning($"[서버 초기화] {role} 덱을 읽지 못했다(업로드 지연 또는 미선택). 기본 덱으로 대체한다.");
        }

        return CreateDeckFromIds(manager, fallbackIds);
    }

    /// <summary>ID 목록을 1:1로 카드로 만든다 (중복 포함 목록용).</summary>
    private List<Card> CreateDeckFromExactIds(GameDataManager manager, IEnumerable<string> ids)
    {
        var deck = new List<Card>();
        if (manager == null || ids == null) return deck;

        foreach (var id in ids)
        {
            if (string.IsNullOrEmpty(id)) continue;
            if (!manager.AllCards.TryGetValue(id, out Card template)) continue;

            deck.Add(template.Clone());
        }

        return deck;
    }

    /// <summary>
    /// 덱에 담긴 카드로부터 용병 2종을 역산해 플레이어에 주입한다.
    /// 카드의 characterId("ELLIE")와 카드 ID 접두사("ELLI")가 다르므로
    /// 문자열을 직접 조립하지 말고 GameDataManager.TryResolveCharacterCardId만 쓴다.
    /// </summary>
    private void ApplyCharactersFromDeck(
        Player player, List<Card> deck, GameDataManager manager, List<string> declaredCharacters = null)
    {
        if (player == null || deck == null || manager == null) return;

        var characterCardIds = new List<string>();

        // ★ 덱 빌더가 명시한 용병이 있으면 그걸 먼저 쓴다.
        //   카드에서 역산하면 "용병 2명을 골랐지만 한쪽 카드만 넣은 덱"이 1명으로 읽힌다.
        if (declaredCharacters != null)
        {
            foreach (string declared in declaredCharacters)
            {
                if (string.IsNullOrEmpty(declared)) continue;
                if (!manager.TryResolveCharacterCardId(declared, out string declaredCardId)) continue;
                if (characterCardIds.Contains(declaredCardId)) continue;

                characterCardIds.Add(declaredCardId);
                if (characterCardIds.Count >= 2) break;
            }
        }

        // 구버전 덱(명시값 없음)이거나 모자라면 카드에서 채운다
        foreach (Card card in deck)
        {
            if (card == null) continue;

            string key = !string.IsNullOrEmpty(card.CharacterId) ? card.CharacterId : card.Id;
            if (!manager.TryResolveCharacterCardId(key, out string characterCardId)) continue;
            if (characterCardIds.Contains(characterCardId)) continue;

            characterCardIds.Add(characterCardId);
            if (characterCardIds.Count >= 2) break; // 룰북상 용병은 2종
        }

        player.CharacterCardId = characterCardIds.Count > 0 ? characterCardIds[0] : null;
        player.SecondaryCharacterId = characterCardIds.Count > 1 ? characterCardIds[1] : null;

        if (characterCardIds.Count == 0)
            Debug.LogWarning($"[서버 초기화] {player.Name}: 덱에서 용병을 찾지 못했다. 용병 능력이 발동하지 않는다.");
    }

    private GameDataManager BuildServerDataManager()
    {
        IJsonLoader loader = new UnityResourceLoader("GameData");
        var manager = new GameDataManager(loader);
        //var manager = new GameDataManager(new UnityResourceLoader("GameData"));

        try
        {
            GameRules.LoadRules(loader, "CommonConfig");
            manager.LoadRulebookCards();
            manager.LoadCharacterCards();
            manager.LoadResourceCards( );
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[서버 초기화] 카드 데이터 로드 실패: {e.Message}");
        }

        return manager;
    }

    // ConsoleRunner의 CreateDeckFromIds 로직과 동일: ID당 2장 복제
    private List<Card> CreateDeckFromIds(GameDataManager manager, IEnumerable<string> ids)
    {
        var deck = new List<Card>();
        if (manager == null || ids == null) return deck;

        foreach (var id in ids)
        {
            if (string.IsNullOrEmpty(id)) continue;
            if (!manager.AllCards.TryGetValue(id, out Card template)) continue;

            for (int i = 0; i < 2; i++)
                deck.Add(template.Clone());
        }

        return deck;
    }

    // ConsoleRunner의 CreateResourceDeck 로직과 동일
    private List<Card> CreateResourceDeckFromDataManager(GameDataManager manager)
    {
        var deck = new List<Card>();
        if (manager != null && manager.AllCards.TryGetValue("RES-01", out Card template))
        {
            for (int i = 0; i < GameRules.ResourceDeckCount; i++)
                deck.Add(template.Clone());
            return deck;
        }

        // 데이터 로드 실패 시 폴백
        for (int i = 0; i < GameRules.ResourceDeckCount; i++)
        {
            deck.Add(new Card
            {
                Id = $"res_{i:000}",
                DataId = "RES-01",
                Name = "자원 카드",
                Type = CardType.Resource,
                Cost = 0,
                OriginalCost = 0,
                Speed = CardSpeed.None
            });
        }

        return deck;
    }

}