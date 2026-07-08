// CharacterAbilityRegistry.cs — Phase 14: ID → 능력 인스턴스 매핑
using System.Collections.Generic;
using TCG_Project.Scripts.Core;

namespace TCG_Project.Scripts.Abilities
{
    public static class CharacterAbilityRegistry
    {
        private static readonly Dictionary<string, ICharacterAbility> _registry = new Dictionary<string, ICharacterAbility>
        {
            ["ELLI-01"] = new EllieAbility(),
            ["VERO-01"] = new VeronicaAbility(),
            ["DAIN-01"] = new DainaAbility(),
            ["SONI-01"] = new SoniaAbility(),
        };

        public static ICharacterAbility Get(string characterCardId)
        {
            if (string.IsNullOrEmpty(characterCardId)) return null;
            return _registry.TryGetValue(characterCardId, out var ability) ? ability : null;
        }

        // 플레이어의 주 캐릭터와 부 캐릭터 능력을 모두 찾아 리스트로 반환
        public static List<ICharacterAbility> GetPlayerAbilities(Player player)
        {
            var abilities = new List<ICharacterAbility>();

            if (!string.IsNullOrEmpty(player.CharacterCardId) && _registry.TryGetValue(player.CharacterCardId, out var primary))
                abilities.Add(primary);

            if (!string.IsNullOrEmpty(player.SecondaryCharacterId) && _registry.TryGetValue(player.SecondaryCharacterId, out var secondary))
                abilities.Add(secondary);

            return abilities;
        }
    }
}
