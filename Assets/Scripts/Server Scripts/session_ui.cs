using System.Collections.Generic;
using System.Linq;
using ServerScripts.EventScripts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static session_manage;

public class session_ui : MonoBehaviour
{
    public TMP_InputField session_id;       //사용자의 id   
    public TMP_InputField session_code;     //세션입장코드
    public TextMeshProUGUI session_status;  //세션상태
    public Button session_create;           //세션생성
    public Button session_join;             //세션입장
    public Button session_random_match;     //랜덤매칭
    public Button session_start;            //세션시작
    public Button session_exit;             //세션나가기

    public Button btn_action_A; // 행동 A
    public Button btn_action_B; // 행동 B
    public Button btn_action_C; // 행동 C

    public TextMeshProUGUI session_timer;
    public TextMeshProUGUI phase_text;
    public TextMeshProUGUI result_text;

    public TextMeshProUGUI my_life_text;
    public TextMeshProUGUI my_deck_text;
    public TextMeshProUGUI my_hand_text;
    public TextMeshProUGUI my_hand_cards_text;
    public TextMeshProUGUI my_resource_deck_text;
    public TextMeshProUGUI my_resource_zone_text;
    public TextMeshProUGUI my_grave_text;
    public TextMeshProUGUI my_set_zone_text;
    public TextMeshProUGUI my_set_card_text;
    public TextMeshProUGUI my_stack_zone_text;
    public TextMeshProUGUI my_stack_cards_text;
    public TextMeshProUGUI my_battlefield_text;

    public TextMeshProUGUI enemy_life_text;
    public TextMeshProUGUI enemy_deck_text;
    public TextMeshProUGUI enemy_hand_text;
    public TextMeshProUGUI enemy_resource_deck_text;
    public TextMeshProUGUI enemy_resource_zone_text;
    public TextMeshProUGUI enemy_grave_text;
    public TextMeshProUGUI enemy_set_zone_text;
    public TextMeshProUGUI enemy_set_card_text;
    public TextMeshProUGUI enemy_stack_zone_text;
    public TextMeshProUGUI enemy_stack_cards_text;
    public TextMeshProUGUI enemy_battlefield_text;

    [System.Serializable]
    public class ActionButton
    {
        public ActionType type; // 행동 종류 (A, B...)
        public Button button;   // 연결할 버튼
    }
    public List<ActionButton> actionButtons;

    public string GetID()  //사용자ID를 반환            
    {
        return session_id.text; 
    }
    public string GetSessionCode()  //세션입장코드 반환
    {
        return session_code.text;
    }

    public void UpdateStatus(string status) //세션상태업데이트
    {
        session_status.text = status;
        Debug.Log(status);
    }

    public void ToggleHost(bool host)   //버튼 ON OFF
    {
        if (session_start != null)
        {
            session_start.interactable = host;
        }
    }

    public void ToggleUI(bool isOn) //버튼 ON OFF
    {
        if (session_id != null) session_id.interactable = isOn;
        if (session_create != null) session_create.interactable = isOn;
        if (session_join != null) session_join.interactable = isOn;
        if (session_code != null) session_code.interactable = isOn;
        if (session_random_match != null) session_random_match.interactable = isOn;
    }
    public void SetActionButtonsState(bool isActive)
    {
        // A/B/C 버튼은 현재 룰 흐름에서 사용하지 않으므로 상태를 제어하지 않습니다.
        // if (btn_action_A != null) btn_action_A.interactable = isActive;
        // if (btn_action_B != null) btn_action_B.interactable = isActive;
        // if (btn_action_C != null) btn_action_C.interactable = isActive;
    }

    public void DestroySessionTimer(float time) //세션 제한 시간
    {
        if (session_timer == null) return;
        if (time > 0)
        {
            session_timer.text = $"Left session : {Mathf.CeilToInt(time)}";
        }
        else
        {
            session_timer.text = "";
        }
    }
    public void UpdateBoardStateUI(string myRole, BoardState boardState)
    {
        if (boardState == null) return;

        bool amHost = myRole == "HOST";
        PlayerState myState = amHost ? boardState.HostState : boardState.GuestState;
        PlayerState enemyState = amHost ? boardState.GuestState : boardState.HostState;

        if (myState == null || enemyState == null) return;

        int mySetCount = CountZoneCards(boardState, myRole, "SetZone");
        int myStackCount = CountZoneCards(boardState, myRole, "Stack");
        int myBattlefieldCount = CountZoneCards(boardState, myRole, "Battlefield");

        string enemyRole = amHost ? "GUEST" : "HOST";
        int enemySetCount = CountZoneCards(boardState, enemyRole, "SetZone");
        int enemyStackCount = CountZoneCards(boardState, enemyRole, "Stack");
        int enemyBattlefieldCount = CountZoneCards(boardState, enemyRole, "Battlefield");
        CardState mySetCardState = FindSetCardState(boardState, myRole);
        CardState enemySetCardState = FindSetCardState(boardState, enemyRole);

        if (phase_text != null)
            phase_text.text = $"Turn {boardState.CurrentTurn} / {boardState.CurrentPhase} / Active {boardState.ActivePlayer}";

        if (result_text != null)
        {
            if (!boardState.IsGameOver)
            {
                result_text.text = "";
            }
            else if (boardState.WinnerRole == "DRAW")
            {
                result_text.text = string.IsNullOrEmpty(boardState.ResultMessage) ? "Draw" : boardState.ResultMessage;
            }
            else if (boardState.WinnerRole == myRole)
            {
                string resultMessage = string.IsNullOrEmpty(boardState.ResultMessage) ? "Victory" : boardState.ResultMessage;
                result_text.text = $"Victory - {resultMessage}";
            }
            else
            {
                string resultMessage = string.IsNullOrEmpty(boardState.ResultMessage) ? "Defeat" : boardState.ResultMessage;
                result_text.text = $"Defeat - {resultMessage}";
            }
        }

        if (my_life_text != null) my_life_text.text = $"Life : {myState.LifeToken}";
        if (my_deck_text != null) my_deck_text.text = $"Deck : {myState.DeckCount}";
        if (my_hand_text != null) my_hand_text.text = $"Hand : {myState.HandCount}";
        if (my_hand_cards_text != null)
        {
            string handCards = myState.HandCardDataIds != null && myState.HandCardDataIds.Length > 0
                ? string.Join(", ", myState.HandCardDataIds)
                : "(empty)";
            my_hand_cards_text.text = $"My Cards : {handCards}";
        }
        if (my_resource_deck_text != null) my_resource_deck_text.text = $"ResDeck : {myState.ResourceDeckCount}";
        if (my_resource_zone_text != null) my_resource_zone_text.text = $"ResZone : {myState.ResourceZoneCount}";
        if (my_grave_text != null) my_grave_text.text = $"Grave : {myState.GraveCount}";
        if (my_set_zone_text != null) my_set_zone_text.text = $"SetZone : {mySetCount}";
        if (my_set_card_text != null) my_set_card_text.text = $"Set Card : {FormatSetCardText(mySetCardState, true)}";
        if (my_stack_zone_text != null) my_stack_zone_text.text = $"Stack : {myStackCount}";
        if (my_stack_cards_text != null) my_stack_cards_text.text = $"Stack Cards : {FormatStackCardList(boardState, myRole)}";
        if (my_battlefield_text != null) my_battlefield_text.text = $"Battlefield : {myBattlefieldCount}";

        if (enemy_life_text != null) enemy_life_text.text = $"Enemy Life : {enemyState.LifeToken}";
        if (enemy_deck_text != null) enemy_deck_text.text = $"Enemy Deck : {enemyState.DeckCount}";
        if (enemy_hand_text != null) enemy_hand_text.text = $"Enemy Hand : {enemyState.HandCount}";
        if (enemy_resource_deck_text != null) enemy_resource_deck_text.text = $"Enemy ResDeck : {enemyState.ResourceDeckCount}";
        if (enemy_resource_zone_text != null) enemy_resource_zone_text.text = $"Enemy ResZone : {enemyState.ResourceZoneCount}";
        if (enemy_grave_text != null) enemy_grave_text.text = $"Enemy Grave : {enemyState.GraveCount}";
        if (enemy_set_zone_text != null) enemy_set_zone_text.text = $"Enemy SetZone : {enemySetCount}";
        if (enemy_set_card_text != null) enemy_set_card_text.text = $"Enemy Set Card : {FormatSetCardText(enemySetCardState, false)}";
        if (enemy_stack_zone_text != null) enemy_stack_zone_text.text = $"Enemy Stack : {enemyStackCount}";
        if (enemy_stack_cards_text != null) enemy_stack_cards_text.text = $"Enemy Stack Cards : {FormatStackCardList(boardState, enemyRole)}";
        if (enemy_battlefield_text != null) enemy_battlefield_text.text = $"Enemy Battlefield : {enemyBattlefieldCount}";
    }

    private int CountZoneCards(BoardState boardState, string ownerRole, string zone)
    {
        if (boardState.FieldCards == null) return 0;

        return boardState.FieldCards.Count(card =>
            card != null &&
            card.OwnerRole == ownerRole &&
            card.Zone == zone);
    }

    private CardState FindSetCardState(BoardState boardState, string ownerRole)
    {
        if (boardState.FieldCards == null) return null;

        return boardState.FieldCards.FirstOrDefault(card =>
            card != null &&
            card.OwnerRole == ownerRole &&
            card.Zone == "SetZone");
    }

    private string FormatStackCardList(BoardState boardState, string ownerRole)
    {
        if (boardState?.FieldCards == null) return "(none)";

        var stackCards = boardState.FieldCards
            .Where(card =>
                card != null &&
                card.OwnerRole == ownerRole &&
                card.Zone == "Stack")
            .ToList();

        if (stackCards.Count == 0) return "(none)";

        return string.Join(", ", stackCards.Select(card =>
            string.IsNullOrEmpty(card.CardDataId) ? card.InstanceId : card.CardDataId));
    }

    private string FormatSetCardText(CardState setCardState, bool isMine)
    {
        if (setCardState == null)
            return "(none)";

        if (isMine)
        {
            string myCardId = string.IsNullOrEmpty(setCardState.CardDataId) ? setCardState.InstanceId : setCardState.CardDataId;
            string myStatus = setCardState.IsRevealed ? "Revealed" : "Hidden";
            return $"{myCardId} ({myStatus})";
        }

        if (!setCardState.IsRevealed)
            return "(hidden)";

        string enemyCardId = string.IsNullOrEmpty(setCardState.CardDataId) ? setCardState.InstanceId : setCardState.CardDataId;
        return $"{enemyCardId} (Revealed)";
    }
}