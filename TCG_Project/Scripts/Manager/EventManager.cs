using System;
using TCG_Project.Scripts.Core;

namespace TCG_Project.Scripts.Managers
{
    public static class EventManager
    {
        // 1. 디버깅/로그 (콘솔창 대신 UI 로그창에 띄울 때 사용)
        public static Action<string> OnLogMessage;

        // 2. 턴 진행
        public static Action<int, string> OnTurnStart; // 턴 시작: 턴수, 플레이어 이름
        public static Action<string> OnTurnEnd;        // 턴 종료: 플레이어 이름
        public static Action<string, int> OnMainPhase; // 메인 페이즈: 플레이어 이름, 턴 수
        public static Action<string, int> OnBattlePhase; // 배틀 페이즈: 플레이어 이름, 턴 수

        public static Action<Player, Player> GameStart;      // 게임 시작: 플레이어, 플레이어
        public static Action<Player, Player, int> GameEnd;   // 게임 종료: 플레이어, 플레이어, 턴 수

        // 3. 플레이어 상태
        public static Action<Player, int> OnManaChange;   // 누구의, 현재 마나
        public static Action<Player, int> OnHealthChange; // 누구의, 현재 체력
        public static Action<Player, int> OnPrizeChange;  // 누구의, 현재 승점

        // 4. 전투 및 카드 행동
        public static Action<Player, Card> OnPlayCard;    // 누가, 어떤 카드를 냈나
        public static Action<Card> OnUnitSummoned;        // 유닛 소환 (연출용)
        public static Action<Card, Card> OnAttack;        // 공격유닛, 타겟유닛 (공격 애니메이션)
        public static Action<Card, int> OnUnitTakeDamage; // 맞은 유닛, 데미지 양 (피격 연출)
        public static Action<Card> OnUnitDeath;           // 죽은 유닛 (사망 연출)
        public static Action<Card, String> UnitStatusChange; // 유닛 상태 갱신 (지침, 스턴 등)

        // 5. 카드 이동 (드로우, 버리기 등)
        public static Action<Card, Player, ZoneType, Player, ZoneType> OnCardMove; // (지정 이동)카드, 누구의, 어디서, 누구의, 어디로
        public static Action<Card, Player, ZoneType> OnCardDraw; // (단순 이동)카드, 누구의, 어디로
    }
}

