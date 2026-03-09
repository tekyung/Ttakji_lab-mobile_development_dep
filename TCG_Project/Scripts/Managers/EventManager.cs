using System;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;

namespace TCG_Project.Scripts.Managers
{
    /// <summary>
    /// 게임 전체 이벤트 버스. 정적 Action 필드를 통해 발행/구독(pub/sub) 패턴을 구현한다.
    /// 게임 로직은 Unity/UI를 직접 호출하지 않고 이 클래스의 이벤트를 통해 방송한다.
    /// UI(또는 ConsoleRunner)는 이벤트를 구독하여 반응한다.
    /// </summary>
    public static class EventManager
    {
        // ─── 1. 디버깅 / 로그 ──────────────────────────────────────────────────

        /// <summary>
        /// 콘솔 또는 Unity 디버그 패널에 출력할 문자열 메시지.
        /// &lt;color=cyan&gt;...&lt;/color&gt; 태그를 사용하면 ConsoleRunner가 색상을 적용한다.
        /// </summary>
        public static Action<string> OnLogMessage;

        // ─── 2. 턴 진행 ────────────────────────────────────────────────────────

        /// <param name="turn">라운드 번호</param>
        /// <param name="playerName">주체 플레이어 이름 (양측="양측")</param>
        public static Action<int, string> OnTurnStart;

        /// <param name="playerName">주체 플레이어 이름</param>
        public static Action<string> OnTurnEnd;

        // ─── 2-1. 룰북 6페이즈 이벤트 ─────────────────────────────────────────

        /// <summary>자원 페이즈 시작: 자원덱→자원존 1장 이동 직전에 발행.</summary>
        public static Action<string, int> OnResourcePhase;

        /// <summary>드로우 페이즈 시작: 메인덱→패 1장 드로우 직전에 발행.</summary>
        public static Action<string, int> OnDrawPhase;

        /// <summary>세트 페이즈 시작: 양측 카드 세트 직전에 발행.</summary>
        public static Action<string, int> OnSetPhase;

        /// <summary>오픈 페이즈 시작: 공개/폐기 선택 직전에 발행.</summary>
        public static Action<string, int> OnOpenPhase;

        /// <summary>메인 페이즈 시작: SpeedResolver 처리 직전에 발행.</summary>
        public static Action<string, int> OnMainPhase;

        /// <summary>엔드 페이즈 시작: 승리 조건 확인 및 버프 만료 직전에 발행.</summary>
        public static Action<string, int> OnEndPhase;

        // ─── 3. 게임 / 매치 단위 이벤트 ──────────────────────────────────────

        /// <summary>단일 게임 시작 시 발행.</summary>
        public static Action<Player, Player> OnGameStart;

        /// <summary>단일 게임 승자가 결정되었을 때 발행. winner = 승리 플레이어.</summary>
        public static Action<Player> OnGameSet;

        /// <summary>단일 게임이 무승부로 종료되었을 때 발행.</summary>
        public static Action<Player, Player, int> OnGameDraw;

        /// <summary>3판 2선승 매치의 승자가 결정되었을 때 발행.</summary>
        public static Action<Player> OnMatchSet;

        /// <summary>3판 2선승 매치가 무승부로 종료되었을 때 발행.</summary>
        public static Action<Player, Player> OnMatchDraw;

        // ─── 4. 플레이어 상태 변경 ─────────────────────────────────────────────

        /// <summary>승점(PrizePoints) 변경. player=소유자, newValue=변경 후 값.</summary>
        public static Action<Player, int> OnPrizeChange;

        /// <summary>라이프 토큰 변경. player=소유자, newValue=변경 후 값.</summary>
        public static Action<Player, int> OnLifeChange;

        // ─── 5. 전투 및 카드 행동 ──────────────────────────────────────────────

        /// <summary>카드 사용(Play) 시 발행.</summary>
        public static Action<Player, Card> OnPlayCard;

        // ─── 6. 카드 이동 ──────────────────────────────────────────────────────

        /// <summary>카드가 한 존에서 다른 존으로 이동할 때 발행.</summary>
        public static Action<Card, Player, ZoneType, Player, ZoneType> OnCardMove;

        /// <summary>카드가 드로우될 때 발행.</summary>
        public static Action<Card, Player, ZoneType> OnCardDraw;

        // ─── 7. Human Input — 6페이즈 구조 (Phase 8) ──────────────────────────

        /// <summary>
        /// 세트 페이즈: UI에 "패에서 세트할 카드를 선택해 주세요" 요청.
        /// 콜백 Action&lt;Card&gt;으로 선택된 카드를 수신한다.
        /// </summary>
        public static Action<Player, GameContext, Action<Card>> OnRequireSetPhaseAction;

        /// <summary>
        /// 오픈 페이즈: UI에 "공개(Open) 또는 폐기(Abandon)를 선택해 주세요" 요청.
        /// effectiveCost(int)는 전장 코스트 감소가 반영된 실효 코스트.
        /// 콜백 Action&lt;OpenPhaseChoice&gt;으로 결과를 수신한다.
        /// </summary>
        public static Action<Player, Card, int, GameContext, Action<OpenPhaseChoice>> OnRequireOpenPhaseAction;

        /// <summary>
        /// 스택 발동 여부: UI에 "이 스택 카드를 지금 발동할지 결정해 주세요" 요청.
        /// stackCard=스택 카드, opponentCard=상대가 낸 카드.
        /// 콜백 Action&lt;bool&gt;으로 발동 여부를 수신한다.
        /// </summary>
        public static Action<Player, Card, Card, Action<bool>> OnRequireStackResponse;

        /// <summary>
        /// 제시된 카드 목록에서 선택: "이 목록 중 N장을 선택해 주세요" 요청.
        /// 예: VERONICA 덱 탑 3장 중 1장 선택.
        /// </summary>
        public static Action<Player, List<Card>, int, Action<List<Card>>> OnRequireCardPick;

        /// <summary>
        /// 존에서 카드 선택: "이 존에서 조건에 맞는 카드 N장을 선택해 주세요" 요청.
        /// filter: "character:ELLI,type:Attack" 형식.
        /// </summary>
        public static Action<Player, ZoneType, int, string, Action<List<Card>>> OnRequireCardChoice;

        // ─── 8. Unity 연출 / 타이밍 제어 ──────────────────────────────────────

        /// <summary>
        /// Unity UI에 지정된 초만큼 연출 딜레이를 요청한다.
        /// BattleManager의 코루틴이 WaitForSeconds(seconds)로 대기한다.
        /// 콘솔 환경에서는 구독자 없이 무시된다.
        /// </summary>
        public static Action<float> OnRequestVisualDelay;

        // ─── 9. 선택적 행동 ──────────────────────────────────────────────────────
        /// <summary>
        /// 선택적 행동 수행 여부를 결정합니다. (예: "메인덱 4장을 폐기하시겠습니까?")
        /// </summary>
        public static Action<Player, string, GameContext, Action<bool>> OnRequireOptionalAction;
    }
}
