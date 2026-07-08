using System;
using UnityEngine;

namespace ServerScripts.EventScripts
{
    // ==========================================
    // 0. 공통 부모 (모든 편지에 기본적으로 들어가는 내용)
    // ==========================================
    [Serializable]
    public class BaseRequest
    {
        public string MatchId;      // 방 번호 (Session Code)
        public string PlayerName;   // 요청을 보낸 사람 (예: "HOST" 또는 "GUEST")
    }

    // ==========================================
    // ─── 1. 턴 / 페이즈 진행 ───
    // ==========================================
    [Serializable]
    public class TurnEndRequest : BaseRequest
    {
        // 턴 종료는 누가 넘기는지만 알면 되므로 추가 변수 불필요
    }
    
    [Serializable]
    public class PhaseChangeRequest : BaseRequest
    {
        public string TargetPhase;  // 가고 싶은 페이즈
    }

    // ==========================================
    // ─── 2. 기본 전투 및 카드 행동 ───
    // ==========================================
    [Serializable]
    public class PlayCardRequest : BaseRequest
    {
        public string CardInstanceId; // 유저가 내려고 고른 카드의 고유번호
        public string[] ResourceCardInstanceIds;
    }

    [Serializable]
    public class ResourceActionRequest : BaseRequest //자원 획득
    {
    }
    // ==========================================
    // ─── 3. Human Input (상세 UI 선택 응답) ───
    // ==========================================

    // 1. 세트 페이즈 응답
    [Serializable]
    public class SetPhaseActionRequest : BaseRequest
    {
        public string PickedCardInstanceId; // 세트하려고 고른 카드의 고유번호

        public bool IsConfirmed;// false: 카드를 올려두기만 한 상태 (선택 스텝)
                                 // true: 카드를 최종적으로 결정한 상태 (확정 스텝)
    }

    // 2. 오픈 페이즈 응답
    [Serializable]
    public class OpenPhaseActionRequest : BaseRequest
    {
        public string SetCardInstanceId; // 세트존에 있던 카드의 고유번호
        public string Choice;            // 유저의 선택 (Enum을 string으로 변환해서 전송, 예: "Open" 또는 "Abandon")
    }

    // 3. 스택 응답
    [Serializable]
    public class StackResponseRequest : BaseRequest
    {
        public string StackCardInstanceId;    // 발동할 내 스택 카드의 고유번호
        public string OpponentCardInstanceId; // 원인이 된 상대방 카드의 고유번호
        public bool IsUsingStack;             // 발동 여부 (true/false)
    }

    // 4. 카드 픽 응답 
    [Serializable]
    public class CardPickRequest : BaseRequest
    {
        public string RequestId;               // 어떤 카드 선택 요청에 대한 응답인지 식별
        // 유니티 JsonUtility는 List 직렬화에 제약이 있어서 배열(Array)을 쓰는 것이 안전합니다.
        public string[] PickedCardInstanceIds; // 고른 카드들의 고유번호 배열
    }

    // 5. 카드 서치 응답
    [Serializable]
    public class CardChoiceRequest : BaseRequest
    {
        public string RequestId;               // 어떤 카드 선택 요청에 대한 응답인지 식별
        public string[] PickedCardInstanceIds; // 고른 카드들의 고유번호 배열
        public string Zone;                    // 어디서 골랐는지 (예: "Graveyard")
    }

    // 6. 선택적 행동 응답
    [Serializable]
    public class OptionalActionRequest : BaseRequest
    {
        public string ActionId; // 어떤 질문에 대한 대답인지 식별 (예: "optionalDrawSkip")
        public bool Choice;     // 예(true) / 아니오(false)
    }
    [Serializable]

    public class RequireCardPickNotification : BaseRequest
    {
        public string RequestId;                  // 응답 시 그대로 돌려줄 요청 식별자
        public string[] PresentedCardInstanceIds; // 화면에 띄워줄 후보 카드들
        public string[] PresentedCardDataIds;     // 화면 표시용 카드 데이터 ID들
        public int RequiredCount;                 // 몇 장을 골라야 하는지
        public string Message;                    // 예: "카드 1장을 선택하세요."
    }

    // 예시: 서버가 선택적 행동(예/아니오)을 요구할 때 보내는 DTO
    [Serializable]
    public class RequireOptionalActionNotification : BaseRequest
    {
        public string ActionMessage; // 예: "메인덱 4장을 폐기하시겠습니까?"
        public string ActionId;
    }
    // ==========================================
    // ─── 4. 특수 요청 ───
    // ==========================================

    // 수동 드로우 요청
    [Serializable]
    public class CardDrawRequest : BaseRequest
    {
        public int DrawCount; // 몇 장을 뽑을 것인지
    }

    // 게임 종료 요청
    [Serializable]
    public class GameSetRequest : BaseRequest
    {
        public string WinnerName;
    }
    
    // ==========================================
    // ─── 5. 직접 수치/상태 제어 요청 ───
    // ==========================================

    // 라이프(체력 코인) 변경 
    [Serializable]
    public class LifeChangeRequest : BaseRequest
    {
        public string TargetPlayerName;
        public int NewLife;             // 덮어씌울 최종 라이프 수치
    }

    // 상태 이상(지침, 스턴 등) 
    [Serializable]
    public class UnitStatusChangeRequest : BaseRequest
    {
        public string TargetInstanceId;
        public string Status;           // 적용할 상태 이름 (예: "Stunned")
    }
    [Serializable]
    public class BoardState
    {
        public int CurrentTurn;
        public string CurrentPhase;
        public string ActivePlayer;
        public bool IsGameOver;
        public string WinnerRole;
        public string ResultMessage;
        public bool IsHostReady;
        public bool IsGuestReady;

        // 룰북 메인 페이즈 스피드 시스템 추가
        public int CurrentSpeedTurn;       // 현재 처리 중인 스피드 (1~3)
        public string CurrentPriorityTurn; // 현재 우선순위 ("방어", "공격", "지원")

        // 양쪽 플레이어의 핵심 수치
        public PlayerState HostState;
        public PlayerState GuestState;

        // 필드, 세트존, 스택존에 깔려있는 카드들의 상태 목록
        public CardState[] FieldCards;
    }

    [Serializable]
    public class PlayerState
    {
        public string Character1_ID;    
        public string Character2_ID;   
        public int LifeToken;           
        
        public int DeckCount;         // 메인덱 장수 (20장 시작)
        public int ResourceDeckCount; // 자원덱 장수 (15장 시작)
        public int ResourceZoneCount; // 자원존에 놓인 사용 가능한 자원 수
        public int HandCount;         // 패에 든 장수
        public int GraveCount;        // 폐기존 장수

        public int CurrentArmor;      // 현재 아머 수치
        public int CurrentSuperArmor; // 현재 슈퍼 아머 수치
        public int CurrentFirepower;  // 현재 화력 수치
        public bool IsInvincible;     // 무적 상태 여부
        public bool IsCounterActive;  // 반격 활성화 여부

        public int SetWins;

        // 디버깅/관찰용: 각 존의 실제 카드 식별자 목록
        public string[] HandCardInstanceIds;
        public string[] HandCardDataIds;
        public string[] DeckCardInstanceIds;
        public string[] DeckCardDataIds;
        public string[] ResourceDeckCardInstanceIds;
        public string[] ResourceDeckCardDataIds;
        public string[] ResourceZoneCardInstanceIds;
        public string[] ResourceZoneCardDataIds;
        public string[] GraveCardInstanceIds;
        public string[] GraveCardDataIds;
    }

    [Serializable]
    public class CardState
    {
        public string InstanceId;
        public string CardDataId;

        public string OwnerRole;
        public string Zone;       // "SetZone", "StackZone", "BattlefieldZone" 등

        public bool IsRevealed;   // 세트존에서 '공개(Open)' 되었는지 여부

        // 유닛/전장 카드일 경우 수치들 (필요시 사용)
        public int CurrentPower;
        public string Status;
    }
    // ==========================================
    // ─── 6. 서버 -> 클라이언트 지시 (Notification) ───
    // ==========================================

    // 1. 세트 페이즈 지시 (세트할 카드를 고르라는 팝업)
    [Serializable]
    public class RequireSetPhaseNotification : BaseRequest
    {
        public string Message; // 예: "패에서 세트할 카드를 선택해 주세요."
    }

    // 2. 오픈 페이즈 지시 (세트된 카드를 공개할지 묻는 팝업)
    [Serializable]
    public class RequireOpenPhaseNotification : BaseRequest
    {
        public string SetCardInstanceId; // 공개/폐기할 카드의 고유번호
        public int EffectiveCost;        // 지불해야 할 실효 코스트
        public string Message;           // 예: "비용을 지불하고 카드를 공개하시겠습니까?"
    }

    // 3. 스택 방어 지시 (상대 공격 시 스택 카드를 쓸지 묻는 팝업)
    [Serializable]
    public class RequireStackNotification : BaseRequest
    {
    public string StackCardInstanceId;    // 발동 여부를 묻는 내 스택 카드의 고유번호
    public string StackCardDataId;        // 클라이언트 UI 표시용 카드 데이터 ID
        public string OpponentCardInstanceId; // 공격 들어온 상대방 카드의 고유번호
        public string Message;                // 예: "상대가 공격했습니다! 스택 카드를 발동하시겠습니까?"
    }


    // 5. 카드 서치/초이스 지시 (특정 존에서 조건에 맞는 카드를 찾으라는 팝업)
    [Serializable]
    public class RequireCardChoiceNotification : BaseRequest
    {
        public string RequestId;                  // 응답 시 그대로 돌려줄 요청 식별자
        public string Zone;         // 찾을 장소 (예: "Graveyard")
        public string[] PresentedCardInstanceIds; // 서버가 허용한 후보 카드들
        public string[] PresentedCardDataIds;     // 화면 표시용 카드 데이터 ID들
        public int RequiredCount;   // 몇 장을 골라야 하는지
        public string Filter;       // 찾을 조건 (예: "character:ELLI" - 클라이언트가 필터링할 때 사용)
        public string Message;      // 예: "폐기존에서 엘리 카드 1장을 선택하세요."
    }

    // 6. 선택적 행동 지시 (디메리트 지불 등 Yes/No 질문 팝업)
    [Serializable]
    public class RequireOptionalNotification : BaseRequest
    {
        public string ActionId; // 응답받을 때 어떤 질문에 대한 대답인지 알기 위한 꼬리표
        public string Message;  // 예: "효과를 위해 패를 2장 버리시겠습니까?"
    }

[Serializable]
public class LifeChangeNotification : BaseRequest
{
    public string TargetPlayerName;
    public int NewLife;
    public int Delta;
    public string Reason;
}

    // ==========================================
    // ─── 7. 서버 -> 클라이언트 연출 (Visual / Effect) ───
    // ==========================================

    [Serializable]
    public class VisualEventNotification : BaseRequest
    {
        public string EventType;      // 연출 종류: "PlayCard"(카드 커짐), "CardMove"(이동 궤적), "Delay"(대기)
        public string CardInstanceId; // 연출의 주인공이 되는 카드
        public string CardDataId;     // 클라에서 카드 외형을 복원할 때 사용할 데이터 ID
        public string OwnerRole;      // 카드 소유자 역할(HOST/GUEST)
        public string FromZone;       // (이동 시) 출발지
        public string ToZone;         // (이동 시) 도착지
        public float DelaySeconds;    // (대기 시) 애니메이션을 기다려줄 시간(초)
    }

    [Serializable]
    public class LogNotification : BaseRequest
    {
        public string LogMessage;     // 전투 로그 창에 띄울 텍스트 (예: "아머 1로 방어!")
    }
}