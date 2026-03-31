using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System.IO;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Effects;
using TCG_Project.Scripts.Interfaces;

namespace TCG_Project.Scripts.Systems
{
    public class GameDataManager
    {
        public Dictionary<string, Card> AllCards { get; private set; } = new Dictionary<string, Card>();

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
            public string rewardOnSuccess; // ★ 반격 성공 시 실행할 보상 (예: "DAIN-09"의 리벤지 효과)
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

        /// <summary>Data/Character.json 을 읽어 캐릭터 카드 데이터를 로드한다.</summary>
        public void LoadCharacterCards(string basePath)
        {
            var wrapper = ReadJson<CharacterCardWrapper>(basePath + "/Character.json");
            if (wrapper?.Characters == null)
            {
                Console.WriteLine("[System] Character.json 없음, 캐릭터 카드 로드 생략.");
                return;
            }

            foreach (var raw in wrapper.Characters)
                _characterCards[raw.characterId] = raw;

            Console.WriteLine($"[System] 캐릭터 카드 {_characterCards.Count}종 로드 완료.");
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
        public void LoadResourceCards(string basePath)
        {
            var wrapper = ReadJson<ResourceCardWrapper>(basePath + "/ResourceCards.json");
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
        public void LoadRulebookCards(string basePath)
        {
            var wrapper = ReadJson<RulebookCardWrapper>(basePath + "/RulebookCards.json");
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
                    ["excludeSelf"] = true // 자기 자신 제외
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
                    p["mode"] = "Random";
                    p["count"] = raw.count != 0 ? raw.count : 1;
                    if (!string.IsNullOrEmpty(raw.filter)) p["filter"] = raw.filter;
                    break;

                case "ShuffleReturnEffect":
                    p["from"] = "Hand";
                    p["to"] = "Deck";
                    p["mode"] = "Random";
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
                    p["mode"] = string.IsNullOrEmpty(raw.mode) ? "Choose" : Capitalize(raw.mode);
                    // p["mode"] = "Top";
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
                        bf.PerResourcePhaseEffect = moveEff;
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

        private T ReadJson<T>(string path)
        {
            if (!File.Exists(path)) return default;
            string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}
