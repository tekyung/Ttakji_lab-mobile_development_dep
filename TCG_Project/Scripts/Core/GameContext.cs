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

        // 유틸리티: 적 찾기 (1:1 상황 가정 시 편의 기능)
        public Player GetOpponent(Player me)
        {
            // 나(me)가 아닌 첫 번째 플레이어를 반환
            return Players.Find(p => p != me);
        }

        // ★ 게임 종료 여부를 판단하는 락(Lock)
        public bool IsGameOver { get; set; } = false;

        // 변수 저장소 (Name -> Integer)
        private Dictionary<string, int> variables = new Dictionary<string, int>();

        // 예약된 효과 리스트
        public List<PendingEffect> PendingEffects { get; private set; } = new List<PendingEffect>();

        public void RegisterPendingEffect(PendingEffect effect) // 후처리 효과 예약
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

        // 읽기 전용 접근자 (Evaluator용)
        public IReadOnlyDictionary<string, int> Variables => variables;
    }
}
