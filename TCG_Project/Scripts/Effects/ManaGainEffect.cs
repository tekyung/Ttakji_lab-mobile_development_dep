using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;
using TCG_Project.Scripts.Systems;
using System; // Action 콜백을 위해 추가

namespace TCG_Project.Scripts.Effects
{
    public class ManaGainEffect : ICardEffect
    {
        private object amountParam;
        private object targetParam;

        // 복제를 위해 초기화 파라미터를 기억해 둡니다.
        private Dictionary<string, object> _cachedParams;
        public void Initialize(Dictionary<string, object> parameters)
        {
            _cachedParams = parameters; // 캐싱
            amountParam = parameters["amount"];
            targetParam = parameters.ContainsKey("target") ? parameters["target"] : "Self";
        }

        public void Execute(GameContext context, Action onComplete)
        {
            // 1. 수식 계산 (var.recycle_amount 같은 변수도 여기서 처리됨)
            

            // 2. 타겟 선정
            
            int finalAmount = FormulaEvaluator.Evaluate(amountParam, context);
            List<Player> targets = TargetEvaluator.Evaluate(targetParam, context);

            // [디버깅]
            DebugHelper.LogEffect("Mana Gain", $"마나 {finalAmount} 회복 시도");

            foreach (Player target in targets)
            {
                int oldMana = target.Mana;
                target.ManaGain(finalAmount);
                // [디버깅] 실제 변화 확인
                DebugHelper.LogEffect("Result", $"{target.Name} Mana: {oldMana} -> {target.Mana}");
            }
            onComplete?.Invoke(); // 효과 실행이 완료되었음을 알리는 콜백 호출
        }

        // ★ 깊은 복사 구현체
        public ICardEffect Clone()
        {
            var clone = new ManaGainEffect(); // 자기 자신의 새 인스턴스 생성
            clone.Initialize(this._cachedParams); // 기억해둔 똑같은 재료로 초기화
            return clone;
        }
    }
}