using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq; // JObject, JArray 등을 쓰기 위해 필요

// 네임스페이스는 프로젝트 구조에 맞게 수정하세요.
namespace TCG_Project.Scripts.Systems
{
    using TCG_Project.Scripts.Core;
    using TCG_Project.Scripts.Interfaces;
    using TCG_Project.Scripts.Effects;
    using TCG_Project.Scripts.Conditions;

    public static class CardFactory
    {
        // 1. 효과(Effect) 등록소: 문자열 키와 클래스 타입을 매핑
        private static readonly Dictionary<string, Type> effectRegistry = new Dictionary<string, Type>
        {
            { "Damage", typeof(DamageEffect) },
            { "Heal", typeof(HealEffect) },
            { "DrawCard", typeof(DrawCardEffect) },
            { "Conditional", typeof(ConditionalEffect) } // 조건부 효과 추가
        };

        // 2. 조건(Condition) 등록소
        private static readonly Dictionary<string, Type> conditionRegistry = new Dictionary<string, Type>
        {
            { "CompareHealth", typeof(CompareHealthCondition) }
        };

        /// <summary>
        /// JSON 파일 경로를 받아 Card 리스트를 반환하는 메인 함수
        /// </summary>
        public static List<Card> LoadCardsFromFile(string jsonPath)
        {
            if (!File.Exists(jsonPath))
            {
                Console.WriteLine($"[Error] 파일을 찾을 수 없습니다: {jsonPath}");
                return new List<Card>();
            }

            var cards = new List<Card>();
            string jsonString = File.ReadAllText(jsonPath);

            // 전체 JSON을 JArray로 파싱 (JObject보다 유연함)
            JArray cardDataList = JArray.Parse(jsonString);

            foreach (JObject data in cardDataList)
            {
                // 기본 정보 생성
                Card newCard = new Card
                {
                    Name = data["name"].ToString(),
                    Cost = (int)data["cost"],
                    Description = data["description"].ToString()
                };

                // effects 배열 파싱
                JArray effectsArray = (JArray)data["effects"];
                foreach (JObject effectData in effectsArray)
                {
                    // 아래의 헬퍼 메서드를 사용해 효과 생성
                    ICardEffect effect = CreateEffectFromJObject(effectData);
                    if (effect != null)
                    {
                        newCard.AddEffect(effect);
                    }
                }

                cards.Add(newCard);
            }

            return cards;
        }

        /// <summary>
        /// JObject(JSON 조각)를 받아 적절한 ICardEffect 객체를 생성하고 초기화하여 반환
        /// ConditionalEffect 내부에서도 이 함수를 호출하여 중첩 효과를 만듦.
        /// </summary>
        public static ICardEffect CreateEffectFromJObject(JObject effectData)
        {
            string typeName = effectData["type"].ToString();

            // params가 없는 경우를 대비해 빈 객체 처리
            JObject parameters = effectData["params"] as JObject ?? new JObject();

            return CreateEffect(typeName, parameters);
        }

        /// <summary>
        /// 타입 이름과 파라미터(JObject)를 받아 효과 인스턴스 생성
        /// </summary>
        public static ICardEffect CreateEffect(string typeName, JObject parameters)
        {
            if (effectRegistry.ContainsKey(typeName))
            {
                // 1. 리플렉션으로 객체 생성
                ICardEffect effect = (ICardEffect)Activator.CreateInstance(effectRegistry[typeName]);

                // 2. JObject를 Dictionary로 변환하여 Initialize 호출
                // (기존 인터페이스와의 호환성을 위해 Dictionary로 변환)
                var paramDict = parameters.ToObject<Dictionary<string, object>>();

                effect.Initialize(paramDict);
                return effect;
            }
            else
            {
                Console.WriteLine($"[Warning] 알 수 없는 효과 타입입니다: {typeName}");
                return null;
            }
        }

        /// <summary>
        /// 조건(Condition)을 생성하는 헬퍼 메서드
        /// </summary>
        public static ICardCondition CreateCondition(string typeName, JObject parameters)
        {
            if (conditionRegistry.ContainsKey(typeName))
            {
                ICardCondition condition = (ICardCondition)Activator.CreateInstance(conditionRegistry[typeName]);

                var paramDict = parameters.ToObject<Dictionary<string, object>>();
                condition.Initialize(paramDict);

                return condition;
            }
            else
            {
                Console.WriteLine($"[Warning] 알 수 없는 조건 타입입니다: {typeName}");
                return null;
            }
        }
    }
}