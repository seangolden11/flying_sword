using UnityEngine;

// Equipment belongs to the current story and its checkpoint, not a global unlock list.
public static class StoryProgress
{
    public static bool HasBossSword { get; private set; }
    public static bool IsBossChallenge { get; private set; }
    static bool checkpointHasBossSword;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void StartNewStory()
    {
        IsBossChallenge = false;
        HasBossSword = false;
        checkpointHasBossSword = false;
    }

    public static void StartBossChallenge()
    {
        StartNewStory();
        IsBossChallenge = true;
    }

    public static void AcquireBossSword() => HasBossSword = true;
    public static void RecordStageEntry() => checkpointHasBossSword = HasBossSword;
    public static void RestoreStageEntry() => HasBossSword = checkpointHasBossSword;
}
