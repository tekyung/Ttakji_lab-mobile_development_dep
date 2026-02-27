using System.Collections.Generic;
using System.Linq;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Systems;

namespace TCG_Project.Scripts.Utils
{
    public static class DeckValidator
    {
        /// <summary>
        /// 플레이어의 덱이 총 장수 및 카드별 제한 매수(MaxDeckCount) 룰을 준수하는지 검사합니다.
        /// </summary>
        /// <param name="deck">검사할 덱 리스트</param>
        /// <param name="errorMessage">실패 시 출력할 에러 메시지 (성공 시 string.Empty)</param>
        /// <returns>덱이 유효하면 true, 아니면 false</returns>
        public static bool IsValidDeck(List<Card> deck, out string errorMessage)
        {
            // 1. 전체 덱 매수(최소/최대) 검사 (GameRules 참조)
            if (deck.Count < GameRules.MinDeckCardCount || deck.Count > GameRules.MaxDeckCardCount)
            {
                errorMessage = $"덱의 총 카드 수는 {GameRules.MinDeckCardCount}장 이상, {GameRules.MaxDeckCardCount}장 이하여야 합니다. (현재 {deck.Count}장)";
                return false;
            }

            // 2. 카드 종류별 제한 매수(리미트 레귤레이션) 검사
            // LINQ GroupBy를 사용하여 원본 ID(Id) 기준으로 카드들을 그룹화합니다.
            var groupedCards = deck.GroupBy(c => c.Id);

            foreach (var group in groupedCards)
            {
                int currentCount = group.Count();
                Card sampleCard = group.First(); // 그룹 내 첫 번째 카드로 정보 확인

                // 해당 카드의 제한 매수를 초과했는지 확인
                if (currentCount > sampleCard.MaxDeckCount)
                {
                    errorMessage = $"'{sampleCard.Name}' 카드는 덱에 최대 {sampleCard.MaxDeckCount}장까지만 넣을 수 있습니다. (현재 {currentCount}장 포함됨)";
                    return false;
                }
            }

            // 모든 검사를 무사히 통과함
            errorMessage = string.Empty;
            return true;
        }
    }
}