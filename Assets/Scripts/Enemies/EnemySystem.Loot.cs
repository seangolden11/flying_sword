using System.Collections.Generic;
using UnityEngine;

public sealed partial class EnemySystem
{
    readonly List<EnemyState> bossLoot = new();

    void DropBossLoot(EnemyState owner)
    {
        EnemyBullet sword = owner.BossSword;
        if (sword == null) return;
        for (int i = 1; i < owner.BasicSwords.Length; i++)
            ReleaseBossSword(owner.BasicSwords[i]);
        owner.BasicSwords = new[] { sword };
        bullets.Remove(sword);
        bossLoot.Add(owner);
        if (sword.RecallCircle != null) Destroy(sword.RecallCircle);
        if (sword.State == BossSwordState.Stolen) return;
        sword.Root.position = owner.Position + Vector2.up*.6f;
        sword.State = BossSwordState.Falling;
        sword.View.SetBladeInactive(true);
        sword.Velocity = Vector2.up*2;
        sword.View.gameObject.SetActive(true);
        sword.View.ResetTrail(false);
    }

    void TickBossLoot(float dt)
    {
        foreach (EnemyState owner in bossLoot)
        {
            EnemyBullet sword = owner.BossSword;
            if (sword == null || sword.State != BossSwordState.Falling) continue;
            Vector2 position = sword.Root.position;
            Vector2 next = position+sword.Velocity*dt+Vector2.down*(9*dt*dt);
            sword.Velocity += Vector2.down*(18*dt);
            bool landed = geometry.ClipTerrain(position,next,Vector2.zero,out next);
            sword.Root.position = next;
            sword.Root.rotation = Quaternion.identity;
            if (landed) sword.State = BossSwordState.Planted;
        }
    }

    void FindBossLoot(Vector2 cursor, ref float nearest, ref EnemyState owner, ref Vector2 position)
    {
        foreach (EnemyState candidate in bossLoot)
        {
            EnemyBullet sword = candidate.BossSword;
            if (sword == null || (sword.State != BossSwordState.Falling && sword.State != BossSwordState.Planted)) continue;
            float distance = ((Vector2)sword.Root.position-cursor).sqrMagnitude;
            if (distance > nearest) continue;
            nearest = distance;
            owner = candidate;
            position = sword.Root.position;
        }
    }

    public EnemyState BossLootAt(Vector2 cursor, float radius)
    {
        float nearest = radius*radius;
        EnemyState owner = null;
        Vector2 position = Vector2.zero;
        FindBossLoot(cursor,ref nearest,ref owner,ref position);
        return owner;
    }

    public bool ClaimBossLoot(EnemyState owner)
    {
        EnemyBullet sword = owner?.BossSword;
        if (sword == null || owner.Health > 0 ||
            (sword.State != BossSwordState.Falling && sword.State != BossSwordState.Planted)) return false;
        sword.State = BossSwordState.Stolen;
        sword.View.ResetTrail(false);
        sword.View.gameObject.SetActive(false);
        return true;
    }

    void ReturnUnclaimedLoot(EnemyState owner, Vector2 position)
    {
        EnemyBullet sword = owner.BossSword;
        if (sword.State != BossSwordState.Stolen) return;
        if (!bossLoot.Contains(owner)) bossLoot.Add(owner);
        sword.State = BossSwordState.Falling;
        sword.View.SetBladeInactive(true);
        sword.Root.position = position;
        sword.Velocity = Vector2.zero;
        sword.View.gameObject.SetActive(true);
        sword.View.ResetTrail(false);
    }

    void ResetBossLoot()
    {
        foreach (EnemyState owner in bossLoot) ReleaseBossSword(owner);
        bossLoot.Clear();
    }
}
