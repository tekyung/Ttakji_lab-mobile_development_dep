using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System.IO;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Effects;
using TCG_Project.Scripts.Interfaces;

namespace TCG_Project.Scripts.Systems
{
    public class GameDataManager
    {
        // 1. Raw Data 클래스 (내부 데이터용)
        private class RawCard { public int id; public string name; public string type; public string skin_res; }

        private class RawUnit
        {
            public int id; public int prize; public int arts_cost; public int power;
            public int on_play_effect_id;
            public string desc;
            public string on_play_condition_type;
            public int on_play_condition_value1;
        }

        private class RawSkill
        {
            public int id; public int skill_cost;
            public int after_use_effect_id;
            public string desc;
            public string use_condition_type;
            public int use_condition_value1;
        }

        private class RawEffect
        {
            public int id;
            public string effect_function_type;
            public string effect_target_type;
            public string effect_target_condition;
            public int effect_target_condition_value1;
            public int effect_function_value1;
            public int effect_function_value2;
        }

        // [신규] JSON 구조에 맞춘 래퍼 클래스 (상자 역할)
        private class CardDataWrapper { public List<RawCard> Card; }
        private class UnitDataWrapper { public List<RawUnit> CardUnit; }
        private class SkillDataWrapper { public List<RawSkill> CardSkill; }
        private class EffectDataWrapper { public List<RawEffect> CardEffect; }

        public Dictionary<string, Card> AllCards { get; private set; } = new Dictionary<string, Card>();

        public void LoadAllData(string basePath)
        {
            // 1. 래퍼 클래스로 먼저 읽어들이기 (Object -> Wrapper)
            var cardsWrapper = ReadJson<CardDataWrapper>(basePath + "/Card.json");
            var unitsWrapper = ReadJson<UnitDataWrapper>(basePath + "/CardUnit.json");
            var skillsWrapper = ReadJson<SkillDataWrapper>(basePath + "/CardSkill.json");
            var effectsWrapper = ReadJson<EffectDataWrapper>(basePath + "/CardEffect.json");

            // 2. 래퍼 안에서 실제 리스트 꺼내기 (null 체크 포함)
            var cardsData = cardsWrapper?.Card ?? new List<RawCard>();
            var unitsData = unitsWrapper?.CardUnit ?? new List<RawUnit>();
            var skillsData = skillsWrapper?.CardSkill ?? new List<RawSkill>();
            var effectsData = effectsWrapper?.CardEffect ?? new List<RawEffect>();

            var effectLookup = effectsData.ToDictionary(e => e.id);
            var unitLookup = unitsData.ToDictionary(u => u.id);
            var skillLookup = skillsData.ToDictionary(s => s.id);

            foreach (var raw in cardsData)
            {
                Card newCard = new Card
                {
                    Id = raw.id.ToString(),
                    Name = raw.name,
                    SkinResource = raw.skin_res
                };

                // 유닛 처리
                if (raw.type == "Unit" && unitLookup.ContainsKey(raw.id))
                {
                    var u = unitLookup[raw.id];
                    newCard.Type = CardType.Unit;
                    newCard.Power = u.power;
                    newCard.MaxHealth = u.power;
                    newCard.Health = u.power;
                    newCard.Prize = u.prize;
                    newCard.AttackCost = u.arts_cost;
                    newCard.Cost = 0;
                    newCard.Description = u.desc;

                    // 유닛 효과 & 조건 연결
                    if (u.on_play_effect_id != -1 && effectLookup.ContainsKey(u.on_play_effect_id))
                    {
                        var extraParams = new Dictionary<string, object>();
                        if (!string.IsNullOrEmpty(u.on_play_condition_type) && u.on_play_condition_type != "None")
                        {
                            extraParams["triggerCondition"] = GetConditionFormula(u.on_play_condition_type, u.on_play_condition_value1);
                        }

                        var effectObj = ConvertEffect(effectLookup[u.on_play_effect_id], extraParams);
                        if (effectObj != null) newCard.Effects.Add(effectObj);
                    }
                }
                // 스킬 처리
                else if (raw.type == "Skill" && skillLookup.ContainsKey(raw.id))
                {
                    var s = skillLookup[raw.id];
                    newCard.Type = CardType.Skill;
                    newCard.Cost = s.skill_cost;
                    newCard.Description = s.desc;

                    // 스킬 발동 조건 변환
                    if (!string.IsNullOrEmpty(s.use_condition_type) && s.use_condition_type != "None")
                    {
                        newCard.PlayCondition = GetConditionFormula(s.use_condition_type, s.use_condition_value1);
                    }

                    if (s.after_use_effect_id != -1 && effectLookup.ContainsKey(s.after_use_effect_id))
                    {
                        var effectObj = ConvertEffect(effectLookup[s.after_use_effect_id]);
                        if (effectObj != null) newCard.Effects.Add(effectObj);
                    }
                }
                AllCards[newCard.Id] = newCard;
            }
            System.Console.WriteLine($"[System] 카드 데이터 {AllCards.Count}장 로드 완료.");
        }

        private string GetConditionFormula(string type, int val1)
        {
            switch (type)
            {
                case "Draw":
                    return "activePlayer.Deck.Count > 0";

                // 1. 애벌레용 (덱에 카드가 있는가?)
                case "DeckNotEmpty":
                    return "DeckNotEmpty"; // ConditionEvaluator에서 처리할 키워드 반환

                // 2. 픽시드래곤/폭탄벌용 (적 유닛이 있는가?)
                case "EnemyUnitExist":
                    return "EnemyUnitExist";

                default:
                    return null;
            }
        }

        private ICardEffect ConvertEffect(RawEffect raw, Dictionary<string, object> extraParams = null)
        {
            var targetInfo = ConvertTarget(raw);
            var finalParams = new Dictionary<string, object>();
            if (extraParams != null) foreach (var kvp in extraParams) finalParams[kvp.Key] = kvp.Value;
            finalParams["target"] = targetInfo;

            switch (raw.effect_function_type)
            {
                case "Draw":
                    var drawEffect = new MoveCardEffect();
                    finalParams["src"] = "Deck";
                    finalParams["dest"] = "Hand";
                    finalParams["count"] = raw.effect_function_value1;
                    drawEffect.Initialize(finalParams);
                    return drawEffect;

                case "Gain_Mana":
                    var manaEffect = new ManaGainEffect();
                    int manaAmt = raw.effect_function_value2 != -1 ? raw.effect_function_value2 : raw.effect_function_value1;
                    finalParams["amount"] = manaAmt;
                    finalParams["target"] = "ActivePlayer";
                    manaEffect.Initialize(finalParams);
                    return manaEffect;

                case "KillUnit":
                    var killEffect = new MoveCardEffect();
                    finalParams["src"] = "Field";
                    finalParams["dest"] = "Graveyard";
                    if (raw.effect_function_value1 != 0) finalParams["prizeOnKill"] = raw.effect_function_value1;
                    killEffect.Initialize(finalParams);
                    return killEffect;

                case "Power_Up":
                    var buffEffect = new ModifyStatEffect();
                    int buffAmt = raw.effect_function_value1 > 0 ? raw.effect_function_value1 : raw.effect_function_value2;
                    if (buffAmt == 0) buffAmt = 100;

                    finalParams["stat"] = "Power";
                    finalParams["amount"] = buffAmt;
                    buffEffect.Initialize(finalParams);
                    return buffEffect;

                case "DamegeToUnit":
                case "DamageToUnit":
                    var dmgEffect = new ModifyStatEffect();
                    int dmg = raw.effect_function_value2 != -1 ? raw.effect_function_value2 : raw.effect_function_value1;
                    finalParams["stat"] = "Power";
                    finalParams["amount"] = -dmg;
                    if (raw.effect_function_value1 != 0) finalParams["prizeOnKill"] = raw.effect_function_value1;
                    dmgEffect.Initialize(finalParams);
                    return dmgEffect;

                default: return null;
            }
        }

        private object ConvertTarget(RawEffect raw)
        {
            var dict = new Dictionary<string, object>();

            switch (raw.effect_target_type)
            {
                case "OppentUnit":
                    dict["controller"] = "Opponent";
                    dict["zones"] = new[] { "Field" };
                    break;
                case "OwnUnit":
                    dict["controller"] = "Self";
                    dict["zones"] = new[] { "Field" };
                    break;
                case "OwnDeck": return "ActivePlayer";
                case "OppentDeck": return "Opponent";
                case "OwnMana": return "ActivePlayer";
                default: return "Self";
            }

            dict["mode"] = "HighestPower";
            dict["count"] = 1;

            if (!string.IsNullOrEmpty(raw.effect_target_condition) && raw.effect_target_condition != "None")
            {
                string cond = raw.effect_target_condition;
                //if (cond == "DeckMoreOrEqual") cond = "DeckHighOrEqual";

                dict["condition"] = cond;
                dict["conditionValue"] = raw.effect_target_condition_value1;
            }

            return dict;
        }

        private T ReadJson<T>(string path)
        {
            if (!File.Exists(path)) return default;
            string json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}