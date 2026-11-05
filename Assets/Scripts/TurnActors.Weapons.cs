using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class TurnActors
{
    public enum WeaponKind { Sword, Shard, Trash, Table, Monitor, Fencing, Washer, Workshop, CrystalRapier, TemporaryFusion }
    [Flags] enum Ability { None = 0, Fragments = 1, Chain = 2, Heavy = 4, Electric = 8, Pierce = 16, KnockbackBurst = 32 }

    [Serializable] public sealed class WeaponTuning
    {
        public WeaponKind kind;
        [Min(1)] public int damage;
        [Min(.1f)] public float speed;
        [Min(0)] public float knockback;
        public WeaponTuning(WeaponKind kind, int damage, float speed, float knockback)
        { this.kind = kind; this.damage = damage; this.speed = speed; this.knockback = knockback; }
    }
    [Header("Weapon test tuning")]
    [SerializeField] WeaponTuning[] weaponTuning = {
        new(WeaponKind.Sword, 1, 16, 6), new(WeaponKind.Shard, 1, 16, 6),
        new(WeaponKind.Trash, 1, 13, 6), new(WeaponKind.Table, 2, 8, 14),
        new(WeaponKind.Monitor, 1, 13, 6), new(WeaponKind.Fencing, 1, 18, 6),
        new(WeaponKind.Washer, 1, 11, 6), new(WeaponKind.Workshop, 3, 10, 14),
        new(WeaponKind.CrystalRapier, 2, 18, 6)
    };
    [SerializeField, Min(.1f)] float chainRange = 4f;
    [SerializeField, Min(.1f)] float chainLifetime = 1.2f;
    [SerializeField, Min(1)] int fragmentDamage = 1;
    [SerializeField, Min(.1f)] float fragmentRange = 3.5f;
    [SerializeField, Min(.1f)] float fragmentSpeed = 12f;
    [SerializeField, Min(.05f)] float fragmentAttackInterval = .6f;
    [SerializeField, Min(.1f)] float fragmentVisualSize = .75f;
    [SerializeField, Min(.1f)] float electricRadius = 1.7f;
    [SerializeField, Min(1)] int electricDamage = 1;
    [UnityEngine.Serialization.FormerlySerializedAs("vortexRadius")]
    [SerializeField, Min(.1f)] float washerKnockbackRadius = 2.8f;
    [SerializeField, Min(0)] float washerKnockbackSpeed = 20f;
    [SerializeField, Min(.01f)] float washerKnockbackDuration = .35f;
    [SerializeField, Min(.05f)] float guardHitInterval = .5f;
    readonly List<Sword> pendingSplits = new();
    bool checkingSynergies;

    WeaponTuning Tuning(WeaponKind kind) => Array.Find(weaponTuning, item => item.kind == kind) ?? weaponTuning[0];
    int SwordDamage(Sword sword)
    {
        if (sword.Components.Count == 0) return Mathf.Max(1, Tuning(sword.Kind).damage + sword.DamageBonus);
        int total = 0;
        foreach (Sword component in sword.Components) total += SwordDamage(component);
        return total;
    }
    float WeaponSpeed(Sword sword)
    {
        bool combined = sword.Components.Count > 0 || sword.Kind == WeaponKind.Workshop || sword.Kind == WeaponKind.CrystalRapier;
        return BaseWeaponSpeed(sword) * (combined ? 2 : 1);
    }
    float BaseWeaponSpeed(Sword sword)
    {
        float speed = Mathf.Max(.1f, Tuning(sword.Kind).speed);
        foreach (Sword component in sword.Components) speed = Mathf.Min(speed, BaseWeaponSpeed(component));
        return speed;
    }
    float WeaponKnockback(Sword sword)
    {
        float strength = Mathf.Max(0, Tuning(sword.Kind).knockback);
        foreach (Sword component in sword.Components) strength = Mathf.Max(strength, WeaponKnockback(component));
        return strength;
    }
    static Ability Abilities(Sword sword)
    {
        Ability value = sword.Kind switch {
            WeaponKind.Shard => Ability.Fragments, WeaponKind.Trash => Ability.Chain,
            WeaponKind.Table => Ability.Heavy, WeaponKind.Monitor => Ability.Electric,
            WeaponKind.Fencing => Ability.Pierce, WeaponKind.Washer => Ability.KnockbackBurst,
            WeaponKind.Workshop => Ability.Chain | Ability.Heavy | Ability.Electric,
            WeaponKind.CrystalRapier => Ability.Pierce | Ability.Fragments, _ => Ability.None
        };
        foreach (Sword component in sword.Components) value |= Abilities(component);
        return value;
    }
    public static string WeaponName(WeaponKind kind) => kind switch {
        WeaponKind.Sword => "기본 검", WeaponKind.Shard => "조각검", WeaponKind.Trash => "쓰레기통",
        WeaponKind.Table => "테이블", WeaponKind.Monitor => "모니터", WeaponKind.Fencing => "펜싱칼",
        WeaponKind.Washer => "세탁기", WeaponKind.Workshop => "전격 작업대", WeaponKind.CrystalRapier => "파편 성검",
        _ => "일시 합체"
    };

    static Color ActiveWeaponColor(Sword sword) => sword.FusionSize > 0 || sword.Kind == WeaponKind.TemporaryFusion
        ? FusionBlade : GameArt.Weapon(sword.Kind) != null ? Color.white : ActiveBlade;

    void ApplyWeaponAppearance(Sword sword)
    {
        Sprite sprite = GameArt.Weapon(sword.Kind);
        if (sprite == null) return;
        sword.Blade.sprite = sprite;
        sword.Blade.color = ActiveWeaponColor(sword);
        float scale = GameArt.WorldScale(sprite, 1.5f);
        sword.Blade.transform.localScale = new Vector3(scale, scale, 1);
        sword.Blade.transform.localPosition = new Vector2(0, .25f);
        foreach (Transform child in sword.Visual)
            if (child.name == "Guard" || child.name == "Grip") child.gameObject.SetActive(false);
    }
    Sword AddWeapon(WeaponKind kind, Sword source = null)
    {
        Sword weapon = CreateSword(WeaponName(kind));
        weapon.Kind = kind;
        if (source != null)
        {
            weapon.DamageBonus = source.DamageBonus;
        }
        ApplyWeaponAppearance(weapon);
        weapon.Root.localPosition = Vector2.right * OrbitRadius;
        weapon.PreviousPosition = weapon.Root.localPosition;
        swords.Add(weapon);
        orbiting.Add(weapon);
        return weapon;
    }
    List<Sword> InventoryWeapons()
    {
        var inventory = new List<Sword>();
        foreach (Sword weapon in swords)
        {
            if (weapon.Components.Count > 0) inventory.AddRange(weapon.Components);
            else inventory.Add(weapon);
        }
        if (mergeSword != null) inventory.AddRange(mergeSword.Components);
        return inventory;
    }
    void DestroyWeaponObjects(Sword weapon)
    {
        foreach (Sword component in weapon.Components) DestroyWeaponObjects(component);
        weapon.Components.Clear();
        ClearOwnedEffects(weapon);
        if (weapon.InterceptTarget != null) weapon.InterceptTarget.Reserved = false;
        weapon.Root.gameObject.SetActive(false);
        Destroy(weapon.Root.gameObject);
    }
    void AbsorbIntoMerge(Sword weapon)
    {
        orbiting.Remove(weapon);
        swords.Remove(weapon);
        ClearOwnedEffects(weapon);
        if (weapon.Components.Count > 0)
        {
            mergeSword.Components.AddRange(weapon.Components);
            weapon.Components.Clear();
            DestroyWeaponObjects(weapon);
        }
        else
        {
            weapon.Root.gameObject.SetActive(false);
            mergeSword.Components.Add(weapon);
        }
        RefreshFusion(mergeSword);
    }
    static void RefreshFusion(Sword weapon)
    {
        weapon.FusionSize = weapon.Components.Count;
        SetFusionSize(weapon, weapon.State == SwordState.Orbiting);
    }
    void SplitReturnedWeapon(Sword compound)
    {
        Vector2 position = compound.Root.localPosition;
        swords.Remove(compound);
        orbiting.Remove(compound);
        foreach (Sword component in compound.Components)
        {
            component.Root.gameObject.SetActive(true);
            component.Root.localPosition = position;
            component.PreviousPosition = position;
            component.Visual.localPosition = Vector2.zero;
            component.State = SwordState.Orbiting;
            component.InterceptTarget = null;
            component.FlightHits.Clear();
            component.ReturnHits.Clear();
            component.GuardHits.Clear();
            component.Trail.Clear();
            component.Trail.emitting = false;
            component.Blade.color = ActiveWeaponColor(component);
            component.GroundMark.enabled = false;
            component.AutoRecallRemaining = 0;
            swords.Add(component);
            orbiting.Add(component);
        }
        compound.Components.Clear();
        DestroyWeaponObjects(compound);
    }
    Sword WeakestMaterial(WeaponKind kind)
    {
        Sword best = null;
        foreach (Sword weapon in swords)
        {
            if (weapon.Kind != kind || weapon.Components.Count > 0) continue;
            int score = weapon.DamageBonus;
            if (best == null || score < best.DamageBonus ||
                (score == best.DamageBonus && weapon.Id < best.Id)) best = weapon;
        }
        return best;
    }
    bool TrySynergy(WeaponKind a, WeaponKind b, WeaponKind c, WeaponKind result)
    {
        Sword first = WeakestMaterial(a), second = WeakestMaterial(b), third = WeakestMaterial(c);
        if (first == null || second == null || third == null) return false;
        RemoveWeapon(first); RemoveWeapon(second); RemoveWeapon(third);
        AddWeapon(result);
        SpawnWeaponEffect("synergy_fx", Vector2.zero, 2f, .5f);
        return true;
    }
    void CheckSynergies()
    {
        if (checkingSynergies) return;
        checkingSynergies = true;
        try
        {
            while (TrySynergy(WeaponKind.Trash, WeaponKind.Table, WeaponKind.Monitor, WeaponKind.Workshop) |
                   TrySynergy(WeaponKind.Sword, WeaponKind.Shard, WeaponKind.Fencing, WeaponKind.CrystalRapier)) { }
        }
        finally { checkingSynergies = false; }
    }
}
