using System;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;
using TCG_Project.Scripts.Managers;

namespace TCG_Project.Scripts.Effects
{
    /// <summary>
    /// 카드를 한 존에서 다른 존으로 이동시키는 범용 효과.
    /// DrawEffect, DiscardFromHandEffect, ResourceGainEffect, RecoverFromDiscardEffect,
    /// ShuffleReturnEffect, TopDeckToDiscardEffect, SearchDeckEffect,
    /// ResourceFromDiscardEffect, ReturnFromDiscardEffect 를 통합한다.
    ///
    /// GameDataManager가 Initialize()에 아래 파라미터를 주입한다:
    ///   from         : ZoneType 이름 (e.g. "Deck", "Hand", "Graveyard", "ResourceDeck", "ResourceZone")
    ///   to           : ZoneType 이름 (e.g. "Hand", "Graveyard", "Deck", "ResourceZone")
    ///   mode         : "Top" | "Bottom" | "Random" | "Choose" | "All" | "First" | "Last"
    ///   count        : int
    ///   filter       : "character:ELLIE,type:Attack" 등 CardSelector.ApplyFilter 형식
    ///   shuffleAfter : bool (to:Deck 이후 ShuffleDeck 여부)
    ///   excludeSelf  : bool (ActivePlayer.PlayingCard 를 후보에서 제외 — ReturnFromDiscard 용)
    /// </summary>
    public class MoveEffect : ICardEffect
    {
        private ZoneType _from;
        private ZoneType _to;
        private SelectMode _mode = SelectMode.Top;
        private int _count = 1;
        private string _filter = "";
        private bool _shuffleAfter = false;
        private bool _excludeSelf = false;
        public bool RequirePreviousSuccess { get; set; } = false; // 기본값은 false (독립 실행)
        public bool IsStackAction { get; set; } = false; // 기본값은 false (카드의 IsStack을 따라가되, JSON에서 오버라이드 가능)
        private bool _isStrict = true; // 엄격 모드 or 최선 진행 모드 선택 (기본값은 가능한 실행)

        private Dictionary<string, object> _cachedParams;

        public void Initialize(Dictionary<string, object> parameters)
        {
            _cachedParams = parameters ?? new Dictionary<string, object>();

            if (parameters.ContainsKey("from") &&
                Enum.TryParse<ZoneType>(parameters["from"].ToString(), out var from))
                _from = from;

            if (parameters.ContainsKey("to") &&
                Enum.TryParse<ZoneType>(parameters["to"].ToString(), out var to))
                _to = to;

            if (parameters.ContainsKey("count"))
                _count = Convert.ToInt32(parameters["count"]);

            if (parameters.ContainsKey("filter"))
                _filter = parameters["filter"].ToString();

            if (parameters.ContainsKey("shuffleAfter"))
                _shuffleAfter = Convert.ToBoolean(parameters["shuffleAfter"]);

            if (parameters.ContainsKey("excludeSelf"))
                _excludeSelf = Convert.ToBoolean(parameters["excludeSelf"]);

            if (parameters.ContainsKey("mode"))
            {
                _mode = parameters["mode"].ToString().ToLower() switch
                {
                    "top" => SelectMode.Top,
                    "bottom" => SelectMode.Bottom,
                    "all" => SelectMode.All,
                    "first" => SelectMode.First,
                    "last" => SelectMode.Last,
                    "choose" => SelectMode.Choose,
                    _ => SelectMode.Random
                };
            }

            // ★ "그 후," 시맨틱 지원
            if (parameters.ContainsKey("requirePreviousSuccess"))
                RequirePreviousSuccess = Convert.ToBoolean(parameters["requirePreviousSuccess"]);

            // ★ 스택/즉발 효과 여부
            if (parameters.TryGetValue("isStackAction", out var isStackObj))
            {
                IsStackAction = Convert.ToBoolean(isStackObj.ToString());
            }

            // 엄격하게 지켜져야 하는가? 여부 ("그 후," 등)
            if (parameters.ContainsKey("isStrict"))
                _isStrict = Convert.ToBoolean(parameters["isStrict"]);
            else
            {
                // JSON에 명시되지 않았을 때의 스마트 기본값:
                // 드로우(Deck->Hand)이거나 전체 버리기(All)면 유연하게(false), 그 외(조건 지불 등)는 엄격하게(true)
                if ((_from == ZoneType.Deck && _to == ZoneType.Hand) || _mode == SelectMode.All)
                    _isStrict = false;
                else
                    _isStrict = true; 
            }
        }

        public void Execute(GameContext context, Action onComplete)
        {
            Player self = context.ActivePlayer;
            Player opponent = context.TargetPlayer;
            if (self == null) { onComplete?.Invoke(); return; }

            Card exclude = _excludeSelf ? self.PlayingCard : null;

            var selector = new CardSelector
            {
                From = _from,
                Owner = "Self",
                Filter = _filter,
                Count = _count,
                Mode = _mode
            };

            List<Card> selected = selector.SelectCards(self, opponent, exclude);
            bool isAllMode = _mode == SelectMode.All;

            // ─── ★ 핵심 로직: 엄격성(Strictness) 검사 ───
            if (!isAllMode) // "모두(All)" 모드는 0장이어도 논리적으로 성공임
            {
                if (_isStrict && selected.Count < _count)
                {
                    // 엄격 모드: 단 1장이라도 모자라면 전체 취소 (All or Nothing)
                    EventManager.OnLogMessage?.Invoke($"  [효과 실패] {_from}에 카드가 부족합니다. (요구: {_count}, 현재: {selected.Count})");
                    context.LastEffectSucceeded = false;
                    onComplete?.Invoke();
                    return;
                }
                else if (selected.Count == 0 && _count > 0)
                {
                    // 유연 모드라도 1장도 옮기지 못했다면 실패로 간주 ("그 후" 조건 불충족)
                    EventManager.OnLogMessage?.Invoke($"  [효과 실패] {_from}에서 이동할 카드가 없습니다.");
                    context.LastEffectSucceeded = false;
                    onComplete?.Invoke();
                    return;
                }
            }

            // ─── 실제 카드 이동 처리 ───
            foreach (var card in selected)
            {
                RemoveFromZone(self, _from, card);
                AddToZone(self, _to, card);
                
                // ★ 누락되었던 시각적 연출(UI) 이벤트 발행 보강
                EventManager.OnLogMessage?.Invoke($"  [카드 이동] {self.Name}: '{card.Name}' {_from}→{_to}");
                EventManager.OnCardMove?.Invoke(card, self, _from, self, _to);

                // Deck -> Hand 이동은 드로우로 취급하여 이벤트 발행
                if (_from == ZoneType.Deck && _to == ZoneType.Hand)
                {
                    EventManager.OnCardDraw?.Invoke(card, self, ZoneType.Deck);
                }
            }

            if (_shuffleAfter)
            {
                self.ShuffleDeck();
                EventManager.OnLogMessage?.Invoke($"  [덱 섞기] {self.Name} 이동 후 메인덱 섞음 (덱: {self.Deck.Count}장)");
            }

            // 여기까지 도달했다면 효과 처리에 성공한 것임
            context.LastEffectSucceeded = true;
            onComplete?.Invoke();

            /*
            if (selected.Count == 0 || selected.Count < _count)
            {
                string filterHint = string.IsNullOrEmpty(_filter) ? "" : $" (필터: {_filter})";
                EventManager.OnLogMessage?.Invoke(
                    $"  [카드 이동] {self.Name}: 대상 없음 — {_from}→{_to}{filterHint}");
                context.LastEffectSucceeded = false; // Phase 18: "그 후" 효과 불발
                onComplete?.Invoke();
                return;
            }

            foreach (var card in selected)
            {
                RemoveFromZone(self, _from, card);
                AddToZone(self, _to, card);
                EventManager.OnLogMessage?.Invoke(
                    $"  [카드 이동] {self.Name}: '{card.Name}' {_from}→{_to}");
            }

            if (_shuffleAfter)
            {
                self.ShuffleDeck();
                EventManager.OnLogMessage?.Invoke(
                    $"  [덱 섞기] {self.Name} 이동 후 메인덱 섞음 (덱: {self.Deck.Count}장)");
            }

            onComplete?.Invoke();
            */
        }

        private void RemoveFromZone(Player p, ZoneType zone, Card card)
        {
            switch (zone)
            {
                case ZoneType.Deck: p.ExtractCard(ZoneType.Deck, card); break;
                case ZoneType.Hand: p.ExtractCard(ZoneType.Hand, card); break;
                case ZoneType.Graveyard: p.ExtractCard(ZoneType.Graveyard, card); break;
                case ZoneType.ResourceDeck: p.ExtractCard(ZoneType.ResourceDeck, card); break;
                case ZoneType.ResourceZone: p.ExtractCard(ZoneType.ResourceZone, card); break;
                case ZoneType.PlayBuffer: p.ExtractCard(ZoneType.PlayBuffer, card); break;
            }
        }

        private void AddToZone(Player p, ZoneType zone, Card card)
        {
            switch (zone)
            {
                case ZoneType.Deck: p.InsertCard(ZoneType.Deck, card); break;
                case ZoneType.Hand: p.InsertCard(ZoneType.Hand, card); break;
                case ZoneType.Graveyard: p.InsertCard(ZoneType.Graveyard, card); break;
                case ZoneType.ResourceZone: p.InsertCard(ZoneType.ResourceZone, card); break;
                case ZoneType.PlayBuffer: p.InsertCard(ZoneType.PlayBuffer, card); break;
            }
        }

        public ICardEffect Clone()
        {
            var clone = new MoveEffect();
            clone.Initialize(_cachedParams);
            return clone;
        }
    }
}
