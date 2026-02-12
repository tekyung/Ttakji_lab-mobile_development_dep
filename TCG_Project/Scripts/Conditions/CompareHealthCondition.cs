using System;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;
using TCG_Project.Scripts.Systems; // FormulaEvaluator 사용

namespace TCG_Project.Scripts.Conditions
{
    public class CompareHealthCondition : ICardCondition
    {
        private string targetType; // "Self" or "Opponent"
        private string op;         // "Greater", "Less", "Equal"...
        private object valueParam; // [변경] int -> object (수식 문자열 지원)

        public void Initialize(Dictionary<string, object> parameters)
        {
            targetType = parameters["target"].ToString();
            op = parameters["operator"].ToString();

            // 값이 없으면 0으로 처리, 있으면 저장 (문자열일 수도 있음)
            if (parameters.ContainsKey("value"))
                valueParam = parameters["value"];
            else
                valueParam = 0;
        }

        public bool IsMet(GameContext context)
        {
            // 1. 비교 대상(주체) 가져오기
            List<Player> targets = TargetEvaluator.Evaluate(targetType, context);
            if (targets.Count == 0) return false;
            Player subject = targets[0]; // 보통 단일 타겟 비교

            int subjectHp = subject.Health;

            // 2. [핵심] 비교할 값 계산 (고정값 OR 수식)
            // 예: "value": 10  -> 10
            // 예: "value": "opponent.Health" -> 상대 체력값
            int compareValue = FormulaEvaluator.Evaluate(valueParam, context);

            // 3. 비교 연산
            switch (op)
            {
                case "Greater": return subjectHp > compareValue;
                case "GreaterOrEqual": return subjectHp >= compareValue;
                case "Less": return subjectHp < compareValue;
                case "LessOrEqual": return subjectHp <= compareValue;
                case "Equal": return subjectHp == compareValue;
                case "NotEqual": return subjectHp != compareValue;
                default: return false;
            }
        }
    }
}