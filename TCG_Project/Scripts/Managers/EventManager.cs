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
    /// <summary>
    /// 카드 선택 요청의 성격. 문구만 단단히 넘기던 것을 구조체로 묶었다
    /// — 앞으로 항목이 늘어도 이벤트 시그니처를 다시 손대지 않기 위해서다.
    ///
    /// 유니티 의존이 없는 순수 구조체라 콘솔·강화학습 빌드에도 그대로 올라간다.
    /// </summary>
    public readonly struct CardPickPrompt
    {
        /// <summary>선택창에 띄울 문구. 비어 두면 수신쪽이 기본 문구를 만든다.</summary>
        public string Message { get; }

        /// <summary>
        /// true면 <b>고르는 순서가 결과를 바꿘다</b>(베로니카의 되돌리기 순서).
        /// UI는 이때만 선택 카드에 1·2·… 번호를 띄운다.
        /// '패 3장 버리기'처럼 순서가 무의미한 요청에 번호를 띄우면
        /// "순서가 중요한가?"라는 오해를 주므로 기본값은 false다.
        /// </summary>
        public bool Ordered { get; }

        public CardPickPrompt(string message, bool ordered = false)
        {
            Message = message;
            Ordered = ordered;
        }
    }

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

        /// <summary>타이브레이커 실행 시 발행.</summary>
        public static Action<Player, Player> OnTiebreaker;

        /// <summary>3판 2선승 매치가 무승부로 종료되었을 때 발행.</summary>
        public static Action<Player, Player> OnMatchDraw;

        // ─── 4. 플레이어 상태 변경 ─────────────────────────────────────────────

        /// <summary>라이프 토큰 변경. player=소유자, newValue=변경 후 값.</summary>
        public static Action<Player, int> OnLifeChange;

        // 자원 변경
        public static Action<Player, int> OnResourceChange;

        /// <summary>게임 시작/리셋 시 용병 필드 4슬롯 전체 동기화.</summary>
        public static Action<IReadOnlyList<CharacterSlotSnapshot>> OnCharacterFieldSync;

        /// <summary>용병 고유 능력 사용 후 단일 슬롯 갱신 (회전·imagePath 포함).</summary>
        public static Action<CharacterSlotSnapshot> OnCharacterSlotUpdated;

        /// <summary>용병 고유 능력 사용 상태 변경. BattleManager가 스냅샷으로 변환해 방송.</summary>
        public static Action<Player, string> OnCharacterAbilityUsed;

        // ─── 5. 전투 및 카드 행동 ──────────────────────────────────────────────

        /// <summary>카드 사용(Play) 시 발행.</summary>
        public static Action<Player, Card> OnPlayCard;

        /// <summary>카드 상태 변경 시 발행.</summary>
        public static Action<Card> OnCardStateChanged;

        /// <summary>카드 효과 발동 실패 시 발행.</summary>
        public static Action<Card> OnPlayFailed;

        // ─── 6. 카드 이동 ──────────────────────────────────────────────────────

        /// <summary>카드가 한 존에서 다른 존으로 이동할 때 발행. (기본값)</summary>
        public static Action<Card, Player, ZoneType, Player, ZoneType> OnCardMove;

        /// <summary>카드가 드로우될 때 발행.</summary>
        public static Action<Card, Player, ZoneType> OnCardDraw;

        /// <summary>카드가 버려질 때 발행.</summary>
        /// 예: 패→묘지, 세트존→묘지(폐기), 스택존→묘지(폐기) 등 모든 버려짐 상황에서 발행.
        public static Action<Card, Player, ZoneType> OnCardDiscard;

        /// <summary>카드가 세트될 때 발행.</summary>
        public static Action<Card, Player> OnCardSet;

        /// <summary>카드가 스택에 추가될 때 발행.</summary>
        public static Action<Card, Player> OnCardStacked;

        /// <summary>카드가 스택에서 제거될 때 발행.</summary>
        public static Action<Card, Player> OnCardUnstacked;

        /// <summary>카드가 전장에 배치될 때 발행.</summary>
        public static Action<Card, Player> OnCardBattlefield;

        /// <summary>카드가 전장에서 제거될 때 발행.</summary>
        public static Action<Card, Player> OnCardUnbattlefield;

        /// <summary>카드가 자원 존에 추가될 때 발행.</summary>
        public static Action<Card, Player> OnCardResourceAdded;

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
        ///
        /// 네 번째 인자는 요청의 성격을 담는 <see cref="CardPickPrompt"/>다.
        /// <c>default</c>를 넘기면 수신쪽이 기본 문구를 만든다(예전 동작).
        /// </summary>
        public static Action<Player, List<Card>, int, CardPickPrompt, Action<List<Card>>> OnRequireCardPick;

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