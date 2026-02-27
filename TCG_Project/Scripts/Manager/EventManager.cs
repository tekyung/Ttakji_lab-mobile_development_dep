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

        public static Action<string, int> OnDrawPhase;   // 드로우 페이즈: 플레이어 이름, 턴 수
        public static Action<string, int> OnMainPhase; // 메인 페이즈: 플레이어 이름, 턴 수
        public static Action<string, int> OnBattlePhase; // 배틀 페이즈: 플레이어 이름, 턴 수
        public static Action<string, int> OnEndPhase;  // 엔드 페이즈: 플레이어 이름, 턴 수

        public static Action<Player, Player> OnGameStart;      // 게임 시작: 플레이어, 플레이어
        public static Action<Player> OnGameSet;   // 게임 종료: 승리 플레이어
        public static Action<Player, Player, int> OnGameDraw;    // 무승부: 양쪽 플레이어, 턴 수

        // 3. 플레이어 상태
        public static Action<Player, int> OnManaChange;   // 누구의, 현재 마나
        public static Action<Player, int> OnPrizeChange;  // 누구의, 현재 승점

        // 4. 전투 및 카드 행동
        public static Action<Player, Card> OnPlayCard;    // 누가, 어떤 카드를 냈나
        public static Action<Card> OnUnitSummoned;        // 유닛 소환 (연출용)
        public static Action<Card, Card> OnAttack;        // 공격유닛, 타겟유닛 (공격 애니메이션)
        public static Action<Card, int> OnUnitTakeDamage; // 맞은 유닛, 데미지 양 (피격 연출)
        public static Action<Card> OnUnitDeath;           // 죽은 유닛 (사망 연출)
        public static Action<Card, String> UnitStatusChange; // 유닛 상태 갱신 (지침, 스턴 등)
        public static Action<Card, int> OnCardPowerChanged; //  대상 유닛, Power 변화량 : 유닛 카드의 파워 변경 연출 (상태 변화)

        // 5. 카드 이동 (드로우, 버리기 등)
        public static Action<Card, Player, ZoneType, Player, ZoneType> OnCardMove; // (지정 이동)카드, 누구의, 어디서, 누구의, 어디로
        public static Action<Card, Player, ZoneType> OnCardDraw; // (단순 이동)카드, 누구의, 어디로


        // --- 6. Human Input (사람의 개입 요구) ---
        // UI에게 "유저가 메인 페이즈 행동(카드 내기 등)을 마치고 [턴 종료]를 누를 때까지 기다려 줘!" 라고 요청
        public static Action<Player, GameContext, Action> OnRequireMainPhaseAction;

        // UI에게 "유저가 유닛들 공격 지시를 마치고 [배틀 종료]를 누를 때까지 기다려 줘!" 라고 요청
        public static Action<Player, GameContext, Action> OnRequireBattlePhaseAction;

        // ★ UI에게 "이 타겟 후보(candidates) 중에서 유저가 1개를 마우스로 클릭하면, 그 결과를 콜백으로 돌려줘!" 라고 요청
        public static Action<List<Target>, int, Action<List<Target>>> OnRequireTargetSelection;
    }
}

