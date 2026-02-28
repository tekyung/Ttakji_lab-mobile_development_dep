using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;

namespace TCG_Project.Scripts.Core
{
    public class UnitCard : Card
    {
        // === [스탯 데이터] ===
        public int MaxPower { get; set; }     // 최대 체력/공격력 (JSON의 power)
        public int CurrentPower { get; set; } // 현재 체력
        public int Prize { get; set; }        // 처치 시 줄 점수
        public int ArtsCost { get; set; }     // 기술 비용

        // === [상태 플래그] ===
        public bool CanAttack { get; set; } = false; // 소환 후유증(소환 턴 공격 불가) 처리용

        // === [효과 컨테이너] ===
        // 기존 Card.effects는 사용 시 즉발 효과지만, 유닛은 '출격 효과'로 명확히 분리
        // (단, Play() 메서드 호환성을 위해 내부적으로 연결 필요)

        // 출격 효과 (Battlecry) - 카드를 낼 때 1회 발동
        public ICardEffect OnPlayEffect { get; set; }

        // 지속 효과 (Passive) - 필드에 있는 동안 적용 (추후 구현)
        public List<ICardEffect> PassiveEffects { get; private set; } = new List<ICardEffect>();

        public UnitCard() : base() { }

        
    }
}