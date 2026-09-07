using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;
using UnityEngine;
using UnityEngine.UI;

public class CharacterFieldUI : MonoBehaviour
{
    [Header("P1 (bottom field)")]
    [SerializeField] private Image p1MainSlot;
    [SerializeField] private Image p1SubSlot;

    [Header("P2 (top field)")]
    [SerializeField] private Image p2MainSlot;
    [SerializeField] private Image p2SubSlot;

    [Header("고유 능력을 쓴 표시")]
    [Tooltip("능력을 쓴 용병 카드에 씌울 흑백 머티리얼. 비우면 Resources/Materials/UIGrayscale 을 찾는다.")]
    [SerializeField] private Material usedMaterial;

    /// <summary>Resources에서 한 번만 찾아 둔다. 못 찾으면 다시 찾지 않는다.</summary>
    private bool _usedMaterialResolved;

    private Player _p1;
    private Player _p2;

    private void Awake()
    {
        ResolveMissingReferences();
    }

    private void OnEnable()
    {
        EventManager.OnGameStart += HandleGameStart;
        EventManager.OnCharacterFieldSync += HandleFieldSync;
        EventManager.OnCharacterSlotUpdated += HandleSlotUpdated;
    }

    private void OnDisable()
    {
        EventManager.OnGameStart -= HandleGameStart;
        EventManager.OnCharacterFieldSync -= HandleFieldSync;
        EventManager.OnCharacterSlotUpdated -= HandleSlotUpdated;
    }

    private void ResolveMissingReferences()
    {
        if (p1MainSlot == null)
            p1MainSlot = FindCharacterImage("character1");
        if (p1SubSlot == null)
            p1SubSlot = FindCharacterImage("character2");
        if (p2MainSlot == null)
            p2MainSlot = FindCharacterImage("EnemyCharacter1");
        if (p2SubSlot == null)
            p2SubSlot = FindCharacterImage("EnemyCharacter2");
    }

    private static Image FindCharacterImage(string objectName)
    {
        GameObject target = GameObject.Find(objectName);
        return target != null ? target.GetComponent<Image>() : null;
    }

    private void HandleGameStart(Player p1, Player p2)
    {
        _p1 = p1;
        _p2 = p2;
    }

    private void HandleFieldSync(IReadOnlyList<CharacterSlotSnapshot> snapshots)
    {
        if (snapshots == null)
            return;

        CachePlayersFromSnapshots(snapshots);

        foreach (CharacterSlotSnapshot snapshot in snapshots)
            ApplySnapshot(snapshot);
    }

    private void CachePlayersFromSnapshots(IReadOnlyList<CharacterSlotSnapshot> snapshots)
    {
        foreach (CharacterSlotSnapshot snapshot in snapshots)
        {
            if (snapshot.Owner == null)
                continue;

            if (snapshot.Slot != CharacterSlotType.Main)
                continue;

            // ★ RotationZ 를 그대로 보면 안 된다. 능력을 쓴 용병은 거기에 180이 더해져
            //   있어서, 아래 보드 주인이 180으로 보이고 위 보드 주인이 0으로 보인다.
            //   이미 능력을 쓴 상태에서 첫 동기화가 오면 위아래가 뒤바뎀다.
            //   더해진 180을 되돌려서 원래 방향만 남긴다.
            float baseZ = snapshot.AbilityUsed
                ? (snapshot.RotationZ + 180f) % 360f
                : snapshot.RotationZ;

            if (_p1 == null && baseZ == 0f)
                _p1 = snapshot.Owner;
            else if (_p2 == null && baseZ == 180f)
                _p2 = snapshot.Owner;
        }
    }

    private void HandleSlotUpdated(CharacterSlotSnapshot snapshot)
    {
        ApplySnapshot(snapshot);
    }

    private void ApplySnapshot(CharacterSlotSnapshot snapshot)
    {
        Image slot = ResolveSlot(snapshot.Owner, snapshot.Slot);
        if (slot == null)
            return;

        if (string.IsNullOrEmpty(snapshot.CharacterCardId))
        {
            slot.sprite = null;
            slot.material = null;   // 빈 칸에 흑백이 남지 않도록 되돌린다
            return;
        }

        Sprite sprite = LoadSprite(snapshot.ImageResourcesPath);
        if (sprite != null)
        {
            slot.sprite = sprite;
            slot.color = Color.white;
            slot.preserveAspect = true;
        }
        else
        {
            Debug.LogWarning(
                $"[CharacterFieldUI] 이미지 로드 실패: {snapshot.CharacterCardId} / path={snapshot.ImageResourcesPath}");
        }

        // ★ 회전은 <b>보드 방향</b>만 나타낸다. 사용 여부는 흑백으로 표시한다.
        //
        //   snapshot.RotationZ 를 쓰지 않는 이유:
        //   CharacterFieldBroadcast.GetRotationZ 는 두 가지를 한 값에 겹쳐 담는다 —
        //     baseZ = 아래 보드 0 / 위 보드 180   (원래 방향)
        //     사용했으면 여기에 180을 더한다      (사용 표시)
        //   그래서 그 값을 그대로 쓰면 "사용함"이 회전으로 나온다.
        //   화면 표현은 UI의 몫이므로 여기서 방향만 다시 계산한다(엔진은 건드리지 않는다).
        bool isBottom = ReferenceEquals(slot, p1MainSlot) || ReferenceEquals(slot, p1SubSlot);
        slot.rectTransform.localRotation = Quaternion.Euler(0f, 0f, isBottom ? 0f : 180f);

        // 능력을 쓴 카드만 채도를 없앤다. 안 쓴 카드는 기본 머티리얼(null)로 되돌린다.
        slot.material = snapshot.AbilityUsed ? ResolveUsedMaterial() : null;
    }

    private Image ResolveSlot(Player owner, CharacterSlotType slot)
    {
        if (owner == null)
            return null;

        bool isP1 = _p1 != null ? owner == _p1 : owner != _p2;

        if (isP1)
            return slot == CharacterSlotType.Main ? p1MainSlot : p1SubSlot;

        return slot == CharacterSlotType.Main ? p2MainSlot : p2SubSlot;
    }

    /// <summary>흑백 머티리얼을 확보한다. 인스펙터 연결이 없으면 Resources에서 찾는다.</summary>
    private Material ResolveUsedMaterial()
    {
        if (usedMaterial != null || _usedMaterialResolved) return usedMaterial;

        _usedMaterialResolved = true;
        usedMaterial = Resources.Load<Material>("Materials/UIGrayscale");

        if (usedMaterial == null)
        {
            Debug.LogWarning(
                "[CharacterFieldUI] 흑백 머티리얼을 찾지 못했습니다: Resources/Materials/UIGrayscale. " +
                "능력을 쓴 용병이 원래 색 그대로 보입니다.");
        }

        return usedMaterial;
    }

    private static Sprite LoadSprite(string resourcesPath)
    {
        if (string.IsNullOrEmpty(resourcesPath))
            return null;

        Sprite sprite = Resources.Load<Sprite>(resourcesPath);
        if (sprite != null)
            return sprite;

        Sprite[] all = Resources.LoadAll<Sprite>(resourcesPath);
        return all.Length > 0 ? all[0] : null;
    }
}
