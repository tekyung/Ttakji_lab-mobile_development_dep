using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;
using TCG_Project.Scripts.Systems;

namespace TCG_Project.Scripts.Effects
{
    public class ConditionalEffect : ICardEffect
    {
        private ICardCondition condition;
        private ICardEffect successEffect;
        private ICardEffect failEffect; // [신규] 실패 시 실행할 효과

        public void Initialize(Dictionary<string, object> parameters)
        {
            var paramJson = JObject.FromObject(parameters);

            // 1. 조건 생성
            if (paramJson["condition"] != null)
            {
                var condData = (JObject)paramJson["condition"];
                // 조건 자체가 문자열 수식인 경우와 객체인 경우 구분 가능하지만, 
                // 현재 구조에서는 ConditionEvaluator가 문자열을 처리하므로 파라미터 구조에 맞춤
                // (여기서는 기존 ConditionFactory 로직을 따름)
                string type = condData["type"].ToString();
                JObject p = (JObject)condData["params"];
                this.condition = EffectFactory.CreateCondition(type, p);
            }

            // 2. 성공 효과
            if (paramJson["successEffectId"] != null)
            {
                this.successEffect = EffectFactory.CreateEffect(paramJson["successEffectId"].ToString());
            }

            // 3. [신규] 실패 효과 (Else)
            if (paramJson["failEffectId"] != null)
            {
                this.failEffect = EffectFactory.CreateEffect(paramJson["failEffectId"].ToString());
            }
        }

        public void Execute(GameContext context)
        {
            if (condition != null)
            {
                if (condition.IsMet(context))
                {
                    successEffect?.Execute(context);
                }
                else
                {
                    // 조건 불만족 시 실행
                    failEffect?.Execute(context);
                }
            }
        }
    }
}