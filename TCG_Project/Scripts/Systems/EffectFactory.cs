using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TCG_Project.Scripts.Interfaces;
using TCG_Project.Scripts.Effects;
using TCG_Project.Scripts.Conditions;

namespace TCG_Project.Scripts.Systems
{
    // 효과 정의 데이터를 저장할 내부 클래스
    public class EffectData
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public JObject Params { get; set; }
    }

    public static class EffectFactory
    {
        // ID로 효과 데이터를 찾기 위한 캐시
        private static Dictionary<string, EffectData> effectDataMap = new Dictionary<string, EffectData>();

        // 클래스 타입 레지스트리 (CardFactory에서 가져옴)
        private static readonly Dictionary<string, Type> effectTypeRegistry = new Dictionary<string, Type>
        {
            { "Damage", typeof(DamageEffect) },
            { "Heal", typeof(HealEffect) },
            { "DrawCard", typeof(DrawCardEffect) },
            { "HandDrop", typeof(HandDropEffect) },
            { "ManaGain", typeof(ManaGainEffect) },
            { "Conditional", typeof(ConditionalEffect) }
        };

        private static readonly Dictionary<string, Type> conditionTypeRegistry = new Dictionary<string, Type>
        {
            { "CompareHealth", typeof(CompareHealthCondition) },
            { "HandCount", typeof(HandCountCondition) }
        };
        
        public static void LoadEffects(string jsonPath)
        {
            if (!File.Exists(jsonPath)) throw new FileNotFoundException("Effects.json not found");

            string json = File.ReadAllText(jsonPath);
            var list = JsonConvert.DeserializeObject<List<EffectData>>(json);

            effectDataMap.Clear();
            foreach (var data in list)
            {
                if (effectDataMap.ContainsKey(data.Id))
                    Console.WriteLine($"[Warning] 중복된 효과 ID: {data.Id}");
                else
                    effectDataMap.Add(data.Id, data);
            }
            Console.WriteLine($"[System] 효과 데이터 {effectDataMap.Count}개 로드 완료.");
        }

        // [변경] overrides 파라미터 추가
        public static ICardEffect CreateEffect(string effectId, JObject overrides = null)
        {
            if (!effectDataMap.ContainsKey(effectId)) return null;

            EffectData data = effectDataMap[effectId];

            // 1. 기본 파라미터 복사 (깊은 복사 필요)
            JObject finalParams = (JObject)data.Params.DeepClone();

            // 2. 오버라이드 적용 (카드가 보낸 args로 덮어쓰기)
            if (overrides != null)
            {
                foreach (var prop in overrides)
                {
                    finalParams[prop.Key] = prop.Value;
                }
            }

            return CreateEffectInstance(data.Type, finalParams);
        }

        // ID를 받아 새로운 효과 인스턴스를 생성
        public static ICardEffect CreateEffect(string effectId)
        {
            if (!effectDataMap.ContainsKey(effectId))
            {
                Console.WriteLine($"[Error] 존재하지 않는 효과 ID: {effectId}");
                return null;
            }

            EffectData data = effectDataMap[effectId];
            return CreateEffectInstance(data.Type, data.Params);
        }

        // 내부 생성 로직
        private static ICardEffect CreateEffectInstance(string typeName, JObject parameters)
        {
            if (effectTypeRegistry.ContainsKey(typeName))
            {
                ICardEffect effect = (ICardEffect)Activator.CreateInstance(effectTypeRegistry[typeName]);
                effect.Initialize(parameters.ToObject<Dictionary<string, object>>());
                return effect;
            }
            return null;
        }

        // 조건 생성 (ConditionalEffect 등에서 사용)
        public static ICardCondition CreateCondition(string typeName, JObject parameters)
        {
            if (conditionTypeRegistry.ContainsKey(typeName))
            {
                ICardCondition cond = (ICardCondition)Activator.CreateInstance(conditionTypeRegistry[typeName]);
                cond.Initialize(parameters.ToObject<Dictionary<string, object>>());
                return cond;
            }
            return null;
        }
    }
}