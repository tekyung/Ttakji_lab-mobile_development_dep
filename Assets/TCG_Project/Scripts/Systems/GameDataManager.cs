using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
//using System.IO;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Effects;
using TCG_Project.Scripts.Interfaces;

namespace TCG_Project.Scripts.Systems
{
    public class GameDataManager
    {
        public Dictionary<string, Card> AllCards { get; private set; } = new Dictionary<string, Card>();
        public readonly IJsonLoader _jsonLoader; // JSON 로딩 담당

        // 생성자에서 심부름꾼을 강제로 받도록 설정
        public GameDataManager(IJsonLoader jsonLoader)
        {
            _jsonLoader = jsonLoader;
        }

        // ─── 룰북 카드 로더 ───────────────────────────────────────────────────

        private class RawRulebookEffect
        {
            public string type;
            public int amount;
            public string duration;
            public int count;
            public string mode;
            public string filter;
            public int times;
            public string buffType;
            public int costReduction;
            public int reduction;
            public bool isStackAction;
            public bool requirePreviousSuccess;
            public string rewardOnSuccess;
            public string description;
            public RawRulebookEffect action;
        }

        private class RawRulebookCard
        {
            public string id;
            public string characterId;
            public string name;
            public string type;
            public int speed;
            public int cost;
            public string rarity;
            public bool isStack;
            public bool isBattlefield;
            public string description;
            public bool cannotBePlayedByEffect; // 다른 카드를 통한 간접 사용이 가능한가?
            public string imagePath;
            public List<RawRulebookEffect> effects;
        }

        // ─── 캐릭터 카드 로더 ─────────────────────────────────────────────────

        private class RawCharacterCard
        {
            public string id;
            public string name;
            public string characterId;
            public string rarity;
            public string triggerPhase;
            public string condition;
            public string description;
            public string flavorText;
            public string imagePath;
        }

        private class CharacterCardWrapper
        {
            public List<RawCharacterCard> Characters;
        }

        private Dictionary<string, RawCharacterCard> _characterCards
            = new Dictionary<string, RawCharacterCard>();

        private Dictionary<string, RawCharacterCard> _characterCardsById
            = new Dictionary<string, RawCharacterCard>();

        /// <summary>Data/Character.json 을 읽어 캐릭터 카드 데이터를 로드한다.</summary>
        public void LoadCharacterCards()
        {
            var wrapper = ReadJson<CharacterCardWrapper>("Character");
            //var wrapper = ReadJson<CharacterCardWrapper>(basePath + "/Character.json");
            if (wrapper?.Characters == null)
            {
                Console.WriteLine("[System] Character.json 없음, 캐릭터 카드 로드 생략.");
                return;
            }

            _characterCards.Clear();
            _characterCardsById.Clear();

            foreach (var raw in wrapper.Characters)
            {
                _characterCards[raw.characterId] = raw;
                if (!string.IsNullOrEmpty(raw.id))
                    _characterCardsById[raw.id] = raw;
            }

            Console.WriteLine($"[System] 캐릭터 카드 {_characterCards.Count}종 로드 완료.");
        }

        /// <summary>
        /// 카드가 속한 용병의 <b>용병 카드 ID</b>를 찾는다. (예: <c>"ELLIE"</c> 또는 <c>"ELLI-05"</c> → <c>"ELLI-01"</c>)
        ///
        /// ★ 문자열을 직접 조립하지 말고 반드시 이 메서드를 쓸 것.
        ///   카드의 <c>characterId</c>는 <c>"ELLIE"</c>인데 카드 ID 접두사는 <c>"ELLI"</c>라 서로 다르다.
        ///   이 불일치를 호출부마다 처리하면 반드시 어딘가에서 틀린다(이미 전례가 있다).
        ///   Character.json이 <c>id</c>와 <c>characterId</c>를 함께 갖고 있으므로 데이터로 해결된다.
        /// </summary>
        /// <param name="themeOrCardId">용병 테마명(<c>characterId</c>) 또는 그 테마의 카드 ID</param>
        public bool TryResolveCharacterCardId(string themeOrCardId, out string characterCardId)
        {
            characterCardId = null;
            if (string.IsNullOrEmpty(themeOrCardId)) return false;

            // 1) 테마명으로 직접 조회 ("ELLIE" → ELLI-01)
            if (_characterCards.TryGetValue(themeOrCardId, out var byTheme) && !string.IsNullOrEmpty(byTheme?.id))
            {
                characterCardId = byTheme.id;
                return true;
            }

            // 2) 카드 ID 접두사로 조회 ("ELLI-05" → 접두사 "ELLI" → ELLI-01)
            string prefix = themeOrCardId.Contains("-")
                ? themeOrCardId.Split('-')[0]
                : themeOrCardId;

            foreach (var raw in _characterCardsById.Values)
            {
                if (raw == null || string.IsNullOrEmpty(raw.id)) continue;
                if (!raw.id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;

                characterCardId = raw.id;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 용병(캐릭터) 카드를 UI 표시용 Card 객체로 만들어 반환한다. 없으면 null.
        ///
        /// 캐릭터 카드는 <see cref="AllCards"/>에 등록되지 않고 내부 사전에만 보관된다
        /// (효과 카드·자원 카드만 AllCards에 들어간다). 그래서 UI가 카드 ID로 이름·설명을
        /// 찾으려면 이 접근자가 필요하다.
        ///
        /// 반환값은 표시 전용 사본이다. 게임 상태로 쓰지 말 것.
        /// </summary>
        public Card GetCharacterCardView(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return null;
            if (!_characterCardsById.TryGetValue(cardId, out var raw) || raw == null) return null;

            return new Card
            {
                Id = raw.id,
                DataId = raw.id,
                Name = raw.name,
                CharacterId = raw.characterId,
                Description = raw.description,
                ImagePath = raw.imagePath,
                Type = CardType.Character
            };
        }

        /// <summary>Resources.Load용 경로로 imagePath를 정규화한다. (확장자·Assets/Resources/ 제거)</summary>
        public string GetCharacterResourcesPath(string cardId)
        {
            if (string.IsNullOrEmpty(cardId))
                return null;

            if (!_characterCardsById.TryGetValue(cardId, out var raw) || string.IsNullOrEmpty(raw.imagePath))
                return null;

            return NormalizeResourcesPath(raw.imagePath);
        }

        private static string NormalizeResourcesPath(string originalPath)
        {
            if (string.IsNullOrEmpty(originalPath))
                return null;

            string path = originalPath
                .Replace("Assets/Resources/", "")
                .Replace(".png", "")
                .Replace(".jpg", "");

            return path;
        }

        private class RawResourceCard
        {
            public string id;
            public string name;
            public string type;
            public int speed;
            public int cost;
            public string description;
            public string imagePath;
        }

        private class ResourceCardWrapper
        {
            public List<RawResourceCard> ResourceCards;
        }

        /// Data/ResourceCards.json 을 읽어 AllCards에 추가한다.
        public void LoadResourceCards()
        {
            var wrapper = ReadJson<ResourceCardWrapper>("ResourceCards");
            // var wrapper = ReadJson<ResourceCardWrapper>(basePath + "/ResourceCards.json");
            if (wrapper?.ResourceCards == null)
            {
                Console.WriteLine("[System] ResourceCards.json 없음, 자원 카드 로드 생략.");
                return;
            }

            foreach (var raw in wrapper.ResourceCards)
            {
                CardSpeed speed = raw.speed switch
                {
                    1 => CardSpeed.Speed1,
                    2 => CardSpeed.Speed2,
                    3 => CardSpeed.Speed3,
                    _ => CardSpeed.None
                };

                if (!Enum.TryParse<CardType>(raw.type, out CardType cardType)) continue;

                var card = new Card
                {
                    Id = raw.id,
                    DataId = raw.id,
                    Name = raw.name,
                    Type = cardType,
                    Speed = speed,
                    Cost = raw.cost,
                    OriginalCost = raw.cost,
                    Description = raw.description ?? "",
                    ImagePath = raw.imagePath ?? "",
                };
                AllCards[card.Id] = card;
            }
            Console.WriteLine($"[System] 자원 카드 {wrapper.ResourceCards.Count}종 로드 완료.");
        }

        private class RulebookCardWrapper
        {
            public List<RawRulebookCard> Card;
        }

        /// <summary>Data/RulebookCards.json 을 읽어 AllCards에 추가한다.</summary>
        public void LoadRulebookCards()
        {
            var wrapper = ReadJson<RulebookCardWrapper>("RulebookCards");
            // var wrapper = ReadJson<RulebookCardWrapper>(basePath + "/RulebookCards.json");
            if (wrapper?.Card == null)
            {
                Console.WriteLine("[System] RulebookCards.json 없음, 룰북 카드 로드 생략.");
                return;
            }

            int loaded = 0;
            foreach (var raw in wrapper.Card)
            {
                CardSpeed speed = raw.speed switch
                {
                    1 => CardSpeed.Speed1,
                    2 => CardSpeed.Speed2,
                    3 => CardSpeed.Speed3,
                    _ => CardSpeed.None
                };

                if (!Enum.TryParse<CardType>(raw.type, out CardType cardType)) continue;

                var card = new Card
                {
                    Id = raw.id,
                    DataId = raw.id,
                    Name = raw.name,
                    Type = cardType,
                    Speed = speed,
                    Cost = raw.cost,
                    OriginalCost = raw.cost,
                    Description = raw.description,
                    CharacterId = raw.characterId,
                    IsStack = raw.isStack,
                    IsBattlefield = raw.isBattlefield,
                    CannotBePlayedByEffect = raw.cannotBePlayedByEffect,
                    ImagePath = raw.imagePath ?? "",
                };

                foreach (var eff in raw.effects ?? new List<RawRulebookEffect>())
                {
                    var effect = CreateKeywordEffect(eff);
                    if (effect != null) card.Effects.Add(effect);
                }

                AllCards[card.Id] = card;
                loaded++;
            }
            Console.WriteLine($"[System] 룰북 카드 {loaded}종 로드 완료.");
        }

        // ─── CreateKeywordEffect ──────────────────────────────────────────────
        // JSON의 기존 type 이름을 받아 새로운 통합 Effect 클래스 인스턴스를 반환한다.
        // 파라미터 Dictionary를 빌드하여 Initialize()로 전달하거나
        // BattlefieldEffect 같이 sub-effect가 필요한 경우 직접 속성을 설정한다.

        private ICardEffect CreateKeywordEffect(RawRulebookEffect raw)
        {
            // ── 데미지 계열 ────────────────────────────────────────────────────
            if (raw.type == "DamageEffect" ||
                raw.type == "PiercingDamageEffect" ||
                raw.type == "MultiHitDamageEffect" ||
                raw.type == "SelfDamageEffect")
            {
                var p = new Dictionary<string, object>();
                if (raw.amount != 0) p["amount"] = raw.amount;
                if (raw.type == "PiercingDamageEffect") p["isPiercing"] = true;
                if (raw.type == "MultiHitDamageEffect" && raw.times != 0) p["times"] = raw.times;
                if (raw.type == "SelfDamageEffect") p["targetSelf"] = true;

                // ★ 공통 플래그 전달
                p["isStackAction"] = raw.isStackAction;
                p["requirePreviousSuccess"] = raw.requirePreviousSuccess;
                var e = new DamageEffect();
                e.Initialize(p);
                return e;
            }

            // ── 버프 계열 ─────────────────────────────────────────────────────
            if (raw.type == "ArmorEffect" ||
                raw.type == "SuperArmorEffect" ||
                raw.type == "InvincibilityEffect" ||
                raw.type == "FirepowerEffect" ||
                raw.type == "CounterAttackEffect" ||
                raw.type == "NextTurnBuffEffect")
            {
                var p = new Dictionary<string, object>();

                string bt = raw.type switch
                {
                    "ArmorEffect" => "Armor",
                    "SuperArmorEffect" => "SuperArmor",
                    "InvincibilityEffect" => "Invincible",
                    "FirepowerEffect" => "Firepower",
                    "CounterAttackEffect" => "CounterAttack",
                    "NextTurnBuffEffect" => string.IsNullOrEmpty(raw.buffType) ? "Firepower" : raw.buffType,
                    _ => "Armor"
                };
                p["buffType"] = bt;

                if (raw.amount != 0) p["amount"] = raw.amount;

                if (!string.IsNullOrEmpty(raw.duration))
                    p["duration"] = raw.duration;
                else if (raw.type == "NextTurnBuffEffect")
                    p["duration"] = "NextTurn";

                // ★ 반격 성공 시 실행할 보상 전달
                if (!string.IsNullOrEmpty(raw.rewardOnSuccess))
                    p["rewardOnSuccess"] = raw.rewardOnSuccess;

                // ★ 공통 플래그 전달
                p["isStackAction"] = raw.isStackAction;
                p["requirePreviousSuccess"] = raw.requirePreviousSuccess;

                var e = new BuffEffect();
                e.Initialize(p);
                return e;
            }

            // ── 카드 이동 계열 ────────────────────────────────────────────────
            if (raw.type == "DrawEffect" ||
                raw.type == "DiscardFromHandEffect" ||
                raw.type == "ResourceGainEffect" ||
                raw.type == "RecoverFromDiscardEffect" ||
                raw.type == "ShuffleReturnEffect" ||
                raw.type == "TopDeckToDiscardEffect" ||
                raw.type == "SearchDeckEffect" ||
                raw.type == "ResourceFromDiscardEffect" ||
                raw.type == "ReturnFromDiscardEffect")
            {
                var p = BuildMoveParams(raw);
                var e = new MoveEffect();
                e.Initialize(p);
                return e;
            }

            // ── 자기 자신 배치 ────────────────────────────────────────────────
            if (raw.type == "SelfAsResourceEffect")
            {
                var p = new Dictionary<string, object> { ["to"] = "ResourceZone" };
                // ★ 공통 플래그 전달
                p["isStackAction"] = raw.isStackAction;
                p["requirePreviousSuccess"] = raw.requirePreviousSuccess;

                var e = new SelfPlaceEffect();
                e.Initialize(p);
                return e;
            }

            // ── 전장 계열 ─────────────────────────────────────────────────────
            if (raw.type == "BattlefieldEffect" ||
                raw.type == "ArmorBattlefieldEffect" ||
                raw.type == "FirepowerBattlefieldEffect" ||
                raw.type == "PeriodicRecoveryBattlefieldEffect" ||
                raw.type == "CostReductionBattlefieldEffect")
            {
                return BuildBattlefieldEffect(raw);
            }

            // ── 선택 행동 ─────────────────────────────────────────────────────
            if (raw.type == "OptionalActionEffect" || raw.type == "optional_action")
            {
                var p = new Dictionary<string, object>
                {
                    ["isStackAction"] = raw.isStackAction,
                    ["requirePreviousSuccess"] = raw.requirePreviousSuccess
                };
                if (!string.IsNullOrEmpty(raw.description))
                    p["description"] = raw.description;
                if (raw.action != null)
                {
                    ICardEffect inner = CreateKeywordEffect(raw.action);
                    if (inner != null)
                        p["action"] = inner;
                }

                var optional = new OptionalActionEffect();
                optional.Initialize(p);
                return optional;
            }

            // ── 복합 효과: 폐기존 카드 선택 → 임시 코스트 감소 → PlayBuffer에서 발동 ───── (다이나: "기뢰")
            if (raw.type == "ReplayCardEffect")
            {
                int reduction = raw.costReduction != 0 ? raw.costReduction : 1;
                string filter = string.IsNullOrEmpty(raw.filter) ? "type:Effect" : raw.filter;
                filter += ",replayable:true"; // 간접 사용 불가능인 카드는 제외합니다(소니아: "신재생에너지")

                // Step 1: 폐기존에서 효과 카드 1장 → PlayBuffer
                var moveParams = new Dictionary<string, object>
                {
                    ["from"] = "Graveyard",
                    ["to"] = "PlayBuffer",
                    ["mode"] = "Choose",
                    ["count"] = 1,
                    ["filter"] = filter,
                    ["excludeSelf"] = true, // 자기 자신 제외

                    // ★ 고른 카드를 곧바로 발동시키는 효과다.
                    //   낼 수 없는 카드를 후보에 두면 코스트도 안 내고 효과만 터진다.
                    //   실제로 깎일 양(reduction)까지 넘겨야 후보와 결과가 어긋나지 않는다.
                    ["requireAffordable"] = true,
                    ["costReduction"] = reduction
                };
                var moveEff = new MoveEffect();
                moveEff.Initialize(moveParams);

                // Step 2: PlayBuffer 카드 코스트 임시 감소
                var tempCostParams = new Dictionary<string, object> { ["reduction"] = reduction };
                var tempCostEff = new TemporaryCostEffect();
                tempCostEff.Initialize(tempCostParams);

                // Step 3: PlayBuffer 카드 발동 후 폐기존으로
                var playEff = new PlayFromBufferEffect();
                playEff.Initialize(new Dictionary<string, object>());

                var composite = new CompositeEffect();
                composite.Initialize(new Dictionary<string, object>());
                composite.AddStep(moveEff);
                composite.AddStep(tempCostEff);
                composite.AddStep(playEff);
                return composite;
            }

            Console.WriteLine($"[GameDataManager] 알 수 없는 Effect 타입: {raw.type}");
            return null;
        }

        // ─── MoveEffect 파라미터 빌드 ─────────────────────────────────────────

        private Dictionary<string, object> BuildMoveParams(RawRulebookEffect raw)
        {
            var p = new Dictionary<string, object>();
            // ★ 공통 플래그 전달 (가장 위에 추가해 주세요)
            p["isStackAction"] = raw.isStackAction;
            p["requirePreviousSuccess"] = raw.requirePreviousSuccess;

            switch (raw.type)
            {
                case "DrawEffect":
                    p["from"] = "Deck";
                    p["to"] = "Hand";
                    p["mode"] = "Top";
                    p["count"] = raw.count != 0 ? raw.count : 1;
                    break;

                case "DiscardFromHandEffect":
                    p["from"] = "Hand";
                    p["to"] = "Graveyard";
                    p["mode"] = string.IsNullOrEmpty(raw.mode) ? "Random" : Capitalize(raw.mode);
                    p["count"] = raw.count != 0 ? raw.count : 1;
                    break;

                case "ResourceGainEffect":
                    p["from"] = "ResourceDeck";
                    p["to"] = "ResourceZone";
                    p["mode"] = "Top";
                    p["count"] = raw.count != 0 ? raw.count : 1;
                    break;

                case "RecoverFromDiscardEffect":
                    p["from"] = "Graveyard";
                    p["to"] = "Hand";
                    // 선택 UI가 없던 시절 "Random"으로 박아 둔 곳이다.
                    // 폐기존은 공개 정보이고 카드 설명에 "랜덤"이 없으므로 플레이어가 고른다.
                    p["mode"] = string.IsNullOrEmpty(raw.mode) ? "Choose" : Capitalize(raw.mode);
                    p["count"] = raw.count != 0 ? raw.count : 1;
                    if (!string.IsNullOrEmpty(raw.filter)) p["filter"] = raw.filter;
                    break;

                case "ShuffleReturnEffect":
                    p["from"] = "Hand";
                    p["to"] = "Deck";
                    // JSON은 "mode": "choose"라고 말하는데 여기서 "Random"으로 덮어쓰고 있었다.
                    p["mode"] = string.IsNullOrEmpty(raw.mode) ? "Choose" : Capitalize(raw.mode);
                    p["count"] = raw.count != 0 ? raw.count : 1;
                    p["shuffleAfter"] = true;
                    break;

                case "TopDeckToDiscardEffect":
                    p["from"] = "Deck";
                    p["to"] = "Graveyard";
                    p["mode"] = "Top";
                    p["count"] = raw.count != 0 ? raw.count : 1;
                    break;

                case "SearchDeckEffect":
                    p["from"] = "Deck";
                    p["to"] = "Hand";
                    p["mode"] = string.IsNullOrEmpty(raw.mode) ? "Choose" : Capitalize(raw.mode);
                    // p["mode"] = "First";
                    p["count"] = raw.count != 0 ? raw.count : 1;
                    p["shuffleAfter"] = true;
                    if (!string.IsNullOrEmpty(raw.filter)) p["filter"] = raw.filter;
                    break;

                case "ResourceFromDiscardEffect":
                    p["from"] = "Graveyard";
                    p["to"] = "ResourceZone";
                    // 자원카드는 전부 RES-01의 동일 복제본이라 무엇을 골라도 결과가 같다.
                    // 구별되지 않는 카드에 선택창을 띄우는 건 조작 비용만 늘린다.
                    p["mode"] = string.IsNullOrEmpty(raw.mode) ? "Top" : Capitalize(raw.mode);
                    p["count"] = raw.count != 0 ? raw.count : 99;  // 기본 전부
                    p["filter"] = "type:Resource";
                    break;

                case "ReturnFromDiscardEffect":
                    p["from"] = "Graveyard";
                    p["to"] = "Deck";
                    p["mode"] = string.IsNullOrEmpty(raw.mode) ? "Choose" : Capitalize(raw.mode);
                    // p["mode"] = "Last";
                    p["count"] = raw.count != 0 ? raw.count : 1;
                    p["filter"] = string.IsNullOrEmpty(raw.filter) ? "type:Effect" : raw.filter;
                    p["shuffleAfter"] = true;
                    p["excludeSelf"] = true;
                    break;
            }

            return p;
        }

        // ─── BattlefieldEffect 빌드 ────────────────────────────────────────────

        private BattlefieldEffect BuildBattlefieldEffect(RawRulebookEffect raw)
        {
            var bf = new BattlefieldEffect();
            bf.Initialize(new Dictionary<string, object>());

            switch (raw.type)
            {
                case "ArmorBattlefieldEffect": // 베로니카: 체크메이트
                    {
                        int amt = raw.amount != 0 ? raw.amount : 1;
                        var buffParams = new Dictionary<string, object>
                        { ["buffType"] = "BattlefieldArmor", ["amount"] = amt };
                        var buffEff = new BuffEffect();
                        buffEff.Initialize(buffParams);
                        bf.PerResourcePhaseEffect = buffEff;
                        break;
                    }

                case "FirepowerBattlefieldEffect": // 다이나: 조선소
                    {
                        int amt = raw.amount != 0 ? raw.amount : 1;
                        var buffParams = new Dictionary<string, object>
                        { ["buffType"] = "BattlefieldFirepower", ["amount"] = amt };
                        var buffEff = new BuffEffect();
                        buffEff.Initialize(buffParams);
                        bf.PerResourcePhaseEffect = buffEff;
                        break;
                    }

                case "PeriodicRecoveryBattlefieldEffect": // 엘리: 무작위 노획
                    {
                        string filter = string.IsNullOrEmpty(raw.filter) ? "type:Attack" : raw.filter;
                        var moveParams = new Dictionary<string, object>
                        {
                            ["from"] = "Graveyard",
                            ["to"] = "Hand",
                            ["mode"] = "Random",
                            ["count"] = 1,
                            ["filter"] = filter
                        };
                        var moveEff = new MoveEffect();
                        moveEff.Initialize(moveParams);

                        // "랜덤으로 가져올 수 있다" — 랜덤은 카드 설계가 맞지만,
                        // "할 수 있다"는 거부권이므로 매 자원페이즈 의사를 묻는다.
                        var optionalRecover = new OptionalActionEffect();
                        optionalRecover.Initialize(new Dictionary<string, object>
                        {
                            ["description"] = "폐기존에서 공격 카드 1장을 무작위로 가져오시겠습니까?",
                            ["action"] = moveEff
                        });

                        // 폐기존에 가져올 카드가 없으면 아예 묻지 않는다.
                        optionalRecover.Precondition = ctx =>
                        {
                            Player owner = ctx?.ActivePlayer;
                            if (owner == null) return false;
                            return CardSelector.ApplyFilter(owner.Graveyard.ToList(), filter).Count > 0;
                        };

                        bf.PerResourcePhaseEffect = optionalRecover;
                        break;
                    }

                case "CostReductionBattlefieldEffect": // 소니아: 노을지는 활주로
                    bf.CostReductionFilter = raw.filter ?? "";
                    bf.CostReduction = raw.reduction != 0 ? raw.reduction : 1;
                    break;

                    // "BattlefieldEffect": 단순 전장 배치 (내부 효과 없음)
            }

            return bf;
        }

        /// <summary>
        /// 지정 캐릭터의 효과 카드 ID 목록 반환 (캐릭터 카드 제외).
        /// 예: "ELLI-01" → ["ELLI-02", "ELLI-03", ..., "ELLI-11"]
        /// </summary>
        public List<string> GetEffectCardIdsForCharacter(string characterCardId)
        {
            if (string.IsNullOrEmpty(characterCardId)) return new List<string>();
            int dashIdx = characterCardId.IndexOf('-');
            string prefix = dashIdx > 0 ? characterCardId.Substring(0, dashIdx) : characterCardId;

            return AllCards.Keys
                .Where(k => k.StartsWith(prefix + "-") && k != characterCardId)
                .OrderBy(x => x)
                .ToList();
        }

        // ─── 공통 유틸 ────────────────────────────────────────────────────────

        private string Capitalize(string s)
            => string.IsNullOrEmpty(s)
                ? s
                : char.ToUpper(s[0]) + s.Substring(1);

        private T ReadJson<T>(string fileName)
        {
            string json = _jsonLoader.LoadJson(fileName);
            if (string.IsNullOrEmpty(json)) return default;

            return JsonConvert.DeserializeObject<T>(json);
            /* ★ 기존 File.ReadAllText 방식에서 IJsonLoader 인터페이스로 변경하여, Unity 리소스 로딩과 콘솔 파일 로딩을 모두 지원합니다.
            if (!File.Exists(path)) return default;
            string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
            return JsonConvert.DeserializeObject<T>(json);*/
        }
    }
}