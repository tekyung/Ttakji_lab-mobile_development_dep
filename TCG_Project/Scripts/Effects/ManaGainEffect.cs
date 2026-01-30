using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;
using TCG_Project.Scripts.Systems;

namespace TCG_Project.Scripts.Effects
{
    public class ManaGainEffect : ICardEffect
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
            int finalAmount = FormulaEvaluator.Evaluate(amountParam, context);
            List<Player> targets = TargetEvaluator.Evaluate(targetParam, context);

            foreach (Player target in targets)
            {
                target.ManaGain(finalAmount);
            }
        }
    }
}