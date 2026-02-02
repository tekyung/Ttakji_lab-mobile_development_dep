using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;

namespace TCG_Project.Scripts.Systems
{
    public static class CardFactory
    {
        public static List<Card> LoadCardsFromFile(string jsonPath)
        {
            if (!File.Exists(jsonPath)) return new List<Card>();

            var cards = new List<Card>();
            string jsonString = File.ReadAllText(jsonPath);
            JArray cardDataList = JArray.Parse(jsonString);

            foreach (JObject data in cardDataList)
            {
                Card newCard = new Card
                {
                    DataId = data["id"].ToString(),
                    Name = data["name"].ToString(),
                    Cost = (int)data["cost"],
                    Description = data["description"].ToString(),
                    // JSON에 필드가 있으면 읽고, 없으면 null
                    PlayCondition = data["playCondition"] != null ? data["playCondition"].ToString() : null
                };

                // [변경] effects 배열 처리 (id와 args가 있는 구조)
                if (data["effects"] != null)
                {
                    JArray effectsArr = (JArray)data["effects"];
                    foreach (JObject effData in effectsArr)
                    {
                        ICardEffect effect = null;

                        // [CASE 1] "id"가 있는 경우 (Effects.json 참조)
                        if (effData["id"] != null)
                        {
                            string effId = effData["id"].ToString();
                            JObject args = effData["args"] as JObject;
                            effect = EffectFactory.CreateEffect(effId, args);
                        }
                        // [CASE 2] "type"이 있는 경우 (인라인 직접 정의) -> 여기서 c_snipe 처리됨!
                        else if (effData["type"] != null)
                        {
                            string typeName = effData["type"].ToString();
                            JObject parameters = effData["params"] as JObject; // args가 아니라 params임에 주의
                            effect = EffectFactory.CreateEffectByType(typeName, parameters);
                        }

                        // 효과 생성 성공 시 추가
                        if (effect != null)
                        {
                            newCard.AddEffect(effect);
                        }
                        else
                        {
                            // 디버깅용 로그 (어떤 카드에서 실패했는지 알면 편함)
                            Console.WriteLine($"[Warning] 효과 생성 실패. Card: {newCard.Name}");
                        }
                    }
                }

                cards.Add(newCard);
            }

            return cards;
        }
    }
}