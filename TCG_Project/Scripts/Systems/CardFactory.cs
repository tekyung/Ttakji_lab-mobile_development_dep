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
                        string effId = effData["id"].ToString();

                        // args가 있으면 가져오고 없으면 null
                        JObject args = effData["args"] as JObject;

                        // Factory에 오버라이드 정보 전달
                        ICardEffect effect = EffectFactory.CreateEffect(effId, args);

                        if (effect != null)
                        {
                            newCard.AddEffect(effect);
                        }
                    }
                }

                cards.Add(newCard);
            }

            return cards;
        }
    }
}