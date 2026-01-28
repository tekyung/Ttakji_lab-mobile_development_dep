using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;

namespace TCG_Project.Scripts.Conditions // 이 부분이 필수입니다!
{
    //  조건 비교 효과 예시: 플레이어 체력 비교
    public class CompareHealthCondition : ICardCondition
    {
        private string comparisonOperator; // "LowerThan", "HigherThan"
        private string targetType;         // "Opponent"

        public void Initialize(Dictionary<string, object> parameters)
        {
            comparisonOperator = parameters["operator"].ToString();
            targetType = parameters["target"].ToString();
        }

        public bool IsMet(GameContext context)
        {
            int myHp = context.Player.Health;
            int targetHp = (targetType == "Opponent") ? context.Opponent.Health : context.Player.Health;

            switch (comparisonOperator)
            {
                case "LowerThan": return myHp < targetHp;
                case "HigherThan": return myHp > targetHp;
                case "Equal": return myHp == targetHp;
                default: return false;
            }
        }
    }
}

