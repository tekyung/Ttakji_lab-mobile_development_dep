using System.Collections.Generic;
using System.Linq;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Systems;

namespace TCG_Project.Scripts.Utils
{
    public class TiebreakerResolver
    {
        /// <summary>
        /// 동시 덱아웃 발생 시 룰북에 명시된 6단계 타이브레이커 판정을 수행합니다.
        /// 반환값: 양수(p1 승리), 음수(p2 승리), 0(무승부로 코인토스 필요)
        /// </summary>
        public static int ResolveTiebreaker(Player p1, Player p2)
        {
            // 1단계: 묘지 존 카드 비교 (적은 쪽 승)
            if (p1.Graveyard.Count != p2.Graveyard.Count) 
                return p2.Graveyard.Count.CompareTo(p1.Graveyard.Count); // 묘지 존은 적은 쪽이 승리

            // 2단계: 자원 존 카드 비교 (많은 쪽 승)
            if (p1.ResourceZone.Count != p2.ResourceZone.Count) 
                return p1.ResourceZone.Count.CompareTo(p2.ResourceZone.Count);

            // 3단계: 덱 카드 비교 (많은 쪽 승)
            int p1DeckCount = p1.Deck.Count;
            int p2DeckCount = p2.Deck.Count;
            if (p1DeckCount != p2DeckCount) 
                return p1DeckCount.CompareTo(p2DeckCount);

            // 4단계: 자원 덱 카드 비교 (많은 쪽 승)
            if(p1.ResourceDeck.Count != p2.ResourceDeck.Count)
                return p1.ResourceDeck.Count.CompareTo(p2.ResourceDeck.Count);

            // 5단계: 라이프 토큰 비교 (많은 쪽 승)
            if (p1.LifeTokens != p2.LifeTokens)
                return p1.LifeTokens.CompareTo(p2.LifeTokens);

            // 6단계: 여기까지 모두 같으면 0을 반환 (ConsoleRunner가 코인 토스 진행)
            return 0;
        }
    }
}