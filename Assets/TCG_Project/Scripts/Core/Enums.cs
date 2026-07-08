namespace TCG_Project.Scripts.Core
{
    public enum ZoneType
    {
        // 기존 존
        Deck,           // 메인덱 (List)
        Hand,           // 패 (List)
        Graveyard,      // 폐기존 (List)
        Field,          // 필드 - 레거시 프로토타입용 (Array)

        // 룰북 신규 존
        SetZone,        // 세트존: 매 턴 뒷면으로 올려두는 카드 1장
        ResourceDeck,   // 자원덱: 매 턴 1장씩 자원존으로 공급
        ResourceZone,   // 자원존: 코스트 지불에 사용하는 자원 카드 보관
        StackZone,      // 스택존: 대기 중인 스택 카드 보관 (복수 가능)
        BattlefieldZone,// 전장존: 파괴 전까지 지속되는 전장 카드 (1장)
        PlayBuffer      // 임시 발동 대기 존: ReplayCardEffect 등 복합 효과에서 카드를 잠시 보관
    }

    public enum GamePhase
    {
        // 룰북 6단계 페이즈
        TurnStart,      // 턴 시작 (초기화)
        ResourcePhase,  // 자원 페이즈: 자원덱→자원존 1장
        DrawPhase,      // 드로우 페이즈: 메인덱→패 1장
        SetPhase,       // 세트 페이즈: 패→세트존 뒷면 1장 (양측 동시 확정)
        OpenPhase,      // 오픈 페이즈: 공개 or 폐기 선택 (양측 동시 선언)
        MainPhase,      // 메인 페이즈: 스피드 순서로 카드 효과 해결
        EndPhase,       // 엔드 페이즈: 승리 조건 확인, 턴 종료 효과 처리
        TurnEnd,        // 턴 종료 후
    }

    public enum CardType
    {
        None = 0,

        // ── 룰북 카드 타입 ──────────────────────────────────────────────
        Attack    = 3, // 공격 카드: 데미지, 관통 등 직접 피해 효과
        Defense   = 4, // 방어 카드: 아머, 슈퍼아머, 무적 등 방어 효과
        Support   = 5, // 지원 카드: 드로우, 자원 획득, 화력 등 보조 효과
        Character = 6, // 캐릭터 카드: 캐릭터 고유 능력 보유
        Resource  = 7  // 자원 카드: 코스트 지불용
    }

    /// <summary>
    /// 카드 스피드: 1이 가장 빠름. 같은 스피드면 방어 > 공격 > 지원 순으로 처리.
    /// </summary>
    public enum CardSpeed
    {
        None = 0,
        Speed1 = 1,
        Speed2 = 2,
        Speed3 = 3
    }

    /// <summary>
    /// 효과 지속 시간. 엔드 페이즈에서 만료 여부를 판단하는 데 사용.
    /// </summary>
    public enum EffectDuration
    {
        Instant,    // 즉시 발동 후 소멸 (기본값)
        ThisTurn,   // 이번 턴 엔드 페이즈까지 유지
        NextTurn,   // 다음 턴 엔드 페이즈까지 유지 (자원 페이즈 시작부터 효과)
        Stack,      // 스택: 대응하는 효과가 발동될 때까지 필드에 잔류, 1회 사용 후 폐기
        Permanent   // 영구 지속 (전장 카드 등, 파괴 시 소멸)
    }

    /// <summary>
    /// 오픈 페이즈에서 플레이어의 선택. 공개하면 강제 사용, 폐기하면 드로우 1장.
    /// </summary>
    public enum OpenPhaseChoice
    {
        Open,    // 공개: 세트 카드를 앞면으로 뒤집고 강제 사용
        Abandon  // 폐기: 뒷면인 채로 폐기존 이동 + 메인덱에서 1장 드로우
    }

    public enum UserType
    {
        Human,  // 실제 플레이어 (입력 대기)
        Bot     // AI (즉시 자동 연산)
    }
}