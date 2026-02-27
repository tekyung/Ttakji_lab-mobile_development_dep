namespace TCG_Project.Scripts.Core
{
    public enum ZoneType
    {
        Deck,       // 덱 (List)
        Hand,       // 패 (List)
        Graveyard,  // 묘지 (List)
        Field       // 필드 (Array - 고정 인덱스)
    }

    public enum GamePhase
    {
        TurnStart,  // 턴 시작 시
        TurnEnd,    // 턴 종료 시
        BattleStart, // 배틀 시작 시
        BattleEnd, // 배틀 종료 시
        CardPlayed, // 카드 발동 시
        CardDye, // 카드 유언계
        CardDraw // 카드 드로우 시
    }

    public enum CardType
    {
        None = 0,
        Unit = 1,
        Skill = 2
    }
    public enum UserType
    {
        Human,  // 실제 플레이어 (마우스/키보드 입력 대기)
        Bot     // AI (즉시 자동 연산)
    }

}