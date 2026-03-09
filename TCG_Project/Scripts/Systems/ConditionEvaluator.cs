using System;
using System.Data;
using System.Linq; // Any() ????? ???? ???
using TCG_Project.Scripts.Core;

namespace TCG_Project.Scripts.Systems
{
    public static class ConditionEvaluator
    {
        public static bool Evaluate(string conditionFormula, GameContext context)
        {
            if (string.IsNullOrWhiteSpace(conditionFormula) || conditionFormula.Trim().ToLower() == "true")
                return true;

            // [규칙] ConditionEvaluator?? 수식이 아닌 문자열이 들어오는 경우도 있습니다. (예: "DeckNotEmpty", "HandFull" 등)
            switch (conditionFormula)
            {
                case "DeckNotEmpty":
                    return context.ActivePlayer.Deck.Count > 0;

                case "HandFull":
                    return context.ActivePlayer.Hand.Count >= GameRules.MaxHandSize;
            }

            // 1. 
            string parsedFormula = ReplaceVariables(conditionFormula, context);

            // 2. C# 연산자를 DataTable 로 변경
            parsedFormula = parsedFormula.Replace("&&", " AND ");
            parsedFormula = parsedFormula.Replace("||", " OR ");
            parsedFormula = parsedFormula.Replace("==", "="); // DataTable?? ?????? '='?? ?????
            parsedFormula = parsedFormula.Replace("!=", "<>"); // 표현식 변경 '<>'

            try
            {
                DataTable table = new DataTable();
                var result = table.Compute(parsedFormula, "");

                if (result is bool boolResult) return boolResult;
                return false;
            }
            catch (Exception e)
            {
                // ??????? ???? ???? ???? ?????? ???? ???
                Console.WriteLine($"[Error] ????? ????: \"{conditionFormula}\" -> \"{parsedFormula}\"\n????: {e.Message}");
                return false;
            }
        }

        private static string ReplaceVariables(string formula, GameContext context)
        {
            Player p = context.ActivePlayer;
            Player opp = context.GetOpponent(p);

            // [???] FormulaEvaluator?? ??????? "?? ??????"?? Inclusive?? ???? ????? ????.

            // 1. Inclusive (???? ??? ??? ????) ???? ??? ?? ??
            int myHandInc = p.Hand.Count + (p.PlayingCard != null ? 1 : 0);
            formula = formula.Replace("activePlayer.Hand.CountInclusive", myHandInc.ToString());

            if (opp != null) // opp null ?? ???
            {
                int oppHandInc = opp.Hand.Count + (opp.PlayingCard != null ? 1 : 0);
                formula = formula.Replace("opponent.Hand.CountInclusive", oppHandInc.ToString());
            }

            // [Turn] (Program.turnCount ?????? ????????? context?? GameRules???? ?????????? ???? ???)
            formula = formula.Replace("turnCount", ConsoleRunner.globalTurn.ToString()); 

            // [Active Player]
            formula = formula.Replace("activePlayer.Hand.Count", p.Hand.Count.ToString());
            formula = formula.Replace("activePlayer.Graveyard.Count", p.Graveyard.Count.ToString());
            formula = formula.Replace("activePlayer.Deck.Count", p.Deck.Count.ToString());

            // [Opponent]
            if (opp != null)
            {
                formula = formula.Replace("opponent.Hand.Count", opp.Hand.Count.ToString());
                formula = formula.Replace("opponent.Graveyard.Count", opp.Graveyard.Count.ToString());
                formula = formula.Replace("opponent.Deck.Count", opp.Deck.Count.ToString());
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

        // ??? ???(Target)?? ???????? ???? ???
        public static bool EvaluateTarget(Target target, string condition, GameContext context)
        {
            if (string.IsNullOrWhiteSpace(condition) || condition.Trim().ToLower() == "true")
                return true;

            string parsed = condition;

            // 1. ??? ???? ???? ??
            if (target.Type == TargetType.Card)
            {
                Card c = target.CardVal;
                parsed = parsed.Replace("target.Cost", c.Cost.ToString());
                parsed = parsed.Replace("target.Name", $"'{c.Name}'");

                // [??? ???] ??? ??? ??? (??: target.HasEffect('Damage'))
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
                parsed = parsed.Replace("target.Hand.Count", p.Hand.Count.ToString());
            }

            // 2. ???? ???? ?? (???? ???? ????)
            parsed = ReplaceVariables(parsed, context);

            // 3. ?????? ???
            parsed = parsed.Replace("&&", " AND ").Replace("||", " OR ").Replace("==", "=").Replace("!=", "<>");

            // 4. ???
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