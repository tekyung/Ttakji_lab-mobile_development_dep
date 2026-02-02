using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;
using TCG_Project.Scripts.Systems;

namespace TCG_Project.Scripts.Effects
{
    public class ModifyStatEffect : ICardEffect
    {
        private object targetParam;
        private string statName;
        private object amountParam;

        public void Initialize(Dictionary<string, object> parameters)
        {
            targetParam = parameters["target"];
            statName = parameters["stat"].ToString();
            amountParam = parameters["amount"];
        }

        public void Execute(GameContext context)
        {
            int amount = FormulaEvaluator.Evaluate(amountParam, context);
            List<Target> targets = TargetSelector.Select(targetParam, context);

            foreach (Target t in targets)
            {
                if (t.Type == TargetType.Player)
                {
                    Player p = t.PlayerVal;
                    if (statName == "Health") p.Health += amount;
                    else if (statName == "Mana") p.Mana += amount;

                    System.Console.WriteLine($"✨ [스탯 변경] {p.Name}의 {statName} {amount} 변동 -> {(statName == "Health" ? p.Health : p.Mana)}");
                }
                else if (t.Type == TargetType.Card)
                {
                    Card c = t.CardVal;
                    if (statName == "Cost")
                    {
                        c.Cost += amount;
                        if (c.Cost < 0) c.Cost = 0;
                        System.Console.WriteLine($"✨ [스탯 변경] 카드 '{c.Name}'의 Cost {amount} 변동 -> {c.Cost}");
                    }
                }
            }
        }
    }
}