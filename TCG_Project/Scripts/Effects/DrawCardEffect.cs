using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;
using TCG_Project.Scripts.Systems;

namespace TCG_Project.Scripts.Effects
{
    public class DrawCardEffect : ICardEffect
    {
        private object countParam;
        private object targetParam;

        public void Initialize(Dictionary<string, object> parameters)
        {
            countParam = parameters["count"];
            targetParam = parameters.ContainsKey("target") ? parameters["target"] : "Self";
        }

        public void Execute(GameContext context)
        {
            int count = FormulaEvaluator.Evaluate(countParam, context);
            List<Player> targets = TargetEvaluator.Evaluate(targetParam, context);

            foreach (Player target in targets)
            {
               target.DrawCard(count);
            }
        }
    }
}