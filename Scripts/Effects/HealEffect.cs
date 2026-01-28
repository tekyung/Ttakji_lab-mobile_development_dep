using System;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;

namespace TCG_Project.Scripts.Effects // 이 부분이 필수입니다!
{
    // 3. 힐 효과
    public class HealEffect : ICardEffect
    {
        private int amount;

        public void Initialize(Dictionary<string, object> parameters)
        {
            amount = Convert.ToInt32(parameters["amount"]);
        }

        public void Execute(GameContext context)
        {
            context.Player.Heal(amount);
        }
    }
}
