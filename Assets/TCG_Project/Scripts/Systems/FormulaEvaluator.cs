using System;
using System.Data;
using Newtonsoft.Json.Linq; // JObject 사용 필수
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers; // EventManager 사용을 위해 추가

namespace TCG_Project.Scripts.Systems
{
    /// <summary>
    /// JSON으로 정의된 수식이나 조건부 객체를 평가하여 최종 정수(int) 값을 반환하는 계산기.
    /// 데미지량, 회복량, 드로우 장수 등 가변적인 수치를 계산할 때 사용됩니다.
    /// </summary>
    public static class FormulaEvaluator
    {
        public static int Evaluate(object value, GameContext context)
        {
            // 1. 이미 순수 숫자 데이터라면 그대로 반환
            if (value is int || value is long || value is double)
                return Convert.ToInt32(value);

            // 2. 분기형 Selector 객체 처리 (예: 조건이 맞으면 3데미지, 틀리면 1데미지)
            // JSON 예시: { "type": "Select", "condition": "activePlayer.Hand.Count > 3", "trueValue": 3, "falseValue": 1 }
            if (value is JObject obj)
            {
                if (obj["type"]?.ToString() == "Select")
                {
                    // 형제 클래스인 ConditionEvaluator에 참/거짓 판별을 위임
                    string condition = obj["condition"].ToString();
                    bool isTrue = ConditionEvaluator.Evaluate(condition, context);

                    // 조건에 따라 trueValue 또는 falseValue 선택
                    var selectedValue = isTrue ? obj["trueValue"] : obj["falseValue"];

                    // 선택된 값 자체가 또 수식이나 Select 객체일 수 있으므로 재귀(Recursive) 호출
                    return Evaluate(selectedValue, context);
                }
            }

            // 3. 문자열 수학 수식 처리 (예: "activePlayer.Hand.Count * 2")
            string formula = value.ToString();
            formula = ReplaceVariables(formula, context);

            try
            {
                DataTable table = new DataTable();
                var result = table.Compute(formula, "");
                return Convert.ToInt32(result);
            }
            catch (Exception e)
            {
                // ★ 리팩토링: Console.WriteLine 대신 통합 EventManager 사용
                EventManager.OnLogMessage?.Invoke($"<color=red>[Error] 수식 계산 실패: {formula} / {e.Message}</color>");
                return 0; // 에러 시 게임 크래시를 막기 위해 0 반환
            }
        }

        // ─── 변수 치환 메서드 (문자열 수식을 실제 숫자로 변환) ───
        public static string ReplaceVariables(string formula, GameContext context)
        {
            Player p = context.ActivePlayer;
            Player opp = context.GetOpponent(p);

            // [규칙] 더 긴 문자열을 먼저 치환해야 부분 일치 오류를 막을 수 있습니다.
            // (예: "Hand.Count"를 먼저 치환하면 "Hand.CountInclusive"의 앞부분만 잘려나감)

            // 1. Inclusive (현재 사용 중이라 패에서 빠져나간 카드까지 패에 있는 것으로 포함) 변수
            int myHandInc = p.Hand.Count + (p.PlayingCard != null ? 1 : 0);
            formula = formula.Replace("activePlayer.Hand.CountInclusive", myHandInc.ToString());

            // ★ 리팩토링: 상대방이 존재할 때만 계산하여 NullReferenceException 방지
            if (opp != null)
            {
                int oppHandInc = opp.Hand.Count + (opp.PlayingCard != null ? 1 : 0);
                formula = formula.Replace("opponent.Hand.CountInclusive", oppHandInc.ToString());
            }

            // 2. Context 임시 변수 치환 (카드 효과 연계용 var.xxx)
            if (context.Variables.Count > 0)
            {
                foreach (var kvp in context.Variables)
                {
                    // 예: "var.moved_count" -> "2"
                    string key = $"var.{kvp.Key}";
                    if (formula.Contains(key))
                    {
                        formula = formula.Replace(key, kvp.Value.ToString());
                    }
                }
            }

            // [Turn] ★ 리팩토링: ConsoleRunner.globalTurn 의존성 제거, Context에서 턴을 가져옴
            formula = formula.Replace("turnCount", context.CurrentTurn.ToString());

            // [Active Player] 시전자 상태
            formula = formula.Replace("activePlayer.Hand.Count", p.Hand.Count.ToString());
            formula = formula.Replace("activePlayer.Graveyard.Count", p.Graveyard.Count.ToString());
            formula = formula.Replace("activePlayer.Deck.Count", p.Deck.Count.ToString());
            formula = formula.Replace("activePlayer.ResourceZone.Count", p.ResourceZone.Count.ToString()); // 자원존 추가

            // [Opponent] 상대방 상태
            if (opp != null)
            {
                formula = formula.Replace("opponent.Hand.Count", opp.Hand.Count.ToString());
                formula = formula.Replace("opponent.Graveyard.Count", opp.Graveyard.Count.ToString());
                formula = formula.Replace("opponent.Deck.Count", opp.Deck.Count.ToString());
                formula = formula.Replace("opponent.ResourceZone.Count", opp.ResourceZone.Count.ToString()); // 자원존 추가
            }

            // [Rules] 게임 기본 룰
            formula = formula.Replace("maxHandSize", GameRules.MaxHandSize.ToString());
            formula = formula.Replace("drawPerTurn", GameRules.DrawPerTurn.ToString());

            return formula;
        }
    }
}