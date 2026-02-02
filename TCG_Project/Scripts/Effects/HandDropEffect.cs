using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;
using TCG_Project.Scripts.Systems;

namespace TCG_Project.Scripts.Effects
{
    public class HandDropEffect : ICardEffect
    {
        private object amountParam;
        private object targetParam;

        public void Initialize(Dictionary<string, object> parameters)
        {
            amountParam = parameters["amount"];
            targetParam = parameters.ContainsKey("target") ? parameters["target"] : "Self";
        }

        public void Execute(GameContext context)
        {
            int amount = FormulaEvaluator.Evaluate(amountParam, context);
            List<Player> targets = TargetEvaluator.Evaluate(targetParam, context);

            foreach (Player target in targets)
            {
                // 랜덤하게 버리거나 앞장부터 버리는 로직 (여기선 단순화하여 0번 인덱스부터)
                // target.DropHand(amount);
                
            }
        }
    }
}