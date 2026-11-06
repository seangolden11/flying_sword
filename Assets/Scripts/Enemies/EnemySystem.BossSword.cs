using UnityEngine;

public sealed partial class EnemySystem
{
    const float BossRecallDelay = 3;

    void CreateBossSword(EnemyState owner)
    {
        BulletView prefab = swordProjectilePrefab != null ? swordProjectilePrefab : bulletPrefab;
        BulletView swordView = swordProjectilePool.Count > 0 ? swordProjectilePool.Pop() : Instantiate(prefab, transform);
        swordView.ResetTrail(false);
        swordView.gameObject.SetActive(false);
        owner.BossSword = new EnemyBullet
        {
            View = swordView, Owner = owner, HomingSword = true, State = BossSwordState.Ready
        };
        SetBossSwordAway(owner, false);
    }

    void LaunchBossSword(EnemyState owner)
    {
        EnemyBullet sword = owner.BossSword;
        if (sword == null || sword.State != BossSwordState.Ready) return;
        sword.State = BossSwordState.Flying;
        sword.Previous = BossSwordHome(owner);
        sword.Root.position = sword.Previous;
        Vector2 direction = (player.Position - sword.Previous).normalized;
        if (direction.sqrMagnitude < .001f) direction = Vector2.left;
        sword.Velocity = direction * 7.5f;
        sword.Lifetime = 2.5f;
        sword.RecallTime = 0;
        sword.Root.rotation = Quaternion.Euler(0,0,Vector2.SignedAngle(Vector2.down,direction));
        sword.View.gameObject.SetActive(true);
        sword.View.ResetTrail(true);
        bullets.Add(sword);
        SetBossSwordAway(owner, true);
    }

    void DisableBossSword(EnemyBullet sword)
    {
        sword.State = BossSwordState.Falling;
        sword.RecallTime = BossRecallDelay;
        sword.Velocity = -sword.Velocity * .25f + Vector2.up * 2;
        sword.View.ResetTrail(false);
    }

    void TickBossSword(EnemyState owner, float dt)
    {
        EnemyBullet sword = owner.BossSword;
        if (sword == null || sword.State == BossSwordState.Ready || sword.State == BossSwordState.Flying || sword.State == BossSwordState.Stolen)
            return;
        if (sword.State == BossSwordState.Falling || sword.State == BossSwordState.Planted)
        {
            sword.RecallTime = Mathf.Max(0, sword.RecallTime - dt);
            if (sword.State == BossSwordState.Falling)
            {
                Vector2 position = sword.Root.position;
                Vector2 next = position + sword.Velocity * dt + Vector2.down * (9 * dt * dt);
                sword.Velocity += Vector2.down * (18 * dt);
                bool landed = geometry.ClipTerrain(position,next,Vector2.zero,out next);
                sword.Root.position = next;
                if (sword.Velocity.sqrMagnitude > .001f)
                    sword.Root.rotation = Quaternion.Euler(0,0,Vector2.SignedAngle(Vector2.down,sword.Velocity));
                if (landed) sword.State = BossSwordState.Planted;
            }
            if (sword.RecallTime > 0) return;
            sword.State = BossSwordState.Returning;
            sword.View.ResetTrail(true);
        }
        Vector2 target = owner.Position + Vector2.up * 2.4f;
        Vector2 travel = target - (Vector2)sword.Root.position;
        sword.Root.rotation = Quaternion.Euler(0,0,Vector2.SignedAngle(Vector2.down,travel));
        sword.Root.position = Vector2.MoveTowards(sword.Root.position,target,12 * dt);
        if (Vector2.Distance(sword.Root.position,target) > .05f) return;
        sword.State = BossSwordState.Ready;
        sword.View.ResetTrail(false);
        sword.View.gameObject.SetActive(false);
        owner.BasicTimer = 1.1f;
        SetBossSwordAway(owner, false);
    }

    public bool TryStealSword(Vector2 cursor, float radius, out EnemyState owner, out Vector2 position)
    {
        owner = null;
        position = Vector2.zero;
        float nearest = radius * radius;
        foreach (EnemyState enemy in enemies)
        {
            EnemyBullet sword = enemy.BossSword;
            if (sword == null || (sword.State != BossSwordState.Falling && sword.State != BossSwordState.Planted))
                continue;
            float distance = ((Vector2)sword.Root.position - cursor).sqrMagnitude;
            if (distance > nearest) continue;
            owner = enemy;
            nearest = distance;
            position = sword.Root.position;
        }
        if (owner == null) return false;
        owner.BossSword.State = BossSwordState.Stolen;
        owner.BossSword.View.ResetTrail(false);
        owner.BossSword.View.gameObject.SetActive(false);
        return true;
    }

    public void ReturnStolenSword(EnemyState owner, Vector2 position)
    {
        if (owner == null || owner.BossSword == null) return;
        if (owner.Health <= 0)
        {
            ReturnUnclaimedLoot(owner,position);
            return;
        }
        EnemyBullet sword = owner.BossSword;
        if (sword.State != BossSwordState.Stolen) return;
        sword.State = BossSwordState.Returning;
        sword.Root.position = position;
        sword.View.gameObject.SetActive(true);
        sword.View.ResetTrail(true);
    }

    static Vector2 BossSwordHome(EnemyState owner)
    {
        var visual = owner.View.GetComponent<SwordBossVisual>();
        return visual != null && visual.blades.Length > 0
            ? (Vector2)visual.blades[0].position : owner.Position + Vector2.up * 2.4f;
    }

    static void SetBossSwordAway(EnemyState owner, bool away)
    {
        var visual = owner.View.GetComponent<SwordBossVisual>();
        if (visual != null) visual.SetSwordAway(away);
    }

    void ReleaseBossSword(EnemyState owner)
    {
        EnemyBullet sword = owner.BossSword;
        if (sword == null) return;
        bullets.Remove(sword);
        sword.View.ResetTrail(false);
        sword.View.gameObject.SetActive(false);
        swordProjectilePool.Push(sword.View);
        owner.BossSword = null;
    }
}
