using System;
using System.Data;
using Newtonsoft.Json.Linq; // JObject 사용 필수
using TCG_Project.Scripts.Core;

namespace TCG_Project.Scripts.Systems
{
    public static class FormulaEvaluator
    {
        public static int Evaluate(object value, GameContext context)
        {
            // 1. 이미 숫자면 반환
            if (value is int || value is long || value is double)
                return Convert.ToInt32(value);

            // 2. Selector 객체 처리 ({ "type": "Select", ... })
            if (value is JObject obj)
            {
                if (obj["type"]?.ToString() == "Select")
                {
                    // 조건 확인
                    string condition = obj["condition"].ToString();
                    bool isTrue = ConditionEvaluator.Evaluate(condition, context);

                    // 조건에 따라 trueValue 또는 falseValue 선택
                    var selectedValue = isTrue ? obj["trueValue"] : obj["falseValue"];

                    // 선택된 값이 또 수식일 수 있으므로 재귀 호출
                    return Evaluate(selectedValue, context);
                }
            }

            // 3. 문자열 수식 처리 (기존 로직)
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
                Console.WriteLine($"[Error] 수식 계산 실패: {formula} / {e.Message}");
                return 0;
            }
        }

        // 변수 치환 메서드
        public static string ReplaceVariables(string formula, GameContext context)
        {
            Player p = context.ActivePlayer;
            Player opp = context.GetOpponent(p);

            // [규칙] 더 긴 문자열을 먼저 치환해야 부분 일치 오류를 막을 수 있습니다.
            // 예: "Hand.Count"를 먼저 치환하면 "Hand.CountInclusive"가 "5Inclusive"가 되어버림!
            // 1. Inclusive (내고 있는 카드 포함) 변수 계산 및 치환
            int myHandInc = p.Hand.Count + (p.PlayingCard != null ? 1 : 0);
            formula = formula.Replace("activePlayer.Hand.CountInclusive", myHandInc.ToString());

            int oppHandInc = opp.Hand.Count + (opp.PlayingCard != null ? 1 : 0);
            formula = formula.Replace("opponent.Hand.CountInclusive", oppHandInc.ToString());

            // Context 변수 치환
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

            // (선택 사항) 정의되지 않은 변수(var.xxx)가 남았다면 0으로 처리하여 에러 방지
            // if (formula.Contains("var.")) return "0"; 

            // [Turn]
            formula = formula.Replace("turnCount", ConsoleRunner.globalTurn.ToString());

            // [Active Player]
            formula = formula.Replace("activePlayer.Mana", p.Mana.ToString());
            formula = formula.Replace("activePlayer.Hand.Count", p.Hand.Count.ToString());
            formula = formula.Replace("activePlayer.Graveyard.Count", p.Graveyard.Count.ToString());
            formula = formula.Replace("activePlayer.Deck.Count", p.Deck.Count.ToString());
            formula = formula.Replace("activePlayer.PrizePoints", p.PrizePoints.ToString());
            formula = formula.Replace("activePlayer.Field.Count", p.Field.Count(c => c != null).ToString());

            // [Opponent]
            formula = formula.Replace("opponent.Hand.Count", opp.Hand.Count.ToString());
            formula = formula.Replace("opponent.Graveyard.Count", opp.Graveyard.Count.ToString());
            formula = formula.Replace("opponent.Deck.Count", opp.Deck.Count.ToString());
            formula = formula.Replace("opponent.Mana", opp.Mana.ToString());
            formula = formula.Replace("opponent.PrizePoints", opp.PrizePoints.ToString());
            formula = formula.Replace("opponent.Field.Count", p.Field.Count(c => c != null).ToString());

            // [Rules]
            formula = formula.Replace("maxHandSize", GameRules.MaxHandSize.ToString());
            formula = formula.Replace("drawPerTurn", GameRules.DrawPerTurn.ToString());
            formula = formula.Replace("prize_count_to_win", GameRules.WinPrizePoints.ToString());
            formula = formula.Replace("maxFieldUnitCount", GameRules.MaxFieldUnitCount.ToString());
            formula = formula.Replace("firstTurnEnergy", GameRules.FirstPlayerFirstTurnEnergy.ToString());
            formula = formula.Replace("secondTurnEnergy", GameRules.SecondPlayerFirstTurnEnergy.ToString());
            formula = formula.Replace("basicEnergy", GameRules.BasicEnergy.ToString());

            // 필요하다면 더 많은 변수 추가 가능
            return formula;
        }
    }
}
