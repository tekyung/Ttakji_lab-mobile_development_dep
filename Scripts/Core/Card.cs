using System;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;

namespace TCG_Project.Scripts.Core
{
    public class Card
    {
        public string Name { get; set; }
        public int Cost { get; set; }
        public string Description { get; set; }

        // 카드는 여러 개의 효과를 가질 수 있습니다.
        private List<ICardEffect> effects = new List<ICardEffect>();

        public void AddEffect(ICardEffect effect)
        {
            effects.Add(effect);
        }

        // 카드를 사용할 때 호출
        public void Play(GameContext context)
        {
            Console.WriteLine($"--- {Name} / {Cost} / {Description} ---\n");
            foreach (var effect in effects)
            {
                effect.Execute(context);
            }
            Console.WriteLine("---------------------------------------------\n");
        }
    }
}
