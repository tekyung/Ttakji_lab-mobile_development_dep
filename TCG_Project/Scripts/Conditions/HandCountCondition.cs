using System;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;
using TCG_Project.Scripts.Systems; // FormulaEvaluator 사용

namespace TCG_Project.Scripts.Conditions
{
    public class HandCountCondition : ICardCondition
    {
        private string targetType;
        private string op;
        private object valueParam; // [변경] int -> object

        public void Initialize(Dictionary<string, object> parameters)
        {
            targetType = parameters["target"].ToString();
            op = parameters["operator"].ToString();

            if (parameters.ContainsKey("value"))
                valueParam = parameters["value"];
            else
                valueParam = 0;
        }

        public bool IsMet(GameContext context)
        {
            // 1. 타겟 핸드 수 가져오기
            List<Player> targets = TargetEvaluator.Evaluate(targetType, context);
            if (targets.Count == 0) return false;
            int handCount = targets[0].Hand.Count;

            // 2. [핵심] 비교할 값 계산
            int compareValue = FormulaEvaluator.Evaluate(valueParam, context);

            // 3. 비교 연산
            switch (op)
            {
                case "Greater": return handCount > compareValue;
                case "GreaterOrEqual": return handCount >= compareValue;
                case "Less": return handCount < compareValue;
                case "LessOrEqual": return handCount <= compareValue;
                case "Equal": return handCount == compareValue;
                case "NotEqual": return handCount != compareValue;
                default: return false;
            }
        }
    }
}