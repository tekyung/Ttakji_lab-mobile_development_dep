using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;
using TCG_Project.Scripts.Systems;

namespace TCG_Project.Scripts.Effects
{
    public class DamageEffect : ICardEffect
    {
        private object amountParam;
        private object targetParam; // [변경] string -> object (유연성)

        public void Initialize(Dictionary<string, object> parameters)
        {
            amountParam = parameters["amount"];
            // 타겟 파라미터가 없으면 기본값 'Opponent' (안전장치)
            targetParam = parameters.ContainsKey("target") ? parameters["target"] : "Opponent";
        }

        public void Execute(GameContext context)
        {
            int finalAmount = FormulaEvaluator.Evaluate(amountParam, context);

            // [핵심] 타겟 판별기를 통해 리스트를 받아옴 (1명 또는 다수)
            List<Player> targets = TargetEvaluator.Evaluate(targetParam, context);

            foreach (Player target in targets)
            {
                target.TakeDamage(finalAmount);
            }
        }
    }
}