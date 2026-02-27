using System;
using System.Data;
using System.Linq; // Any() 사용을 위해 필요
using TCG_Project.Scripts.Core;

namespace TCG_Project.Scripts.Systems
{
    public static class ConditionEvaluator
    {
        public static bool Evaluate(string conditionFormula, GameContext context)
        {
            if (string.IsNullOrWhiteSpace(conditionFormula) || conditionFormula.Trim().ToLower() == "true")
                return true;

            // 키워드 기반 조건 처리 (GameDataManager에서 변환된 키워드 대응)
            // 수식 파싱 전에 빠르게 체크하여 처리합니다.
            switch (conditionFormula)
            {
                case "DeckNotEmpty":
                    return context.ActivePlayer.Deck.Count > 0;

                case "EnemyUnitExist":
                    // 상대방(TargetPlayer) 필드에 유닛이 하나라도 있는지 확인
                    return context.TargetPlayer != null && context.TargetPlayer.Field.Any(c => c != null);

                case "HandFull":
                    return context.ActivePlayer.Hand.Count >= GameRules.MaxHandSize;
            }

            // 1. 변수 치환 (기존 로직 유지)
            string parsedFormula = FormulaEvaluator.ReplaceVariables(conditionFormula, context); // 로컬 메서드 대신 FormulaEvaluator 사용 권장
            // * 코드엔 ReplaceVariables가 private static으로 구현되어 있으므로 그걸 호출합니다.
            parsedFormula = ReplaceVariables(conditionFormula, context);

            // 2. C# 스타일 연산자를 DataTable 문법으로 변환
            parsedFormula = parsedFormula.Replace("&&", " AND ");
            parsedFormula = parsedFormula.Replace("||", " OR ");
            parsedFormula = parsedFormula.Replace("==", "="); // DataTable은 같음을 '='로 씁니다
            parsedFormula = parsedFormula.Replace("!=", "<>"); // 다름은 '<>'

            try
            {
                DataTable table = new DataTable();
                var result = table.Compute(parsedFormula, "");

                if (result is bool boolResult) return boolResult;
                return false;
            }
            catch (Exception e)
            {
                // 디버깅을 위해 파싱된 최종 수식을 같이 출력
                Console.WriteLine($"[Error] 조건식 오류: \"{conditionFormula}\" -> \"{parsedFormula}\"\n원인: {e.Message}");
                return false;
            }
        }

        private static string ReplaceVariables(string formula, GameContext context)
        {
            Player p = context.ActivePlayer;
            Player opp = context.GetOpponent(p);

            // [중요] FormulaEvaluator와 동일하게 "긴 변수명"인 Inclusive를 먼저 치환해야 합니다.

            // 1. Inclusive (내고 있는 카드 포함) 변수 계산 및 치환
            int myHandInc = p.Hand.Count + (p.PlayingCard != null ? 1 : 0);
            formula = formula.Replace("activePlayer.Hand.CountInclusive", myHandInc.ToString());

            if (opp != null) // opp null 체크 추가
            {
                int oppHandInc = opp.Hand.Count + (opp.PlayingCard != null ? 1 : 0);
                formula = formula.Replace("opponent.Hand.CountInclusive", oppHandInc.ToString());
            }

            // [Turn] (Program.turnCount 접근이 불가능하다면 context나 GameRules에서 가져오도록 수정 필요)
            formula = formula.Replace("turnCount", ConsoleRunner.globalTurn.ToString()); 

            // [Active Player]
            formula = formula.Replace("activePlayer.Mana", p.Mana.ToString());
            formula = formula.Replace("activePlayer.Hand.Count", p.Hand.Count.ToString());
            formula = formula.Replace("activePlayer.Graveyard.Count", p.Graveyard.Count.ToString());
            formula = formula.Replace("activePlayer.Deck.Count", p.Deck.Count.ToString());
            formula = formula.Replace("activePlayer.PrizePoints", p.PrizePoints.ToString());
            formula = formula.Replace("activePlayer.Field.Count", p.Field.Count(c => c != null).ToString());

            // [Opponent]
            if (opp != null)
            {
                formula = formula.Replace("opponent.Hand.Count", opp.Hand.Count.ToString());
                formula = formula.Replace("opponent.Graveyard.Count", opp.Graveyard.Count.ToString());
                formula = formula.Replace("opponent.Deck.Count", opp.Deck.Count.ToString());
                formula = formula.Replace("opponent.Mana", opp.Mana.ToString());
                formula = formula.Replace("opponent.PrizePoints", opp.PrizePoints.ToString());
            }

            // [Rules]
            formula = formula.Replace("maxHandSize", GameRules.MaxHandSize.ToString());
            formula = formula.Replace("drawPerTurn", GameRules.DrawPerTurn.ToString());
            formula = formula.Replace("prize_count_to_win", GameRules.WinPrizePoints.ToString());
            formula = formula.Replace("maxFieldUnitCount", GameRules.MaxFieldUnitCount.ToString());
            formula = formula.Replace("firstTurnEnergy", GameRules.FirstPlayerFirstTurnEnergy.ToString());
            formula = formula.Replace("secondTurnEnergy", GameRules.SecondPlayerFirstTurnEnergy.ToString());
            formula = formula.Replace("basicEnergy", GameRules.BasicEnergy.ToString());

            return formula;
        }

        // 특정 타겟(Target)을 기준으로 조건 검사
        public static bool EvaluateTarget(Target target, string condition, GameContext context)
        {
            if (string.IsNullOrWhiteSpace(condition) || condition.Trim().ToLower() == "true")
                return true;

            string parsed = condition;

            // 1. 타겟 내부 변수 치환
            if (target.Type == TargetType.Card)
            {
                Card c = target.CardVal;
                parsed = parsed.Replace("target.Cost", c.Cost.ToString());
                parsed = parsed.Replace("target.Name", $"'{c.Name}'");

                // [요청 기능] 효과 타입 검사 (예: target.HasEffect('Damage'))
                if (parsed.Contains("target.HasEffect"))
                {
                    bool hasDamage = c.HasEffectType("Damage");
                    bool hasHeal = c.HasEffectType("Heal");

                    parsed = parsed.Replace("target.HasEffect('Damage')", hasDamage.ToString().ToLower());
                    parsed = parsed.Replace("target.HasEffect('Heal')", hasHeal.ToString().ToLower());
                }
            }
            else if (target.Type == TargetType.Player)
            {
                Player p = target.PlayerVal;
                parsed = parsed.Replace("target.Health", p.Health.ToString());
                parsed = parsed.Replace("target.Mana", p.Mana.ToString());
                parsed = parsed.Replace("target.Hand.Count", p.Hand.Count.ToString());
            }

            // 2. 전역 변수 치환 (기존 로직 재사용)
            parsed = ReplaceVariables(parsed, context);

            // 3. 연산자 변환
            parsed = parsed.Replace("&&", " AND ").Replace("||", " OR ").Replace("==", "=").Replace("!=", "<>");

            // 4. 계산
            try
            {
                System.Data.DataTable table = new System.Data.DataTable();
                var result = table.Compute(parsed, "");
                if (result is bool b) return b;
                return false;
            }
            catch { return false; }
        }
    }
}