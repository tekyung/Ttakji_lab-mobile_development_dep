using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TCG_Project.Scripts.Effects;
using TCG_Project.Scripts.Interfaces;

namespace TCG_Project.Scripts.Systems
{
    // 팀원들의 정적 데이터를 개발자의 동적 로직으로 변환하는 어댑터
    public static class TeamDataTranslator
    {
        // [매핑 테이블 1] 타겟 타입 (Enum.json 참고)
        // 팀원: "OppentUnit" (Index 1) -> 나: { controller: "Opponent", zones: ["Field"] }
        private static readonly Dictionary<string, JObject> TargetMap = new Dictionary<string, JObject>
        {
            { "OppentUnit", JObject.Parse(@"{ 'controller': 'Opponent', 'zones': ['Field'] }") },
            { "OwnUnit",    JObject.Parse(@"{ 'controller': 'Self', 'zones': ['Field'] }") },
            { "OwnDeck",    JObject.Parse(@"{ 'controller': 'Self', 'zones': ['Deck'] }") },
            { "OppentDeck", JObject.Parse(@"{ 'controller': 'Opponent', 'zones': ['Deck'] }") },
            { "OwnHand",    JObject.Parse(@"{ 'controller': 'Self', 'zones': ['Hand'] }") },
            { "OppentHand", JObject.Parse(@"{ 'controller': 'Opponent', 'zones': ['Hand'] }") },
            // Mana 등은 별도 처리
        };

        // [핵심 함수] 팀원의 Effect 데이터를 받아 나의 ICardEffect로 변환
        public static ICardEffect Translate(JObject teamEffectData)
        {
            string funcType = teamEffectData["effect_function_type"].ToString();

            // 수치 파싱 (팀원은 value1, value2에 값을 나눠 담음)
            int val1 = (int)teamEffectData["effect_function_value1"];
            int val2 = (int)teamEffectData["effect_function_value2"];

            // 1. 타겟 정보 변환
            string targetTypeStr = teamEffectData["effect_target_type"].ToString();
            JObject targetParam = GetTargetParam(targetTypeStr);

            // 2. 조건 정보 변환 ("PowerUnderOrEqual" -> "target.Power <= 300")
            string conditionFormula = TranslateCondition(
                teamEffectData["effect_target_condition"].ToString(),
                (int)teamEffectData["effect_target_condition_value1"]
            );

            // 조건이 있다면 TargetSelector의 'filter'에 주입해야 함
            if (!string.IsNullOrEmpty(conditionFormula) && targetParam != null)
            {
                targetParam["filter"] = conditionFormula;
            }

            // 3. 기능(Function) 별 효과 생성
            switch (funcType)
            {
                case "DamegeToUnit": // 오타(Damege) 대응
                case "DamageToUnit":
                    var dmgParams = new Dictionary<string, object>
                    {
                        { "amount", val2 }, // Damage는 value2에 있다고 가정 (JSON 예시 기반)
                        { "target", targetParam }
                    };
                    var eff = new DamageEffect();
                    eff.Initialize(dmgParams);
                    return eff;

                case "KillUnit":
                    // Kill -> MoveCard(Field -> Graveyard)
                    var killParams = new Dictionary<string, object>
                    {
                        { "src", "Field" },
                        { "dest", "Graveyard" },
                        { "target", targetParam }
                    };
                    var killEff = new MoveCardEffect();
                    killEff.Initialize(killParams);
                    return killEff;

                case "Draw":
                    // Draw -> MoveCard(Deck -> Hand)
                    var drawParams = new Dictionary<string, object>
                    {
                        { "src", "Deck" },
                        { "dest", "Hand" },
                        { "count", val1 }, // 드로우 장수
                        { "srcTarget", "Self" },
                        { "destTarget", "Self" }
                    };
                    var drawEff = new MoveCardEffect();
                    drawEff.Initialize(drawParams);
                    return drawEff;

                case "Gain_Mana":
                    var manaParams = new Dictionary<string, object>
                    {
                        { "amount", val2 }, // value2 사용 가정
                        { "target", "Self" }
                    };
                    var manaEff = new ManaGainEffect();
                    manaEff.Initialize(manaParams);
                    return manaEff;

                case "Power_Up":
                    // 스탯 변경
                    var buffParams = new Dictionary<string, object>
                    {
                        { "stat", "Power" }, // UnitCard에 Power가 있다고 가정
                        { "amount", val1 > 0 ? val1 : val2 }, // 값이 어디 있는지 확인 필요
                        { "target", targetParam }
                    };
                    var buffEff = new ModifyStatEffect();
                    buffEff.Initialize(buffParams);
                    return buffEff;

                default:
                    return null;
            }
        }

        private static JObject GetTargetParam(string teamKey)
        {
            if (TargetMap.ContainsKey(teamKey))
            {
                return (JObject)TargetMap[teamKey].DeepClone();
            }
            // 매핑 없는 경우 기본값
            return JObject.Parse(@"{ 'controller': 'Opponent', 'zones': ['Field'] }");
        }

        // [조건 통역] "PowerUnderOrEqual", 300 -> "target.Power <= 300"
        private static string TranslateCondition(string conditionType, int value)
        {
            switch (conditionType)
            {
                case "PowerUnderOrEqual":
                    return $"target.CurrentPower <= {value}"; // UnitCard 프로퍼티명 매칭
                case "DeckMoreOrEqual":
                    // 타겟 필터라기보단 발동 조건에 가까울 수 있음. 
                    // 하지만 TargetSelector filter로 쓴다면 "target.Deck.Count >= value" 가 됨.
                    // 만약 이게 'Global Condition'이라면 별도 처리가 필요하지만 일단 필터로 처리
                    return null;
                default:
                    return null;
            }
        }
    }
}