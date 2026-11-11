using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class InventoryPanel : MonoBehaviour
{
    [SerializeField] InventorySection equippedSection;
    [SerializeField] InventorySection storageSection;
    [SerializeField] SwordSlot slotPrefab;
    [SerializeField] Transform dragLayer;
    [SerializeField] Text summary;
    readonly List<SwordSlot> slots = new();
    EquipmentSystem equipment;

    public Transform DragLayer => dragLayer;

    public void Initialize(EquipmentSystem inventory, SwordSystem swordSystem)
    {
        equipment = inventory;
        equippedSection.Initialize(this, true);
        storageSection.Initialize(this, false);
    }

    void OnEnable()
    {
        if (equipment != null)
            equipment.Changed += Refresh;
    }

    void OnDisable()
    {
        if (equipment != null)
            equipment.Changed -= Refresh;
    }

    public void Refresh()
    {
        if (equipment == null)
            return;
        IReadOnlyList<SwordInstance> inventory = equipment.Inventory;
        for (int i = 0; i < inventory.Count; i++)
        {
            if (i >= slots.Count)
            {
                SwordSlot slot = Instantiate(slotPrefab, equippedSection.Content);
                slots.Add(slot);
            }
            SwordInstance sword = inventory[i];
            bool equipped = sword.State != SwordState.Stored;
            SwordSlot card = slots[i];
            card.gameObject.SetActive(true);
            card.transform.SetParent(equipped ? equippedSection.Content : storageSection.Content, false);
            card.Bind(this, sword, equipment.Definition(sword.Kind), i + 1);
            card.transform.SetAsLastSibling();
        }
        for (int i = inventory.Count; i < slots.Count; i++)
            slots[i].gameObject.SetActive(false);
        equippedSection.RefreshCount(equipment.EquippedCount);
        storageSection.RefreshCount(inventory.Count - equipment.EquippedCount);
        summary.text = $"보유 {inventory.Count}  /  장착 {equipment.EquippedCount}     ·     검 속도 ×{equipment.SpeedMultiplier:0.00}" +
            $"     ·     회수 속도 ×{equipment.SpeedMultiplier * equipment.RecallMultiplier:0.00}";
    }

    public void Toggle(SwordInstance sword) => equipment.SetEquipped(sword, sword.State == SwordState.Stored);
    public void Move(SwordInstance sword, bool equipped) => equipment.SetEquipped(sword, equipped);
    public void EquipAll() => equipment.SetAllEquipped(true);
    public void StoreAll() => equipment.SetAllEquipped(false);
}
