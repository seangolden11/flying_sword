using UnityEngine;

public sealed partial class SwordSystem
{
    static void AcquireBossSword() => StoryProgress.AcquireBossSword();

    void ApplyOwnedSwordColors(SwordInstance sword)
    {
        Color color = SwordKindColor(sword.Kind);
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
        sword.Root.localPosition = RestPosition(orbiting.Count,orbiting.Count+1);
        PointSword(sword,RestDirection(orbiting.Count,orbiting.Count+1));
        swords.Add(sword);
        orbiting.Add(sword);
    }
}
