using UnityEngine;

public sealed partial class SwordSystem
{
    static void AcquireBossSword() => StoryProgress.AcquireBossSword();

    void ApplyOwnedSwordColors(SwordInstance sword)
    {
        Color color = sword.IsEnemyWeapon && !sword.PermanentLoot
            ? new Color32(181, 111, 255, 255) : SwordKindColor(sword.Kind);
        sword.CapturedBladeColor = color;
        sword.Blade.color = color;
        sword.Trail.startColor = new Color(color.r,color.g,color.b,.65f);
        sword.Trail.endColor = new Color(color.r,color.g,color.b,0);
    }

    void AddStoryBossSword()
    {
        if (!StoryProgress.HasBossSword) return;
        var source = Resources.Load<BulletView>("Enemies/HomingSword");
        if (source == null) return;
        SwordInstance sword = CreateSword("Boss reward sword");
        sword.PermanentLoot = true;
        CopyCapturedAppearance(sword,source);
        ApplyOwnedSwordColors(sword);
        sword.State = SwordState.Orbiting;
        swords.Add(sword);
        orbiting.Add(sword);
    }
}
