using System;
using System.Collections.Generic;
using System.Linq;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;

namespace TCG_Project.Scripts.Effects
{
    public enum SelectMode
    {
        Top,     // 존의 앞(맨 위)에서 N장
        Bottom,  // 존의 뒤(맨 아래)에서 N장
        Random,  // 랜덤 N장 (봇/Human 모두 랜덤)
        Choose,  // 플레이어 선택 (봇 환경에서는 Random과 동일)
        All,     // 조건에 맞는 전체
        First,   // 조건에 맞는 첫 번째 카드
        Last,    // 조건에 맞는 마지막 N장 (최근 추가 순)
    }

    /// <summary>
    /// 카드 선택 공통 로직.
    /// MoveEffect가 내부적으로 사용하며, 존·필터·수량·선택 방식을 조합하여 대상 카드를 결정한다.
    /// </summary>
    public class CardSelector
    {
        public ZoneType From { get; set; } = ZoneType.Deck;
        public string Owner { get; set; } = "Self";    // "Self" | "Opponent"
        public string Filter { get; set; } = "";
        public int Count { get; set; } = 1;
        public SelectMode Mode { get; set; } = SelectMode.Top;

        private static readonly Random _rng = new Random();

        public List<Card> SelectCards(Player self, Player opponent, Card excludeByName = null)
        {
            Player owner = (Owner == "Opponent") ? opponent : self;
            List<Card> source = GetZoneCards(owner);
            List<Card> pool = ApplyFilter(source, Filter);

            if (excludeByName != null)
                pool = pool.Where(c => c.Name != excludeByName.Name).ToList();

            return SelectByMode(pool);
        }

        // ─── 정적 유틸: 다른 곳에서도 재사용 가능 ───────────────────────────

        /// <summary>
        /// 단일 카드가 filter 조건에 맞는지 확인.
        /// ApplyFilter를 활용하므로 type:Effect 등 통합 필터 지원.
        /// </summary>
        public static bool MatchesSingle(Card card, string filter)
            => ApplyFilter(new List<Card> { card }, filter).Count > 0;

        /// <summary>
        /// filter 문자열("character:ELLIE,type:Attack" 형식)에 맞게 카드 목록을 필터링한다.
        /// "type:Effect"는 Attack·Defense·Support 통합 필터로 처리된다.
        /// </summary>
        public static List<Card> ApplyFilter(List<Card> cards, string filter)
        {
            if (string.IsNullOrEmpty(filter)) return cards.ToList();

            var result = cards.ToList();
            // ★ 안전장치: 모든 필터 문자열을 소문자로 변환하여 대소문자 오타로 인한 버그 예방
            foreach (var cond in filter.ToLower().Split(','))
            {
                var parts = cond.Trim().Split(':');
                if (parts.Length != 2) continue;
                string key = parts[0].Trim();
                string val = parts[1].Trim();

                switch (key)
                {
                    case "character":
                        // 1. CharacterId 직접 매칭 (sonia == sonia)
                        // 2. ID 접두사 매칭 (soni-02 -> soni 추출 후 포함 여부 검사)
                        result = result.Where(c =>
                        {
                            string cardPrefix = c.Id.Contains("-") ? c.Id.Split('-')[0].ToLower() : c.Id.ToLower();
                            return (c.CharacterId != null && c.CharacterId.ToLower() == val) || val.StartsWith(cardPrefix);
                        }).ToList();
                        break;

                    case "type":
                        if (val == "effect")
                        {
                            result = result.Where(c =>
                                c.Type == CardType.Attack ||
                                c.Type == CardType.Defense ||
                                c.Type == CardType.Support).ToList();
                        }
                        // Enum.TryParse의 두 번째 인자 'true'는 대소문자를 무시하라는 뜻입니다.
                        else if (Enum.TryParse<CardType>(val, true, out CardType t))
                            result = result.Where(c => c.Type == t).ToList();
                        break;

                    case "replayable": // ★ 신규 추가: 신재생에너지 제약 조건
                        bool isReplayable = (val == "true");

                        /*EventManager.OnLogMessage?.Invoke($"간접 발동 불가 카드는 제외합니다.");
                        ★ 진실의 방: 검사소에 도착한 카드의 실제 메모리 상태를 강제로 까봅니다.
                        foreach(var c in result) {
                            EventManager.OnLogMessage?.Invoke($"  [디버그] '{c.Name}' 카드의 제약 상태는? -> {c.CannotBePlayedByEffect}");
                        }*/

                        // replayable:true 이면 CannotBePlayedByEffect가 false인 것만 남김
                        result = result.Where(c => isReplayable ? !c.CannotBePlayedByEffect : c.CannotBePlayedByEffect).ToList();
                        break;
                }
            }
            return result;
        }

        // ─── private ────────────────────────────────────────────────────────

        private List<Card> GetZoneCards(Player owner)
        {
            return From switch
            {
                ZoneType.Deck => owner.Deck.ToList(),
                ZoneType.Hand => owner.Hand.ToList(),
                ZoneType.Graveyard => owner.Graveyard.ToList(),
                ZoneType.ResourceDeck => owner.ResourceDeck.ToList(),
                ZoneType.ResourceZone => owner.ResourceZone.ToList(),
                ZoneType.StackZone => owner.StackZone.ToList(),
                _ => new List<Card>()
            };
        }

        private List<Card> SelectByMode(List<Card> pool)
        {
            if (pool.Count == 0) return new List<Card>();

            return Mode switch
            {
                SelectMode.All => pool,
                SelectMode.Top => pool.Take(Count).ToList(),
                SelectMode.First => pool.Take(Count).ToList(),
                SelectMode.Bottom => pool.Skip(Math.Max(0, pool.Count - Count)).ToList(),
                SelectMode.Last => pool.Skip(Math.Max(0, pool.Count - Count)).ToList(),
                SelectMode.Random or SelectMode.Choose
                                    => SelectRandom(pool, Count),
                _ => pool.Take(Count).ToList()
            };
        }

        private static List<Card> SelectRandom(List<Card> pool, int count)
        {
            var copy = pool.ToList();
            var selected = new List<Card>();
            int n = Math.Min(count, copy.Count);
            for (int i = 0; i < n; i++)
            {
                int idx = _rng.Next(copy.Count);
                selected.Add(copy[idx]);
                copy.RemoveAt(idx);
            }
            return selected;
        }
    }
}
