using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;

namespace TCG_Project.Scripts.Effects
{
    // [시스템 전용] 스탯 변경을 되돌리는 효과 (JSON 생성 X)
    public class RevertStatEffect : ICardEffect
    {
        private List<Card> targetsToRevert; // 되돌릴 카드 명단 (직접 참조)
        private string statName;
        private int amountToRevert; // 되돌릴 수치 (이미 반대 부호여야 함)

        // 복제를 위해 초기화 파라미터를 기억해 둡니다.
        private Dictionary<string, object> _cachedParams;

        public RevertStatEffect(List<Card> targets, string stat, int amount)
        {
            this.targetsToRevert = targets;
            this.statName = stat;
            this.amountToRevert = amount;
        }

        public void Initialize(Dictionary<string, object> parameters) { } // 미사용

        public void Execute(GameContext context, Action onComplete)
        {
            int count = 0;
            foreach (var card in targetsToRevert)
            {
                // [중요] 묘지나 덱으로 간 카드는 이미 ResetState()로 초기화되었으므로
                // 되돌리기(Revert)를 하면 안 됩니다. (오히려 스탯이 망가짐)
                // 따라서 '패'나 '필드'에 살아있는 카드만 되돌립니다.
                bool isActiveZone = context.Players.Exists(p => p.Hand.Contains(card) || p.Field.Contains(card));

                if (isActiveZone)
                {
                    ApplyRevert(card);
                    count++;
                }
            }

            if (count > 0)
            {
                System.Console.WriteLine($"↩️ [효과 만료] {count}장 카드의 {statName} 스탯이 원래대로 돌아갑니다.");
            }
        }

        private void ApplyRevert(Card card)
        {
            if (statName == "Cost")
            {
                card.Cost += amountToRevert;
                if (card.Cost < 0) card.Cost = 0;
            }
            // Power, HP 등 확장 가능
        }

        // 깊은 복사 구현체
        public ICardEffect Clone()
        {
            // 이 효과는 JSON 파라미터가 아니라, 런타임에 지정된 특정 타겟들을 기억해야 합니다.
            // 타겟 리스트(List<Card>)는 새 리스트로 얕은 복사본을 만들어 원본 훼손을 방지합니다.
            // (내부 변수 이름이 _targets, _statName, _amount 라고 가정)

            return new RevertStatEffect(new List<Card>(this.targetsToRevert), this.statName, this.amountToRevert);
        }
    }
}