using System.Collections.Generic;
using TCG_Project.Scripts.Core;

namespace TCG_Project.Scripts.Core
{
    public class GameContext
    {
        // 전체 플레이어 목록
        public List<Player> Players { get; set; } = new List<Player>();

        // 현재 턴을 진행 중인 플레이어 (행동 주체)
        public Player ActivePlayer { get; set; }

        // 타겟팅된 플레이어 (공격 대상 등) - 상황에 따라 null일 수 있음
        public Player TargetPlayer { get; set; }
        
        // 게임 중 현재의 턴
        public int CurrentTurn { get; set; } = 1;

        // 유틸리티: 적 찾기 (1:1 상황 가정 시 편의 기능)
        public Player GetOpponent(Player me)
        {
            // 나(me)가 아닌 첫 번째 플레이어를 반환
            return Players.Find(p => p != me);
        }

        // 현재 진행 중인 페이즈 (UI 상태 표시 및 효과 발동 타이밍 판단용)
        public GamePhase CurrentPhase { get; set; } = GamePhase.TurnStart;

        // ★ 게임 종료 여부를 판단하는 락(Lock)
        public bool IsGameOver { get; set; } = false;

        /// <summary>
        /// Phase 18: "그 후" 시맨틱 — 선행 효과 실패 시 후속 효과 불발.
        /// CompositeEffect가 Step N 실행 전에 검사. Effect가 실패 시 false로 설정.
        /// </summary>
        public bool LastEffectSucceeded { get; set; } = true;

        // 각 플레이어(Name 또는 ID 기준)의 이번 턴 오픈 페이즈 결과 기록
        public Dictionary<string, PlayerOpenPhaseState> OpenPhaseStates { get; set; }
            = new Dictionary<string, PlayerOpenPhaseState>();

        // 오픈 페이즈 시작 시 이전 턴 기록 초기화
        public void ClearOpenPhaseStates()
        {
            OpenPhaseStates.Clear();
        }

        // 변수 저장소 (Name -> Integer)
        private Dictionary<string, int> variables = new Dictionary<string, int>();

        // 예약된 효과 리스트
        public List<PendingEffect> PendingEffects { get; private set; } = new List<PendingEffect>();

        public void RegisterPendingEffect(PendingEffect effect) // 후처리 효과 예약 (아직 미사용)
        {
            PendingEffects.Add(effect);
            System.Console.WriteLine($"⏰ [예약] {effect.TriggerPhase}에 효과 발동 예약됨.");
        }

        // 변수 설정 (Write)
        public void SetVariable(string key, int value)
        {
            if (string.IsNullOrEmpty(key)) return;

            if (variables.ContainsKey(key)) variables[key] = value;
            else variables.Add(key, value);

            // 디버깅: 변수 변경 로그
            // System.Console.WriteLine($"   💾 [Memory] {key} = {value}");
        }

        // 변수 설정 (Bool 오버로딩: True=1, False=0)
        public void SetVariable(string key, bool value)
        {
            SetVariable(key, value ? 1 : 0);
        }

        // 변수 가져오기 (Read)
        public int GetVariable(string key)
        {
            return variables.ContainsKey(key) ? variables[key] : 0;
        }

        // 변수 초기화 (카드 사용 시 호출 권장)
        public void ClearVariables()
        {
            variables.Clear();
        }

        // 상대방이 특정 타입의 카드를 공개했는지 확인하는 유틸리티 (S2 유나 컨셉)
        public bool DidOpponentRevealCardType(Player me, CardType targetType)
        {
            Player opponent = GetOpponent(me);
            if (opponent == null || !OpenPhaseStates.ContainsKey(opponent.Name))
                return false;

            var state = OpenPhaseStates[opponent.Name];
            return state.HasOpened && state.RevealedCard != null && state.RevealedCard.Type == targetType;
        }

        // 읽기 전용 접근자 (Evaluator용)
        public IReadOnlyDictionary<string, int> Variables => variables;
    }
    // 오픈 페이즈에서의 플레이어 행동 기록용 클래스
    public class PlayerOpenPhaseState
    {
        public bool HasOpened { get; set; } = false; // true: 공개, false: 폐기
        public Card RevealedCard { get; set; } = null; // 공개한 카드 (폐기했으면 null)
    }
}
