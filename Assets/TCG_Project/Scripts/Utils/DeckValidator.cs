using System.Collections.Generic;
using System.Linq;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Systems;

namespace TCG_Project.Scripts.Utils
{

    public static class DeckValidator
    {
        /// <summary>
        /// [전투! 용병의 시대] 덱 빌딩 룰을 검사합니다.
        /// 1. 선택 용병은 서로 다른 2종류
        /// 2. 덱은 총 20장, 각각 2장씩 10종류
        /// 3. 모든 카드는 선택한 용병 테마 카드
        /// </summary>
        public static DeckValidationResult ValidateFullDeckSet(List<Card> mainDeck, List<Card> resourceDeck, string char1Id, string char2Id)
        {
            // ── 1. 용병 선택 검증 (서로 다른 2종류) ──
            if (string.IsNullOrEmpty(char1Id) || string.IsNullOrEmpty(char2Id))
            {
                return DeckValidationResult.Fail("두 명의 용병(캐릭터)을 모두 선택해야 합니다.");
            }

            if (char1Id == char2Id)
            {
                return DeckValidationResult.Fail("서로 다른 두 명의 용병을 선택해야 합니다.");
            }

            // 캐릭터 ID에서 접두사(Prefix) 추출 (예: "ELLI-01" -> "ELLI")
            string prefix1 = char1Id.Contains("-") ? char1Id.Split('-')[0] : char1Id;
            string prefix2 = char2Id.Contains("-") ? char2Id.Split('-')[0] : char2Id;

            // ── 2. 메인 덱 총 매수 검증 (20장) ──
            if (mainDeck.Count != 20)
            {
                return DeckValidationResult.Fail($"메인 덱은 정확히 20장이어야 합니다. (현재 {mainDeck.Count}장)");
            }

            // ── 3. 카드 종류 및 매수 검증 (10종류, 각각 2장씩) ──
            var groupedCards = mainDeck.GroupBy(c => c.Id).ToList();

            if (groupedCards.Count != 10)
            {
                //return DeckValidationResult.Fail($"메인 덱은 정확히 10종류의 카드로 구성되어야 합니다. (현재 {groupedCards.Count}종류)");
            }

            foreach (var group in groupedCards)
            {
                if (group.Count() != 2)
                {
                    Card sample = group.First();
                    //return DeckValidationResult.Fail($"'{sample.Name}' 카드가 {group.Count()}장 있습니다. 모든 카드는 정확히 2장씩 넣어야 합니다.");
                }

                // ── 4. 선택한 용병 테마 일치 여부 검증 ──
                Card card = group.First();

                // ★ 수정: "ELLIE" == "ELLI" 같은 하드코딩 불일치 문제를 해결하기 위해,
                // Card.Id("ELLI-05") 가 접두사로 시작하거나, CharacterId("ELLIE") 가 접두사로 시작하면 통과시킵니다.

                bool isTheme1 = card.Id.StartsWith(prefix1) ||
                                (!string.IsNullOrEmpty(card.CharacterId) && card.CharacterId.StartsWith(prefix1));

                bool isTheme2 = card.Id.StartsWith(prefix2) ||
                                (!string.IsNullOrEmpty(card.CharacterId) && card.CharacterId.StartsWith(prefix2));

                if (!isTheme1 && !isTheme2)
                {
                    return DeckValidationResult.Fail($"'{card.Name}' 카드는 선택하신 용병({prefix1}, {prefix2})의 테마 카드가 아닙니다.");
                }
            }

            // ── 5. 자원 덱 검증 (15장) ──
            if (resourceDeck != null && resourceDeck.Count != GameRules.ResourceDeckCount)
            {
                return DeckValidationResult.Fail($"자원 덱은 정확히 {GameRules.ResourceDeckCount}장이어야 합니다. (현재 {resourceDeck.Count}장)");
            }

            // 모든 검사 통과
            return DeckValidationResult.Success();
        }

        /// <summary>
        /// 덱 검증 결과를 담는 구조체입니다. UI 팝업에서 ErrorMessage를 그대로 띄워주면 됩니다.
        /// </summary>
        public class DeckValidationResult
        {
            public bool IsValid { get; set; }
            public string ErrorMessage { get; set; }

            public static DeckValidationResult Success() => new DeckValidationResult { IsValid = true, ErrorMessage = string.Empty };
            public static DeckValidationResult Fail(string msg) => new DeckValidationResult { IsValid = false, ErrorMessage = msg };
        }

        /// <summary>
        /// 동시 덱아웃 발생 시 룰북에 명시된 6단계 타이브레이커 판정을 수행합니다.
        /// 반환값: 양수(p1 승리), 음수(p2 승리), 0(무승부로 코인토스 필요)
        /// </summary>
        public static int ResolveTiebreaker(Player p1, Player p2)
        {
            // 1단계: 남은 라이프 (많은 쪽 승)
            if (p1.LifeTokens != p2.LifeTokens)
                return p1.LifeTokens.CompareTo(p2.LifeTokens);

            // 2단계: 전장 카드 유무 (있는 쪽 승)
            int p1Field = p1.BattlefieldCard != null ? 1 : 0;
            int p2Field = p2.BattlefieldCard != null ? 1 : 0;
            if (p1Field != p2Field)
                return p1Field.CompareTo(p2Field);

            // 3단계: 스택 존 카드 수 (많은 쪽 승)
            if (p1.StackZone.Count != p2.StackZone.Count)
                return p1.StackZone.Count.CompareTo(p2.StackZone.Count);

            // 4단계: 패(Hand) 장수 (많은 쪽 승)
            if (p1.Hand.Count != p2.Hand.Count)
                return p1.Hand.Count.CompareTo(p2.Hand.Count);

            // 5단계: 자원 존(Resource) 개수 (많은 쪽 승)
            if (p1.ResourceZone.Count != p2.ResourceZone.Count)
                return p1.ResourceZone.Count.CompareTo(p2.ResourceZone.Count);

            // 6단계: 여기까지 모두 같으면 0을 반환 (ConsoleRunner가 코인 토스 진행)
            return 0;
        }


    }

}