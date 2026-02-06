using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json; // 혹은 유니티 JsonUtility 사용
using System.IO;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Effects;
using TCG_Project.Scripts.Interfaces;

namespace TCG_Project.Scripts.Systems
{
    public class GameDataManager
    {
        // 팀원의 Raw Data 구조체 (내부용)
        private class RawCard { public int id; public string name; public string type; public string skin_res; }
        private class RawUnit { public int id; public int prize; public int arts_cost; public int power; public int on_play_effect_id; }
        private class RawSkill { public int id; public int skill_cost; public int after_use_effect_id; }
        private class RawEffect
        {
            public int id;
            public string effect_function_type; // "Draw", "Gain_Mana", "KillUnit"
            public string effect_target_type;   // "OppentUnit", "OwnDeck"
            public string effect_target_condition; // [조건 필드]
            public int effect_target_condition_value1; // [조건 값]
            public int effect_function_value1;
            public int effect_function_value2;
        }

        // 전체 카드 도감
        public Dictionary<string, Card> AllCards { get; private set; } = new Dictionary<string, Card>();

        public void LoadAllData(string basePath)
        {
            // 1. JSON 파일 읽기 (경로는 환경에 맞춰 수정)
            var cardsData = ReadJson<List<RawCard>>(basePath + "/Card.json");
            var unitsData = ReadJson<List<RawUnit>>(basePath + "/CardUnit.json");
            var skillsData = ReadJson<List<RawSkill>>(basePath + "/CardSkill.json");
            var effectsData = ReadJson<List<RawEffect>>(basePath + "/CardEffect.json");

            // 효과 조회용 딕셔너리
            var effectLookup = effectsData.ToDictionary(e => e.id);
            var unitLookup = unitsData.ToDictionary(u => u.id);
            var skillLookup = skillsData.ToDictionary(s => s.id);

            // 2. 통합 및 변환 (Flattening)
            foreach (var raw in cardsData)
            {
                Card newCard = new Card
                {
                    Id = raw.id.ToString(),
                    Name = raw.name,
                    SkinResource = raw.skin_res
                };

                // 타입별 파싱
                if (raw.type == "Unit" && unitLookup.ContainsKey(raw.id))
                {
                    var u = unitLookup[raw.id];
                    newCard.Type = CardType.Unit;
                    newCard.Power = u.power;
                    newCard.MaxHealth = u.power; // 일단 Power를 체력으로 사용
                    newCard.Health = u.power; // Power가 공격력이자 체력
                    newCard.Prize = u.prize;
                    newCard.AttackCost = u.arts_cost;
                    newCard.Cost = 0; // 유닛 소환 코스트가 JSON에 없음 (0으로 가정)

                    // 출격 효과(OnPlay) 연결
                    if (u.on_play_effect_id != -1 && effectLookup.ContainsKey(u.on_play_effect_id))
                    {
                        var effectObj = ConvertEffect(effectLookup[u.on_play_effect_id]);
                        if (effectObj != null) newCard.Effects.Add(effectObj);
                    }
                }
                else if (raw.type == "Skill" && skillLookup.ContainsKey(raw.id))
                {
                    var s = skillLookup[raw.id];
                    newCard.Type = CardType.Skill;
                    newCard.Cost = s.skill_cost;

                    // 스킬 사용 효과 연결
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

        // 3. 효과 변환기 (Translator)
        // 팀원의 Effect Enum을 우리 엔진의 Effect Class로 변환
        private ICardEffect ConvertEffect(RawEffect raw)
        {
            // [핵심] 타겟 정보 생성 시 '조건(condition)'도 함께 넘김
            var targetInfo = ConvertTarget(raw);

            switch (raw.effect_function_type)
            {
                case "Draw":
                    var drawEffect = new MoveCardEffect();
                    drawEffect.Initialize(new Dictionary<string, object> {
                        { "src", "Deck" }, { "dest", "Hand" },
                        { "count", raw.effect_function_value1 },
                        { "target", targetInfo }
                    });
                    return drawEffect;

                case "Gain_Mana":
                    var manaEffect = new ManaGainEffect();
                    int amount = raw.effect_function_value2 != -1 ? raw.effect_function_value2 : raw.effect_function_value1;
                    manaEffect.Initialize(new Dictionary<string, object> {
                        { "amount", amount },
                        { "target", "ActivePlayer" }
                    });
                    return manaEffect;

                case "KillUnit":
                    var killEffect = new MoveCardEffect();
                    killEffect.Initialize(new Dictionary<string, object> {
                        { "src", "Field" }, { "dest", "Graveyard" },
                        { "target", targetInfo }
                    });
                    return killEffect;

                case "Power_Up":
                    var buffEffect = new ModifyStatEffect();
                    int buffAmt = raw.effect_function_value1 > 0 ? raw.effect_function_value1 : 100;
                    buffEffect.Initialize(new Dictionary<string, object> {
                        { "stat", "Power" },
                        { "amount", buffAmt },
                        { "target", targetInfo }
                    });
                    return buffEffect;

                case "DamegeToUnit":
                    var dmgEffect = new ModifyStatEffect();
                    int dmg = raw.effect_function_value2 != -1 ? raw.effect_function_value2 : raw.effect_function_value1;
                    dmgEffect.Initialize(new Dictionary<string, object> {
                        { "stat", "Health" },
                        { "amount", -dmg },
                        { "target", targetInfo }
                    });
                    return dmgEffect;

                default: return null;
            }
            /*
            switch (raw.effect_function_type)
            {
                case "Draw":
                    var drawEffect = new MoveCardEffect();
                    drawEffect.Initialize(new Dictionary<string, object> {
                        { "src", "Deck" }, { "dest", "Hand" },
                        { "count", raw.effect_function_value1 },
                        { "target", ConvertTarget(raw.effect_target_type) }
                    });
                    return drawEffect;

                case "Gain_Mana":
                    var manaEffect = new ManaGainEffect();
                    // value2가 양수인 경우가 많은 듯 함 (JSON 참고)
                    int amount = raw.effect_function_value2 != -1 ? raw.effect_function_value2 : raw.effect_function_value1;
                    manaEffect.Initialize(new Dictionary<string, object> {
                        { "amount", amount },
                        { "target", ConvertTarget(raw.effect_target_type) }
                    });
                    return manaEffect;

                case "KillUnit":
                    var killEffect = new MoveCardEffect();
                    killEffect.Initialize(new Dictionary<string, object> {
                        { "src", "Field" }, { "dest", "Graveyard" },
                        { "target", ConvertTarget(raw.effect_target_type) }
                    });
                    return killEffect;

                case "Power_Up":
                    var buffEffect = new ModifyStatEffect();
                    buffEffect.Initialize(new Dictionary<string, object> {
                        { "stat", "Power" }, // 혹은 "Health"
                        { "amount", raw.effect_function_value1 },
                        { "target", ConvertTarget(raw.effect_target_type) }
                    });
                    return buffEffect;

                case "DamegeToUnit": // 오타(Damege) 그대로 대응
                    // DamageEffect가 없다면 ModifyStat(Health)으로 대체
                    var dmgEffect = new ModifyStatEffect();
                    int dmg = raw.effect_function_value2 != -1 ? raw.effect_function_value2 : raw.effect_function_value1;
                    dmgEffect.Initialize(new Dictionary<string, object> {
                        { "stat", "Health" },
                        { "amount", -dmg }, // 데미지니까 음수
                        { "target", ConvertTarget(raw.effect_target_type) }
                    });
                    return dmgEffect;

                default:
                    return null;
            }*/
        }

        // [수정] RawEffect 전체를 받아서 조건까지 처리
        private object ConvertTarget(RawEffect raw)
        {
            var dict = new Dictionary<string, object>();

            // 1. 기본 타겟 설정
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

            // 2. 봇을 위해 랜덤 모드 적용
            dict["mode"] = "Random";
            dict["count"] = 1;

            // 3. [신규] 조건(Condition) 매핑
            if (!string.IsNullOrEmpty(raw.effect_target_condition) && raw.effect_target_condition != "None")
            {
                dict["condition"] = raw.effect_target_condition;
                dict["conditionValue"] = raw.effect_target_condition_value1;
            }

            return dict;
        }
        /* [핵심 수정] 봇을 위해 "Manual" 대신 "Random" 사용
        private object ConvertTarget(string enumType)
        {
            switch (enumType)
            {
                case "OppentUnit":
                    return new Dictionary<string, object> {
                        {"controller", "Opponent"}, {"zones", new[]{ "Field" }}, {"mode", "Random"}, {"count", 1}
                    };
                case "OwnUnit":
                    return new Dictionary<string, object> {
                        {"controller", "Self"}, {"zones", new[]{ "Field" }}, {"mode", "Random"}, {"count", 1}
                    };
                case "OwnDeck": return "ActivePlayer";
                case "OppentDeck": return "Opponent";
                case "OwnMana": return "ActivePlayer";
                default: return "Self";
            }
        }

        /* 타겟 변환기 원본
        private object ConvertTarget(string enumType)
        {
            // 우리 엔진의 타겟팅 쿼리(JSON style object)로 변환
            switch (enumType)
            {
                case "OppentUnit":
                    return new Dictionary<string, object> {
                        {"controller", "Opponent"}, {"zones", new[]{ "Field" }}, {"mode", "Manual"}, {"count", 1}
                    };
                case "OwnUnit":
                    return new Dictionary<string, object> {
                        {"controller", "Self"}, {"zones", new[]{ "Field" }}, {"mode", "Manual"}, {"count", 1}
                    };
                case "OwnDeck": return "ActivePlayer"; // 단순화
                case "OppentDeck": return "Opponent";
                case "OwnMana": return "ActivePlayer";
                default: return "Self";
            }
        }
        */

        private T ReadJson<T>(string path)
        {
            if (!File.Exists(path)) return default;
            string json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}