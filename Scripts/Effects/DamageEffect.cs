using System;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;

// 1. 데미지 주는 효과
namespace TCG_Project.Scripts.Effects // 이 부분이 필수입니다!
{
    public class DamageEffect : ICardEffect
    {
        private int amount;
        private string targetType; // "Self" or "Opponent"

        public void Initialize(Dictionary<string, object> parameters)
        {
            // JSON 파싱 (안전한 타입 변환 로직 필요, 여기선 간략화)
            amount = Convert.ToInt32(parameters["amount"]);
            targetType = parameters["target"].ToString();
        }

        public void Execute(GameContext context)
        {
            Player target = (targetType == "Opponent") ? context.Opponent : context.Player;
            target.TakeDamage(amount);
        }
    }
}
