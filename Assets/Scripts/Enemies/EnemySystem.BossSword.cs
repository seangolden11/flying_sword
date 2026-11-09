using UnityEngine;

public sealed partial class EnemySystem
{
    const float BossRecallDelay = 3;

    void CreateBossSword(EnemyState owner)
    {
        BulletView prefab = swordProjectilePrefab != null ? swordProjectilePrefab : bulletPrefab;
        BulletView swordView = swordProjectilePool.Count > 0 ? swordProjectilePool.Pop() : Instantiate(prefab, transform);
        swordView.SetBladeInactive(false);
        swordView.ResetTrail(false);
        swordView.gameObject.SetActive(false);
        owner.BossSword = new EnemyBullet
        {
            View = swordView, Owner = owner, HomingSword = true, State = BossSwordState.Ready
        };
        if (owner.SwordVisual != null) owner.SwordVisual.BindSword(swordView);
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
        sword.View.SetBladeInactive(true);
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
            StartBossSwordRecall(owner);
        }
        if (!owner.RecallInFlight)
        {
            if (!AdvanceBossRecallCircle(owner.RecallCircle, sword.Root.position, dt)) return;
            if (owner.RecallCircle != null) owner.RecallCircle.SetActive(false);
            owner.RecallInFlight = true;
            sword.View.SetBladeInactive(false);
            sword.View.ResetTrail(true);
        }
        if (!AdvanceBossRecallFlight(sword.View, BossSwordHome(owner), 12, dt)) return;
        sword.State = BossSwordState.Ready;
        sword.View.ResetTrail(false);
        owner.BasicTimer = 1.1f;
        SetBossSwordAway(owner, false);
    }

    public bool TryStealSword(Vector2 cursor, float radius, out EnemyState owner, out BulletView captured, out bool giant)
    {
        owner = null;
        captured = null;
        giant = false;
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
            captured = sword.View;
        }
        int portalIndex = -1;
        for (int i = 0; i < plantedPortalSwords.Count; i++)
        {
            BulletView sword = plantedPortalSwords[i];
            float distance = ((Vector2)sword.transform.position - cursor).sqrMagnitude;
            if (distance > nearest) continue;
            nearest = distance;
            portalIndex = i;
        }
        if (plantedGiantSword != null && !giantFalling &&
            PlantedGiantDistanceSquared(cursor) <= nearest)
        {
            owner = null;
            giantBlade.color = giantActiveColor;
            captured = capturedGiantSword = plantedGiantSword;
            plantedGiantSword = null;
            giant = true;
            return true;
        }
        if (portalIndex >= 0)
        {
            owner = null;
            captured = plantedPortalSwords[portalIndex];
            plantedPortalSwords.RemoveAt(portalIndex);
            return true;
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
        sword.Root.position = position;
        sword.View.gameObject.SetActive(true);
        StartBossSwordRecall(owner);
    }

    void StartBossSwordRecall(EnemyState owner)
    {
        owner.BossSword.State = BossSwordState.Returning;
        owner.RecallInFlight = false;
        OpenBossRecallCircle(ref owner.RecallCircle, owner.Position);
    }

    void OpenBossRecallCircle(ref GameObject circle, Vector2 origin)
    {
        if (circle == null && recallCirclePrefab != null)
            circle = Instantiate(recallCirclePrefab, transform);
        if (circle == null) return;
        circle.transform.position = origin;
        circle.SetActive(true);
    }

    static bool AdvanceBossRecallCircle(GameObject circle, Vector2 target, float dt)
    {
        if (circle == null) return true;
        Vector2 next = Vector2.MoveTowards(circle.transform.position, target, BossRecallCircleSpeed * dt);
        circle.transform.position = next;
        return next == target;
    }

    static bool AdvanceBossRecallFlight(BulletView sword, Vector2 target, float speed, float dt)
    {
        Vector2 from = sword.transform.position;
        Vector2 next = Vector2.MoveTowards(from, target, speed * dt);
        if (next != from)
            sword.transform.rotation = Quaternion.Euler(0, 0, Vector2.SignedAngle(Vector2.down, next - from));
        sword.transform.position = next;
        return next == target;
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
        if (owner.RecallCircle != null) Destroy(owner.RecallCircle);
        sword.View.SetBladeInactive(false);
        swordProjectilePool.Push(sword.View);
        owner.BossSword = null;
    }
}
