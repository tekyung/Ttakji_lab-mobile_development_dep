using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;

namespace TCG_Project.Scripts.Systems
{
    public static class CardFactoryver2
    {
        // 캐싱용 데이터
        private static JArray unitDataList;
        private static JArray skillDataList;
        private static Dictionary<int, JObject> effectDataMap = new Dictionary<int, JObject>();

        // 1. 모든 JSON 데이터를 메모리에 로드 (게임 시작 시 호출)
        public static void LoadAllData(string basePath)
        {
            // Unit 상세 데이터
            string unitPath = Path.Combine(basePath, "CardUnit.json");
            if (File.Exists(unitPath)) unitDataList = JArray.Parse(File.ReadAllText(unitPath));

            // Skill 상세 데이터
            string skillPath = Path.Combine(basePath, "CardSkill.json");
            if (File.Exists(skillPath)) skillDataList = JArray.Parse(File.ReadAllText(skillPath));

            // Effect 상세 데이터 -> Dictionary로 변환 (검색 속도 향상)
            string effectPath = Path.Combine(basePath, "CardEffect.json");
            if (File.Exists(effectPath))
            {
                var arr = JArray.Parse(File.ReadAllText(effectPath));
                foreach (JObject obj in arr)
                {
                    int id = (int)obj["id"];
                    if (!effectDataMap.ContainsKey(id)) effectDataMap.Add(id, obj);
                }
            }
        }

        // 2. 메인 카드 생성 (Card.json 기반)
        public static List<Card> CreateCards(string cardJsonPath)
        {
            var cards = new List<Card>();
            string json = File.ReadAllText(cardJsonPath);
            JArray basicList = JArray.Parse(json);

            foreach (JObject basicData in basicList)
            {
                int id = (int)basicData["id"];
                string type = basicData["type"].ToString(); // "Unit" or "Skill"

                Card newCard = null;

                if (type == "Unit")
                {
                    newCard = CreateUnitCard(id, basicData);
                }
                else if (type == "Skill")
                {
                    newCard = CreateSkillCard(id, basicData);
                }

                if (newCard != null) cards.Add(newCard);
            }
            return cards;
        }

        // 유닛 카드 조립
        private static UnitCard CreateUnitCard(int id, JObject basicData)
        {
            // CardUnit.json에서 해당 ID 검색
            var detail = unitDataList.FirstOrDefault(x => (int)x["id"] == id);
            if (detail == null) return null;

            UnitCard u = new UnitCard();

            // 기본 정보 매핑
            u.DataId = id.ToString();
            u.Name = basicData["name"].ToString();
            // u.Cost = ... (Unit JSON에는 Cost가 없음, Prize나 ArtsCost로 대체 고려)

            // 유닛 상세 정보 매핑
            u.MaxPower = (int)detail["power"];
            u.CurrentPower = u.MaxPower;
            u.Prize = (int)detail["prize"];
            u.ArtsCost = (int)detail["arts_cost"];

            // *** 효과 주입 (핵심) ***
            int onPlayId = (int)detail["on_play_effect_id"];
            if (onPlayId != -1 && effectDataMap.ContainsKey(onPlayId))
            {
                // 번역기를 통해 효과 생성!
                ICardEffect effect = TeamDataTranslator.Translate(effectDataMap[onPlayId]);
                if (effect != null) u.AddEffect(effect);
            }

            return u;
        }

        // 스킬 카드 조립
        private static Card CreateSkillCard(int id, JObject basicData)
        {
            // CardSkill.json에서 검색
            var detail = skillDataList.FirstOrDefault(x => (int)x["id"] == id);
            if (detail == null) return null;

            Card s = new Card();
            s.DataId = id.ToString();
            s.Name = basicData["name"].ToString();
            s.Cost = (int)detail["skill_cost"]; // 스킬은 코스트가 있음

            // 사용 효과 주입
            int effectId = (int)detail["after_use_effect_id"];
            if (effectId != -1 && effectDataMap.ContainsKey(effectId))
            {
                ICardEffect effect = TeamDataTranslator.Translate(effectDataMap[effectId]);
                if (effect != null) s.AddEffect(effect);
            }

            return s;
        }
    }
}