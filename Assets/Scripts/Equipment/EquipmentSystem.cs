using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class EquipmentSystem : MonoBehaviour
{
    [SerializeField] SwordDefinition[] definitions;
    readonly List<SwordInstance> inventory = new();
    SwordSystem swordSystem;

    public event Action Changed;
    public IReadOnlyList<SwordInstance> Inventory => inventory;
    internal List<SwordInstance> Instances => inventory;
    public IReadOnlyList<SwordDefinition> Definitions => definitions;
    public float SpeedMultiplier { get; private set; } = 1;
    public float RecallMultiplier { get; private set; } = 1;
    public int EquippedCount { get; private set; }

    public void Initialize(SwordSystem system) => swordSystem = system;

    public SwordDefinition Definition(SwordKind kind)
    {
        foreach (SwordDefinition definition in definitions)
            if (definition.kind == kind)
                return definition;
        throw new InvalidOperationException($"Missing sword definition for {kind}.");
    }

    public void SetEquipped(SwordInstance sword, bool equipped)
    {
        swordSystem.SetSwordEquipped(sword, equipped);
    }

    public void SetAllEquipped(bool equipped)
    {
        foreach (SwordInstance sword in inventory)
            swordSystem.SetSwordEquipped(sword, equipped, false);
        NotifyChanged();
    }

    public void NotifyChanged()
    {
        float speed = 0;
        float recall = 0;
        int count = 0;
        foreach (SwordInstance sword in inventory)
        {
            if (sword.State != SwordState.Stored)
                AddBonuses(sword, ref speed, ref recall, ref count);
        }
        if (swordSystem != null && swordSystem.MergingSword != null && swordSystem.MergingSword.FusionSize > 0)
            AddBonuses(swordSystem.MergingSword, ref speed, ref recall, ref count);
        SpeedMultiplier = 1 + speed;
        RecallMultiplier = 1 + recall;
        EquippedCount = count;
        Changed?.Invoke();
    }

    void AddBonuses(SwordInstance sword, ref float speed, ref float recall, ref int count)
    {
        if (sword.FusionSize > 0)
        {
            foreach (SwordKind kind in sword.FusedKinds)
                AddDefinition(Definition(kind), ref speed, ref recall, ref count);
        }
        else
        {
            AddDefinition(Definition(sword.Kind), ref speed, ref recall, ref count);
        }
    }

    static void AddDefinition(SwordDefinition definition, ref float speed, ref float recall, ref int count)
    {
        speed += definition.speedBonus;
        recall += definition.recallBonus;
        count++;
    }
}
