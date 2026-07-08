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

            if (_p1 == null && snapshot.Slot == CharacterSlotType.Main && snapshot.RotationZ == 0f)
                _p1 = snapshot.Owner;
            else if (_p2 == null && snapshot.Slot == CharacterSlotType.Main && snapshot.RotationZ == 180f)
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

        slot.rectTransform.localRotation = Quaternion.Euler(0f, 0f, snapshot.RotationZ);
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
