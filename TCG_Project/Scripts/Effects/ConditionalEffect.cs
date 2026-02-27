using System; // Action 델리게이트 사용을 위해 필수
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
        private ICardEffect failEffect; // 실패 시 실행할 효과

        public void Initialize(Dictionary<string, object> parameters)
        {
            var paramJson = JObject.FromObject(parameters);

            // 1. 조건 생성
            if (paramJson["condition"] != null)
            {
                var condData = (JObject)paramJson["condition"];
                string type = condData["type"].ToString();
                JObject p = (JObject)condData["params"];
                this.condition = EffectFactory.CreateCondition(type, p);
            }

            // 2. 성공 효과
            if (paramJson["successEffectId"] != null)
            {
                this.successEffect = EffectFactory.CreateEffect(paramJson["successEffectId"].ToString());
            }

            // 3. 실패 효과 (Else)
            if (paramJson["failEffectId"] != null)
            {
                this.failEffect = EffectFactory.CreateEffect(paramJson["failEffectId"].ToString());
            }
        }

        public void Execute(GameContext context, Action onComplete)
        {
            // 1. 조건식이 아예 없다면 무조건 성공(True)으로 간주
            bool isMet = (condition == null) || condition.IsMet(context);

            if (isMet)
            {
                // 성공 효과가 등록되어 있으면 실행하고, 끝나면 onComplete를 넘겨서 호출하게 함
                if (successEffect != null)
                {
                    successEffect.Execute(context, onComplete);
                }
                else
                {
                    // 등록된 효과가 없으면 즉시 콜백 반환 (흐름이 멈추지 않도록 방어)
                    onComplete?.Invoke();
                }
            }
            else
            {
                // 실패 효과가 등록되어 있으면 실행하고, 끝나면 onComplete를 넘겨서 호출하게 함
                if (failEffect != null)
                {
                    failEffect.Execute(context, onComplete);
                }
                else
                {
                    onComplete?.Invoke();
                }
            }
        }
    }
}