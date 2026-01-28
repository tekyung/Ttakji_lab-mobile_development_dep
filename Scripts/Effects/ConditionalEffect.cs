using System;
using Newtonsoft.Json.Linq; // JObject 사용
using TCG_Project.Scripts.Systems; // CardFactory 접근
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;

namespace TCG_Project.Scripts.Effects
{
    public class ConditionalEffect : ICardEffect
    {
        private ICardCondition condition;
        private ICardEffect successEffect;

        public void Initialize(Dictionary<string, object> parameters)
        {
            // Dictionary -> JObject로 다시 변환 (중첩된 구조를 쉽게 다루기 위함)
            var paramJson = JObject.FromObject(parameters);

            // 1. 조건 생성 (CardFactory 위임)
            var condData = (JObject)paramJson["condition"];
            string condType = condData["type"].ToString();
            var condParams = (JObject)condData["params"];

            this.condition = CardFactory.CreateCondition(condType, condParams);

            // 2. 효과 생성 (CardFactory 위임 -> 여기서 재귀 발생 가능)
            var effectData = (JObject)paramJson["successEffect"];
            // Factory의 헬퍼 메서드 재사용
            this.successEffect = CardFactory.CreateEffectFromJObject(effectData);
        }

        public void Execute(GameContext context)
        {
            if (condition != null && condition.IsMet(context))
            {
                successEffect?.Execute(context);
            }
        }
    }
}