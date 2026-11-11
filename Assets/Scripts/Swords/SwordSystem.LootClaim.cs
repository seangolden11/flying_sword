using UnityEngine;

public sealed partial class SwordSystem
{
    const float LootClaimDuration = 2;
    EnemyState claimingLootOwner;
    float lootClaimTime;
    int lootClaimFrame = -10;
    SpriteRenderer lootClaimBlade;
    Color lootClaimColor;

    bool UpdateLootClaim(Vector2 cursor, bool pointerAvailable, float dt)
    {
        if (claimingLootOwner != null && Time.frameCount-lootClaimFrame > 1) CancelLootClaim();
        if (!pointerAvailable)
        {
            CancelLootClaim();
            return false;
        }
        if (claimingLootOwner == null)
        {
            claimingLootOwner = enemySystem.BossLootAt(transform.TransformPoint(cursor), RecallRadius);
            if (claimingLootOwner == null) return false;
            lootClaimTime = 0;
            Transform blade = claimingLootOwner.BossSword.Root.Find("Visual/Blade");
            lootClaimBlade = blade != null ? blade.GetComponent<SpriteRenderer>() : null;
            if (lootClaimBlade != null) lootClaimColor = lootClaimBlade.color;
        }
        EnemyBullet source = claimingLootOwner.BossSword;
        if (source == null || (source.State != BossSwordState.Falling && source.State != BossSwordState.Planted))
        {
            CancelLootClaim();
            return false;
        }
        lootClaimFrame = Time.frameCount;
        lootClaimTime += dt;
        float progress = Mathf.Clamp01(lootClaimTime/LootClaimDuration);
        cameraFollow.FocusLoot(source.Root.position,progress);
        if (lootClaimBlade != null)
            lootClaimBlade.color = Color.Lerp(lootClaimColor,Color.white,progress*.6f);
        if (progress < 1) return true;
        EnemyState owner = claimingLootOwner;
        Vector2 position = source.Root.position;
        CancelLootClaim();
        if (!enemySystem.ClaimBossLoot(owner)) return true;
        SwordInstance sword = CreateSword("Boss reward sword");
        sword.BorrowedFrom = owner;
        sword.PermanentLoot = true;
        CopyCapturedAppearance(sword,source.View);
        ApplyOwnedSwordColors(sword);
        sword.Root.position = position;
        swords.Add(sword);
        AcquireBossSword();
        BeginRecall(sword);
        equipment.NotifyChanged();
        SpawnHitBurst(position,new Color(1,.8f,.4f));
        return true;
    }

    void CancelLootClaim()
    {
        if (lootClaimBlade != null) lootClaimBlade.color = lootClaimColor;
        claimingLootOwner = null;
        lootClaimTime = 0;
        lootClaimBlade = null;
    }
}
