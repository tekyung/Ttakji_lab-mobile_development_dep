using System;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;

namespace TCG_Project.Scripts.Effects // 이 부분이 필수입니다!
{
    // 2. 카드를 드로우하는 효과
    public class DrawCardEffect : ICardEffect
    {
        private int count;

        public void Initialize(Dictionary<string, object> parameters)
        {
            count = Convert.ToInt32(parameters["count"]);
        }

        public void Execute(GameContext context)
        {
            for (int i = 0; i < count; i++)
            {
                context.Player.Draw();
            }
        }
    }
}
