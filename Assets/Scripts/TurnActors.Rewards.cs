using System;
using UnityEngine;

public sealed partial class TurnActors
{
    public enum Skill { Fusion, AutoIntercept, CrossSlash, Guard, CursorFollow }
    public enum RewardEffect { AddSword }

    [Serializable]
    public sealed class RewardOption
    {
        public string id = "sword_plus_one";
        public string name = "검 +1";
        public string description = "공전 대열에 검을 하나 추가합니다.";
        public RewardEffect effect = RewardEffect.AddSword;
        public Sprite icon;
    }

    [Header("Level rewards (one selected per level)")]
    [SerializeField] RewardOption[] rewardOptions = { new RewardOption(), new RewardOption(), new RewardOption() };
    readonly bool[] unlockedSkills = new bool[5];
    int pendingRewards, ownedSwordCount;
    int rewardSelectionId;
    float timeScaleBeforeReward = 1;
    bool ownsRewardPause, suppressCombatUntilMouseReleased;
    public bool IsChoosingReward => pendingRewards > 0;
    public int RewardSelectionId => rewardSelectionId;
    public int PendingRewardCount => pendingRewards;
    public int RewardOptionCount => rewardOptions.Length;
    public RewardOption GetRewardOption(int index) => rewardOptions[index];
    public bool IsSkillUnlocked(Skill skill) => unlockedSkills[(int)skill];
    public void UnlockSkill(Skill skill) => unlockedSkills[(int)skill] = true;

    void ResetRewards()
    {
        RestoreRewardTime();
        pendingRewards = 0;
        rewardSelectionId++;
        ownedSwordCount = InitialSwordCount;
        Array.Clear(unlockedSkills, 0, unlockedSkills.Length);
        suppressCombatUntilMouseReleased = false;
        // Keep a selectable fallback if an Inspector edit clears all options.
        if (rewardOptions == null || rewardOptions.Length == 0)
            rewardOptions = new[] { new RewardOption(), new RewardOption(), new RewardOption() };
        for (int i = 0; i < rewardOptions.Length; i++)
            rewardOptions[i] ??= new RewardOption();
    }

    void QueueLevelReward()
    {
        pendingRewards++;
        if (pendingRewards == 1) rewardSelectionId++;
        PauseRewardTime();
    }

    void OnEnable()
    {
        if (IsChoosingReward) PauseRewardTime();
    }

    void PauseRewardTime()
    {
        if (ownsRewardPause) return;
        timeScaleBeforeReward = Time.timeScale;
        ownsRewardPause = true;
        Time.timeScale = 0;
    }

    public bool ChooseReward(int index, int selectionId)
    {
        if (!IsChoosingReward || IsGameOver || selectionId != rewardSelectionId ||
            index < 0 || index >= rewardOptions.Length)
            return false;
        switch (rewardOptions[index].effect)
        {
            case RewardEffect.AddSword:
                pendingSwords++;
                ownedSwordCount++;
                FlushPendingSwords();
                break;
            default:
                return false;
        }
        pendingRewards--;
        rewardSelectionId++;
        suppressCombatUntilMouseReleased = true;
        if (!IsChoosingReward) RestoreRewardTime();
        return true;
    }

    void RestoreRewardTime()
    {
        if (!ownsRewardPause) return;
        Time.timeScale = timeScaleBeforeReward;
        ownsRewardPause = false;
    }
}
