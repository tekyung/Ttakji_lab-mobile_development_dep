using System;
using TCG_Project.Scripts.Core;

namespace TCG_Project.Scripts.Managers
{
    public static class EventManager
    {
        // 1. 디버깅/로그 (콘솔창 대신 UI 로그창에 띄울 때 사용)
        public static Action<string> OnLogMessage;

        // 2. 턴 진행
        public static Action<int, string> OnTurnStart; // 턴수, 플레이어 이름
        public static Action<string> OnTurnEnd;        // 플레이어 이름

        // 3. 플레이어 상태
        public static Action<Player, int> OnManaChange;   // 대상, 현재 마나
        public static Action<Player, int> OnHealthChange; // 대상, 현재 체력
        public static Action<Player, int> OnPrizeChange;  // 대상, 현재 승점

        // 4. 전투 및 카드 행동
        public static Action<Player, Card> OnPlayCard;    // 누가, 어떤 카드를 냈나
        public static Action<Card> OnUnitSummoned;        // 유닛 소환됨 (연출용)
        public static Action<Card, Card> OnAttack;        // 공격자, 타겟 (공격 애니메이션)
        public static Action<Card, int> OnUnitTakeDamage; // 맞은 유닛, 데미지 양 (피격 연출)
        public static Action<Card> OnUnitDeath;           // 죽은 유닛 (사망 연출)

        // 5. 카드 이동 (드로우, 버리기 등)
        public static Action<Card, ZoneType, ZoneType> OnCardMove; // 카드, 어디서, 어디로
    }
}

