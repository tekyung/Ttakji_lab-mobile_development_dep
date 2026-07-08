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
        public readonly IJsonLoader _jsonLoader; // JSON ·Îµù ´ã´ç

        // »ý¼ºÀÚ¿¡¼­ ½ÉºÎ¸§²ÛÀ» °­Á¦·Î ¹Þµµ·Ï ¼³Á¤
        public GameDataManager(IJsonLoader jsonLoader)
        {
            _jsonLoader = jsonLoader;
        }

        // ¦¡¦¡¦¡ ·êºÏ Ä«µå ·Î´õ ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡

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
            public string rewardOnSuccess; // ¡Ú ¹Ý°Ý ¼º°ø ½Ã ½ÇÇàÇÒ º¸»ó (¿¹: "DAIN-09"ÀÇ ¸®º¥Áö È¿°ú)
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
            public bool cannotBePlayedByEffect; // ´Ù¸¥ Ä«µå¸¦ ÅëÇÑ °£Á¢ »ç¿ëÀÌ °¡´ÉÇÑ°¡?
            public string imagePath;
            public List<RawRulebookEffect> effects;
        }

        // ¦¡¦¡¦¡ Ä³¸¯ÅÍ Ä«µå ·Î´õ ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡

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

        /// <summary>Data/Character.json À» ÀÐ¾î Ä³¸¯ÅÍ Ä«µå µ¥ÀÌÅÍ¸¦ ·ÎµåÇÑ´Ù.</summary>
        public void LoadCharacterCards()
        {
            var wrapper = ReadJson<CharacterCardWrapper>("Character");
            //var wrapper = ReadJson<CharacterCardWrapper>(basePath + "/Character.json");
            if (wrapper?.Characters == null)
            {
                Console.WriteLine("[System] Character.json ¾øÀ½, Ä³¸¯ÅÍ Ä«µå ·Îµå »ý·«.");
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

            Console.WriteLine($"[System] Ä³¸¯ÅÍ Ä«µå {_characterCards.Count}Á¾ ·Îµå ¿Ï·á.");
        }

        /// <summary>Resources.Load¿ë °æ·Î·Î imagePath¸¦ Á¤±ÔÈ­ÇÑ´Ù. (È®ÀåÀÚ¡¤Assets/Resources/ Á¦°Å)</summary>
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

        /// Data/ResourceCards.json À» ÀÐ¾î AllCards¿¡ Ãß°¡ÇÑ´Ù.
        public void LoadResourceCards()
        {
            var wrapper = ReadJson<ResourceCardWrapper>("ResourceCards");
            // var wrapper = ReadJson<ResourceCardWrapper>(basePath + "/ResourceCards.json");
            if (wrapper?.ResourceCards == null)
            {
                Console.WriteLine("[System] ResourceCards.json ¾øÀ½, ÀÚ¿ø Ä«µå ·Îµå »ý·«.");
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
            Console.WriteLine($"[System] ÀÚ¿ø Ä«µå {wrapper.ResourceCards.Count}Á¾ ·Îµå ¿Ï·á.");
        }

        private class RulebookCardWrapper
        {
            public List<RawRulebookCard> Card;
        }

        /// <summary>Data/RulebookCards.json À» ÀÐ¾î AllCards¿¡ Ãß°¡ÇÑ´Ù.</summary>
        public void LoadRulebookCards()
        {
            var wrapper = ReadJson<RulebookCardWrapper>("RulebookCards");
            // var wrapper = ReadJson<RulebookCardWrapper>(basePath + "/RulebookCards.json");
            if (wrapper?.Card == null)
            {
                Console.WriteLine("[System] RulebookCards.json ¾øÀ½, ·êºÏ Ä«µå ·Îµå »ý·«.");
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
            Console.WriteLine($"[System] ·êºÏ Ä«µå {loaded}Á¾ ·Îµå ¿Ï·á.");
        }

        // ¦¡¦¡¦¡ CreateKeywordEffect ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
        // JSONÀÇ ±âÁ¸ type ÀÌ¸§À» ¹Þ¾Æ »õ·Î¿î ÅëÇÕ Effect Å¬·¡½º ÀÎ½ºÅÏ½º¸¦ ¹ÝÈ¯ÇÑ´Ù.
        // ÆÄ¶ó¹ÌÅÍ Dictionary¸¦ ºôµåÇÏ¿© Initialize()·Î Àü´ÞÇÏ°Å³ª
        // BattlefieldEffect °°ÀÌ sub-effect°¡ ÇÊ¿äÇÑ °æ¿ì Á÷Á¢ ¼Ó¼ºÀ» ¼³Á¤ÇÑ´Ù.

        private ICardEffect CreateKeywordEffect(RawRulebookEffect raw)
        {
            // ¦¡¦¡ µ¥¹ÌÁö °è¿­ ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
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

                // ¡Ú °øÅë ÇÃ·¡±× Àü´Þ
                p["isStackAction"] = raw.isStackAction;
                p["requirePreviousSuccess"] = raw.requirePreviousSuccess;
                var e = new DamageEffect();
                e.Initialize(p);
                return e;
            }

            // ¦¡¦¡ ¹öÇÁ °è¿­ ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
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

                // ¡Ú ¹Ý°Ý ¼º°ø ½Ã ½ÇÇàÇÒ º¸»ó Àü´Þ
                if (!string.IsNullOrEmpty(raw.rewardOnSuccess))
                    p["rewardOnSuccess"] = raw.rewardOnSuccess;

                // ¡Ú °øÅë ÇÃ·¡±× Àü´Þ
                p["isStackAction"] = raw.isStackAction;
                p["requirePreviousSuccess"] = raw.requirePreviousSuccess;

                var e = new BuffEffect();
                e.Initialize(p);
                return e;
            }

            // ¦¡¦¡ Ä«µå ÀÌµ¿ °è¿­ ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
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

            // ¦¡¦¡ ÀÚ±â ÀÚ½Å ¹èÄ¡ ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
            if (raw.type == "SelfAsResourceEffect")
            {
                var p = new Dictionary<string, object> { ["to"] = "ResourceZone" };
                // ¡Ú °øÅë ÇÃ·¡±× Àü´Þ
                p["isStackAction"] = raw.isStackAction;
                p["requirePreviousSuccess"] = raw.requirePreviousSuccess;

                var e = new SelfPlaceEffect();
                e.Initialize(p);
                return e;
            }

            // ¦¡¦¡ ÀüÀå °è¿­ ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
            if (raw.type == "BattlefieldEffect" ||
                raw.type == "ArmorBattlefieldEffect" ||
                raw.type == "FirepowerBattlefieldEffect" ||
                raw.type == "PeriodicRecoveryBattlefieldEffect" ||
                raw.type == "CostReductionBattlefieldEffect")
            {
                return BuildBattlefieldEffect(raw);
            }

            // ¦¡¦¡ º¹ÇÕ È¿°ú: Æó±âÁ¸ Ä«µå ¼±ÅÃ ¡æ ÀÓ½Ã ÄÚ½ºÆ® °¨¼Ò ¡æ PlayBuffer¿¡¼­ ¹ßµ¿ ¦¡¦¡¦¡¦¡¦¡ (´ÙÀÌ³ª: "±â·Ú")
            if (raw.type == "ReplayCardEffect")
            {
                int reduction = raw.costReduction != 0 ? raw.costReduction : 1;
                string filter = string.IsNullOrEmpty(raw.filter) ? "type:Effect" : raw.filter;
                filter += ",replayable:true"; // °£Á¢ »ç¿ë ºÒ°¡´ÉÀÎ Ä«µå´Â Á¦¿ÜÇÕ´Ï´Ù(¼Ò´Ï¾Æ: "½ÅÀç»ý¿¡³ÊÁö")

                // Step 1: Æó±âÁ¸¿¡¼­ È¿°ú Ä«µå 1Àå ¡æ PlayBuffer
                var moveParams = new Dictionary<string, object>
                {
                    ["from"] = "Graveyard",
                    ["to"] = "PlayBuffer",
                    ["mode"] = "Choose",
                    ["count"] = 1,
                    ["filter"] = filter,
                    ["excludeSelf"] = true // ÀÚ±â ÀÚ½Å Á¦¿Ü
                };
                var moveEff = new MoveEffect();
                moveEff.Initialize(moveParams);

                // Step 2: PlayBuffer Ä«µå ÄÚ½ºÆ® ÀÓ½Ã °¨¼Ò
                var tempCostParams = new Dictionary<string, object> { ["reduction"] = reduction };
                var tempCostEff = new TemporaryCostEffect();
                tempCostEff.Initialize(tempCostParams);

                // Step 3: PlayBuffer Ä«µå ¹ßµ¿ ÈÄ Æó±âÁ¸À¸·Î
                var playEff = new PlayFromBufferEffect();
                playEff.Initialize(new Dictionary<string, object>());

                var composite = new CompositeEffect();
                composite.Initialize(new Dictionary<string, object>());
                composite.AddStep(moveEff);
                composite.AddStep(tempCostEff);
                composite.AddStep(playEff);
                return composite;
            }

            Console.WriteLine($"[GameDataManager] ¾Ë ¼ö ¾ø´Â Effect Å¸ÀÔ: {raw.type}");
            return null;
        }

        // ¦¡¦¡¦¡ MoveEffect ÆÄ¶ó¹ÌÅÍ ºôµå ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡

        private Dictionary<string, object> BuildMoveParams(RawRulebookEffect raw)
        {
            var p = new Dictionary<string, object>();
            // ¡Ú °øÅë ÇÃ·¡±× Àü´Þ (°¡Àå À§¿¡ Ãß°¡ÇØ ÁÖ¼¼¿ä)
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
                    p["count"] = raw.count != 0 ? raw.count : 99;  // ±âº» ÀüºÎ
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

        // ¦¡¦¡¦¡ BattlefieldEffect ºôµå ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡

        private BattlefieldEffect BuildBattlefieldEffect(RawRulebookEffect raw)
        {
            var bf = new BattlefieldEffect();
            bf.Initialize(new Dictionary<string, object>());

            switch (raw.type)
            {
                case "ArmorBattlefieldEffect": // º£·Î´ÏÄ«: Ã¼Å©¸ÞÀÌÆ®
                    {
                        int amt = raw.amount != 0 ? raw.amount : 1;
                        var buffParams = new Dictionary<string, object>
                        { ["buffType"] = "BattlefieldArmor", ["amount"] = amt };
                        var buffEff = new BuffEffect();
                        buffEff.Initialize(buffParams);
                        bf.PerResourcePhaseEffect = buffEff;
                        break;
                    }

                case "FirepowerBattlefieldEffect": // ´ÙÀÌ³ª: Á¶¼±¼Ò
                    {
                        int amt = raw.amount != 0 ? raw.amount : 1;
                        var buffParams = new Dictionary<string, object>
                        { ["buffType"] = "BattlefieldFirepower", ["amount"] = amt };
                        var buffEff = new BuffEffect();
                        buffEff.Initialize(buffParams);
                        bf.PerResourcePhaseEffect = buffEff;
                        break;
                    }

                case "PeriodicRecoveryBattlefieldEffect": // ¿¤¸®: ¹«ÀÛÀ§ ³ëÈ¹
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

                case "CostReductionBattlefieldEffect": // ¼Ò´Ï¾Æ: ³ëÀ»Áö´Â È°ÁÖ·Î
                    bf.CostReductionFilter = raw.filter ?? "";
                    bf.CostReduction = raw.reduction != 0 ? raw.reduction : 1;
                    break;

                    // "BattlefieldEffect": ´Ü¼ø ÀüÀå ¹èÄ¡ (³»ºÎ È¿°ú ¾øÀ½)
            }

            return bf;
        }

        /// <summary>
        /// ÁöÁ¤ Ä³¸¯ÅÍÀÇ È¿°ú Ä«µå ID ¸ñ·Ï ¹ÝÈ¯ (Ä³¸¯ÅÍ Ä«µå Á¦¿Ü).
        /// ¿¹: "ELLI-01" ¡æ ["ELLI-02", "ELLI-03", ..., "ELLI-11"]
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

        // ¦¡¦¡¦¡ °øÅë À¯Æ¿ ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡

        private string Capitalize(string s)
            => string.IsNullOrEmpty(s)
                ? s
                : char.ToUpper(s[0]) + s.Substring(1);

        private T ReadJson<T>(string fileName)
        {
            string json = _jsonLoader.LoadJson(fileName);
            if (string.IsNullOrEmpty(json)) return default;

            return JsonConvert.DeserializeObject<T>(json);
            /* ¡Ú ±âÁ¸ File.ReadAllText ¹æ½Ä¿¡¼­ IJsonLoader ÀÎÅÍÆäÀÌ½º·Î º¯°æÇÏ¿©, Unity ¸®¼Ò½º ·Îµù°ú ÄÜ¼Ö ÆÄÀÏ ·ÎµùÀ» ¸ðµÎ Áö¿øÇÕ´Ï´Ù.
            if (!File.Exists(path)) return default;
            string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
            return JsonConvert.DeserializeObject<T>(json);*/
        }
    }
}