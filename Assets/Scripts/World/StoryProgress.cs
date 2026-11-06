using UnityEngine;

// Equipment belongs to the current story and its checkpoint, not a global unlock list.
public static class StoryProgress
{
    public static bool HasBossSword { get; private set; }
    static bool checkpointHasBossSword;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void StartNewStory()
    {
        HasBossSword = false;
        checkpointHasBossSword = false;
    }

    public static void AcquireBossSword() => HasBossSword = true;
    public static void RecordStageEntry() => checkpointHasBossSword = HasBossSword;
    public static void RestoreStageEntry() => HasBossSword = checkpointHasBossSword;
}
