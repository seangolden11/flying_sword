using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class TurnActors
{
    public enum Skill { Fusion, AutoIntercept, CrossSlash, Guard, CursorFollow }
    public enum RewardEffect { AddSword, DeleteWeapon, CopyWeapon, AttackUp, FasterRecall, HomingUp,
        AddShard, AddTrash, AddTable, AddMonitor, AddFencing, AddWasher,
        UnlockFusion, UnlockIntercept, UnlockGuard, CounterPassive, HealPassive }
    [Serializable]
    public sealed class RewardOption
    {
        public string id = "sword_plus_one";
        public string name = "검 +1";
        public string description = "공전 대열에 검을 하나 추가합니다.";
        public RewardEffect effect = RewardEffect.AddSword;
        public Sprite icon;
    }
    [Header("Level rewards")]
    [SerializeField, Min(1)] int rewardChoiceCount = 3;
    readonly List<RewardOption> offeredRewards = new();
    readonly bool[] unlockedSkills = new bool[5];
    int pendingRewards, recallUpgrades, homingUpgrades;
    public int HomingUpgradeLevel => homingUpgrades;
    int rewardSelectionId, targetRewardIndex = -1;
    float timeScaleBeforeReward = 1;
    bool ownsRewardPause, suppressCombatUntilMouseReleased, rerollUsed;
    public bool IsChoosingReward => pendingRewards > 0;
    public bool IsChoosingWeapon => targetRewardIndex >= 0;
    public int RewardSelectionId => rewardSelectionId;
    public int PendingRewardCount => pendingRewards;
    public int RewardOptionCount => offeredRewards.Count;
    public string TargetRewardName => IsChoosingWeapon ? offeredRewards[targetRewardIndex].name : "";
    public bool CanReroll => IsChoosingReward && !IsChoosingWeapon && !rerollUsed;
    public float AutomaticRecallDelay => Mathf.Max(1, AutoRecallDelay - recallUpgrades);
    public RewardOption GetRewardOption(int index) => offeredRewards[index];
    public bool IsSkillUnlocked(Skill skill) => unlockedSkills[(int)skill];
    public void UnlockSkill(Skill skill) => unlockedSkills[(int)skill] = true;

    void ResetRewards()
    {
        RestoreRewardTime();
        pendingRewards = 0;
        rewardSelectionId++;
        recallUpgrades = 0;
        homingUpgrades = 0;
        targetRewardIndex = -1;
        offeredRewards.Clear();
        rerollUsed = false;
        Array.Clear(unlockedSkills, 0, unlockedSkills.Length);
        suppressCombatUntilMouseReleased = false;
    }
    static RewardOption Reward(RewardEffect effect, string name, string description) =>
        new RewardOption { id = effect.ToString(), effect = effect, name = name, description = description };
    static RewardEffect WeaponReward(WeaponKind kind) => (RewardEffect)((int)RewardEffect.AddShard + (int)kind - 1);
    static bool TryWeaponKind(RewardEffect effect, out WeaponKind kind)
    {
        kind = (WeaponKind)((int)effect - (int)RewardEffect.AddShard + 1);
        return effect >= RewardEffect.AddShard && effect <= RewardEffect.AddWasher;
    }
    static string WeaponDescription(WeaponKind kind) => kind switch {
        WeaponKind.Shard => "명중 시 독립 공격 분신 2개를 소환합니다. 원본 회수 시 사라집니다.",
        WeaponKind.Trash => "다른 적에게 한 번 더 호밍 공격합니다.",
        WeaponKind.Table => "느리게 날아가 강하게 넉백시킵니다.",
        WeaponKind.Monitor => "명중 위치에서 전기 광역 폭발을 일으킵니다.",
        WeaponKind.Fencing => "전방의 여러 적을 관통합니다.",
        _ => "설치 순간 주변 적만 강하게 밀어냅니다. 추가 피해는 없습니다."
    };
    static Sprite RewardIcon(RewardEffect effect)
    {
        if (TryWeaponKind(effect, out WeaponKind kind)) return GameArt.Weapon(kind);
        return GameArt.Get(effect switch {
            RewardEffect.AddSword => "add", RewardEffect.DeleteWeapon => "delete", RewardEffect.CopyWeapon => "copy",
            RewardEffect.AttackUp => "attack", RewardEffect.FasterRecall => "recall", RewardEffect.HomingUp => "homing",
            RewardEffect.UnlockFusion => "fusion", RewardEffect.UnlockIntercept => "intercept", RewardEffect.UnlockGuard => "guard",
            RewardEffect.CounterPassive => "counter", _ => "heal"
        });
    }
    bool HasEligibleWeapon(RewardEffect effect)
    {
        foreach (Sword sword in InventoryWeapons())
            if (IsEligibleWeapon(sword, effect)) return true;
        return false;
    }
    bool IsEligibleWeapon(Sword sword, RewardEffect effect) =>
        effect != RewardEffect.DeleteWeapon || OwnedSwordCount > 1;
    List<RewardOption> BuildRewardPool()
    {
        var pool = new List<RewardOption>
        {
            Reward(RewardEffect.AddSword, "검 +1", "기본 검 하나를 공전 대열에 추가합니다."),
            Reward(RewardEffect.CopyWeapon, "무기 복사", "무기 하나와 그 강화 상태를 복사합니다."),
            Reward(RewardEffect.AttackUp, "공격력 +1", "선택한 무기 하나의 공격력을 1 높입니다.")
        };
        if (HasEligibleWeapon(RewardEffect.DeleteWeapon))
            pool.Add(Reward(RewardEffect.DeleteWeapon, "무기 삭제", "선택한 무기 하나를 제거합니다. 마지막 무기는 유지됩니다."));
        if (recallUpgrades < 4)
            pool.Add(Reward(RewardEffect.FasterRecall, "자동 회수 -1초", "모든 무기의 설치 대기 시간을 1초 줄입니다. 최소 1초."));
        if (homingUpgrades < 3)
            pool.Add(Reward(RewardEffect.HomingUp, "호밍 강화", "모든 현재·미래 무기: 거리 +0.25, 각도 +5°, 회전 +10°/초. 최대 3단계."));
        foreach (WeaponKind kind in new[] { WeaponKind.Shard, WeaponKind.Trash, WeaponKind.Table, WeaponKind.Monitor, WeaponKind.Fencing, WeaponKind.Washer })
            pool.Add(Reward(WeaponReward(kind), WeaponName(kind), WeaponDescription(kind)));
        if (!IsSkillUnlocked(Skill.Fusion)) pool.Add(Reward(RewardEffect.UnlockFusion, "검 합체 해금", "1번을 길게 눌러 공전 무기의 공격력과 능력을 합칩니다."));
        if (!IsSkillUnlocked(Skill.AutoIntercept)) pool.Add(Reward(RewardEffect.UnlockIntercept, "자동 요격 해금", "2번으로 설치 무기의 탄환 요격을 켜거나 끕니다."));
        if (!IsSkillUnlocked(Skill.Guard)) pool.Add(Reward(RewardEffect.UnlockGuard, "검막 해금", "4번을 누르면 공전 무기로 적을 공격하고 탄환을 막습니다."));
        if (!hasCounter) pool.Add(Reward(RewardEffect.CounterPassive, "피격 반격", "접촉 피해를 받으면 그 적에게 피해 1과 넉백. 재사용 10초."));
        if (!hasKillHeal) pool.Add(Reward(RewardEffect.HealPassive, "처치 회복", "획득 이후 10킬마다 HP 1 회복. 최대 HP 10."));
        foreach (RewardOption item in pool) item.icon = RewardIcon(item.effect);
        return pool;
    }
    void RollRewards()
    {
        var pool = BuildRewardPool();
        offeredRewards.Clear();
        int count = Mathf.Min(Mathf.Max(1, rewardChoiceCount), pool.Count);
        for (int i = 0; i < count; i++)
        {
            int index = UnityEngine.Random.Range(0, pool.Count);
            offeredRewards.Add(pool[index]);
            pool.RemoveAt(index);
        }
        targetRewardIndex = -1;
        rewardSelectionId++;
    }
    public bool RerollRewards(int selectionId)
    {
        if (!CanReroll || selectionId != rewardSelectionId || IsGameOver) return false;
        rerollUsed = true;
        RollRewards();
        return true;
    }
    void QueueLevelReward()
    {
        pendingRewards++;
        if (pendingRewards == 1)
        {
            rerollUsed = false;
            RollRewards();
        }
        PauseRewardTime();
    }
    void OnEnable() { if (IsChoosingReward) PauseRewardTime(); }
    void PauseRewardTime()
    {
        if (ownsRewardPause) return;
        timeScaleBeforeReward = Time.timeScale;
        ownsRewardPause = true;
        Time.timeScale = 0;
    }
    static bool RequiresWeapon(RewardEffect effect) => effect == RewardEffect.DeleteWeapon ||
        effect == RewardEffect.CopyWeapon || effect == RewardEffect.AttackUp;
    public bool ChooseReward(int index, int selectionId)
    {
        if (!IsChoosingReward || IsChoosingWeapon || IsGameOver || selectionId != rewardSelectionId ||
            index < 0 || index >= offeredRewards.Count) return false;
        RewardEffect effect = offeredRewards[index].effect;
        if (RequiresWeapon(effect))
        {
            if (!HasEligibleWeapon(effect)) return false;
            targetRewardIndex = index;
            rewardSelectionId++;
            return true;
        }
        if (effect == RewardEffect.AddSword)
        {
            pendingSwords++;
            FlushPendingSwords();
        }
        else if (effect == RewardEffect.FasterRecall && recallUpgrades < 4)
        {
            recallUpgrades++;
            foreach (Sword sword in swords)
                if (sword.State == SwordState.Planted)
                    sword.AutoRecallRemaining = Mathf.Max(0, sword.AutoRecallRemaining - 1);
        }
        else if (effect == RewardEffect.HomingUp && homingUpgrades < 3) homingUpgrades++;
        else if (TryWeaponKind(effect, out WeaponKind kind)) AddWeapon(kind);
        else if (effect == RewardEffect.UnlockFusion && !IsSkillUnlocked(Skill.Fusion)) UnlockSkill(Skill.Fusion);
        else if (effect == RewardEffect.UnlockIntercept && !IsSkillUnlocked(Skill.AutoIntercept)) UnlockSkill(Skill.AutoIntercept);
        else if (effect == RewardEffect.UnlockGuard && !IsSkillUnlocked(Skill.Guard)) UnlockSkill(Skill.Guard);
        else if (effect == RewardEffect.CounterPassive && !hasCounter) { hasCounter = true; counterRemaining = 0; }
        else if (effect == RewardEffect.HealPassive && !hasKillHeal) { hasKillHeal = true; killsSinceHeal = 0; }
        else return false;
        FinishReward();
        return true;
    }
    public bool BackToRewards(int selectionId)
    {
        if (!IsChoosingReward || !IsChoosingWeapon || selectionId != rewardSelectionId) return false;
        targetRewardIndex = -1;
        rewardSelectionId++;
        return true;
    }
    public int WeaponOptionCount => InventoryWeapons().Count;
    public int GetWeaponId(int index) => InventoryWeapons()[index].Id;
    public Sprite GetWeaponIcon(int index) => GameArt.Weapon(InventoryWeapons()[index].Kind);
    public string GetWeaponDescription(int index)
    {
        Sword sword = InventoryWeapons()[index];
        string state = sword.State switch
        {
            SwordState.Orbiting => "공전", SwordState.Planted => "설치", SwordState.Returning => "회수",
            SwordState.Intercepting => "요격", SwordState.Rebounding => "튕김", _ => "비행"
        };
        Vector2 position = sword.Root.localPosition;
        if (!sword.Root.gameObject.activeSelf) state = "합체 구성품";
        return $"{WeaponName(sword.Kind)} #{sword.Id}\n공격력 {SwordDamage(sword)} · 공통 호밍 {homingUpgrades}/3\n{state} ({position.x:0.0}, {position.y:0.0})";
    }
    public bool CanChooseWeapon(int index) => IsChoosingWeapon && index >= 0 && index < WeaponOptionCount &&
        IsEligibleWeapon(InventoryWeapons()[index], offeredRewards[targetRewardIndex].effect);
    public bool ChooseWeapon(int weaponId, int selectionId)
    {
        if (!IsChoosingReward || !IsChoosingWeapon || IsGameOver || selectionId != rewardSelectionId) return false;
        Sword sword = InventoryWeapons().Find(candidate => candidate.Id == weaponId);
        RewardEffect effect = offeredRewards[targetRewardIndex].effect;
        if (sword == null || !IsEligibleWeapon(sword, effect)) return false;
        switch (effect)
        {
            case RewardEffect.DeleteWeapon:
                RemoveWeapon(sword);
                break;
            case RewardEffect.CopyWeapon:
                AddWeapon(sword.Kind, sword);
                break;
            case RewardEffect.AttackUp: sword.DamageBonus++; break;
            default: return false;
        }
        FinishReward();
        return true;
    }
    void RemoveWeapon(Sword sword)
    {
        var compounds = new List<Sword>(swords);
        if (mergeSword != null) compounds.Add(mergeSword);
        foreach (Sword parent in compounds)
        {
            if (!parent.Components.Remove(sword)) continue;
            if (parent.Components.Count == 0)
            {
                swords.Remove(parent); orbiting.Remove(parent);
                pendingSplits.Remove(parent);
                if (mergeSword == parent) { mergeSword = null; merging = false; }
                DestroyWeaponObjects(parent);
            }
            else RefreshFusion(parent);
            break;
        }
        swords.Remove(sword);
        orbiting.Remove(sword);
        pendingSplits.Remove(sword);
        DestroyWeaponObjects(sword);
    }
    void FinishReward()
    {
        CheckSynergies();
        pendingRewards--;
        targetRewardIndex = -1;
        rewardSelectionId++;
        suppressCombatUntilMouseReleased = true;
        if (IsChoosingReward)
        {
            rerollUsed = false;
            RollRewards();
        }
        else
        {
            offeredRewards.Clear();
            RestoreRewardTime();
        }
    }
    void RestoreRewardTime()
    {
        if (!ownsRewardPause) return;
        Time.timeScale = timeScaleBeforeReward;
        ownsRewardPause = false;
    }
}
