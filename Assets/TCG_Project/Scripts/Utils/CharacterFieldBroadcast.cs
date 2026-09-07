using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;
using TCG_Project.Scripts.Systems;

namespace TCG_Project.Scripts.Utils
{
    /// <summary>
    /// 용병(캐릭터) 필드 슬롯 스냅샷 생성 및 EventManager 방송.
    ///
    /// RotationZ 규칙: 하단(P1)=0° 시작, 상단(P2)=180° 시작, 능력 사용 시 +180°.
    ///
    /// ★ 단, 유니티 화면은 더 이상 그 +180°를 회전으로 그리지 않는다 —
    ///   능력을 썼다는 표시는 <b>흑백 처리</b>로 바뀌었고(CharacterFieldUI),
    ///   화면은 AbilityUsed 를 보고 방향은 스스로 계산한다.
    ///   RotationZ 는 온라인 DTO 호환을 위해 그대로 유지한다.
    /// </summary>
    public static class CharacterFieldBroadcast
    {
        private static Player _bottomPlayer;
        private static GameDataManager _dataManager;

        public static void Register(Player bottomPlayer, GameDataManager dataManager)
        {
            _bottomPlayer = bottomPlayer;
            _dataManager = dataManager;
        }

        public static float GetRotationZ(Player owner, Player bottomPlayer, string characterCardId)
        {
            if (owner == null || string.IsNullOrEmpty(characterCardId))
                return 0f;

            bool isBottom = owner == bottomPlayer;
            bool used = owner.HasUsedCharacterAbility(characterCardId);
            float baseZ = isBottom ? 0f : 180f;
            return used ? (baseZ + 180f) % 360f : baseZ;
        }

        public static CharacterSlotSnapshot BuildSnapshot(
            Player owner,
            CharacterSlotType slot,
            string characterCardId,
            Player bottomPlayer,
            GameDataManager dataManager)
        {
            bool used = !string.IsNullOrEmpty(characterCardId)
                && owner.HasUsedCharacterAbility(characterCardId);

            return new CharacterSlotSnapshot(
                owner,
                slot,
                characterCardId,
                dataManager?.GetCharacterResourcesPath(characterCardId),
                GetRotationZ(owner, bottomPlayer, characterCardId),
                used);
        }

        public static List<CharacterSlotSnapshot> BuildAllSnapshots(
            Player p1,
            Player p2,
            GameDataManager dataManager)
        {
            var snapshots = new List<CharacterSlotSnapshot>(4);

            if (p1 != null)
            {
                snapshots.Add(BuildSnapshot(p1, CharacterSlotType.Main, p1.CharacterCardId, p1, dataManager));
                snapshots.Add(BuildSnapshot(p1, CharacterSlotType.Sub, p1.SecondaryCharacterId, p1, dataManager));
            }

            if (p2 != null)
            {
                snapshots.Add(BuildSnapshot(p2, CharacterSlotType.Main, p2.CharacterCardId, p1, dataManager));
                snapshots.Add(BuildSnapshot(p2, CharacterSlotType.Sub, p2.SecondaryCharacterId, p1, dataManager));
            }

            return snapshots;
        }

        public static void SyncAll(Player p1, Player p2, GameDataManager dataManager)
        {
            var snapshots = BuildAllSnapshots(p1, p2, dataManager);
            EventManager.OnCharacterFieldSync?.Invoke(snapshots);
        }

        public static void EmitSlotUpdate(Player owner, string characterCardId)
        {
            if (owner == null || string.IsNullOrEmpty(characterCardId) || _dataManager == null)
                return;

            CharacterSlotType slot = owner.CharacterCardId == characterCardId
                ? CharacterSlotType.Main
                : CharacterSlotType.Sub;

            var snapshot = BuildSnapshot(owner, slot, characterCardId, _bottomPlayer, _dataManager);
            EventManager.OnCharacterSlotUpdated?.Invoke(snapshot);
        }
    }
}
