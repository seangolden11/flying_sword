using UnityEngine;

public sealed partial class EnemySystem
{
    const float BossRecallDelay = 3;
    const float BossSwordLifetime = 3.5f;
    const float BossSwordSpeed = 7.5f;
    const int BossChallengeSwordCount = 7;

    void TickBossBasicAttack(EnemyState owner, float dt)
    {
        owner.BasicTimer -= dt;
        if (owner.BasicTimer > 0)
            return;

        foreach (EnemyBullet sword in owner.BasicSwords)
        {
            if (sword.State != BossSwordState.Ready)
                continue;

            LaunchBossSword(sword);
            owner.BasicTimer = burstShotInterval;
            return;
        }
    }

    void CreateBossSword(EnemyState owner)
    {
        BulletView prefab = swordProjectilePrefab != null ? swordProjectilePrefab : bulletPrefab;
        owner.BasicSwords = new EnemyBullet[!owner.IsPractice && StoryProgress.IsBossChallenge ? BossChallengeSwordCount : 1];
        for (int i = 0; i < owner.BasicSwords.Length; i++)
        {
            BulletView view = swordProjectilePool.Count > 0 ? swordProjectilePool.Pop() : Instantiate(prefab, transform);
            view.transform.localScale = prefab.transform.localScale;
            view.SetBladeInactive(false);
            view.ResetTrail(false);
            var sword = new EnemyBullet
            {
                View = view, Owner = owner, HomingSword = true, State = BossSwordState.Ready, BossSlot = i,
                IsPracticeShot = owner.IsPractice
            };
            owner.BasicSwords[i] = sword;
            SetReadyBossSwordPose(sword);
            view.gameObject.SetActive(true);
        }
    }

    void LaunchBossSword(EnemyBullet sword)
    {
        sword.State = BossSwordState.Flying;
        sword.Previous = sword.Root.position;
        EnemyState owner = sword.Owner;
        int directions = Mathf.Max(4, owner.BasicSwords.Length);
        float duration = Mathf.Clamp(Vector2.Distance(owner.Position, player.Position) / BossSwordSpeed, .32f, 1.65f);
        sword.Convergence = SwordConvergence.Begin(sword.Previous, player.Position, owner.BasicCurveSlot, directions, duration);
        owner.BasicCurveSlot = (owner.BasicCurveSlot + 1) % directions;
        Vector2 direction = (player.Position - sword.Previous).normalized;
        if (direction.sqrMagnitude < .001f) direction = Vector2.left;
        sword.Velocity = direction * BossSwordSpeed;
        sword.Lifetime = BossSwordLifetime;
        sword.RecallTime = 0;
        sword.Root.rotation = Quaternion.Euler(0,0,Vector2.SignedAngle(Vector2.down,direction));
        sword.View.gameObject.SetActive(true);
        sword.View.ResetTrail(true);
        bullets.Add(sword);
    }

    void DisableBossSword(EnemyBullet sword, bool expired)
    {
        sword.State = BossSwordState.Falling;
        sword.RecallTime = BossRecallDelay;
        sword.View.SetBladeInactive(true);
        sword.Velocity = expired ? Vector2.zero : -sword.Velocity * .25f + Vector2.up * 2;
        sword.View.ResetTrail(false);
    }

    void TickBossSword(EnemyState owner, float dt)
    {
        if (owner.BasicSwords == null) return;
        foreach (EnemyBullet sword in owner.BasicSwords)
            TickBossSword(sword, dt);
    }

    void TickBossSword(EnemyBullet sword, float dt)
    {
        EnemyState owner = sword.Owner;
        if (sword.State == BossSwordState.Ready)
        {
            SetReadyBossSwordPose(sword, 1 - Mathf.Exp(-12 * dt));
            return;
        }
        if (sword.State == BossSwordState.Flying || sword.State == BossSwordState.Stolen)
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
            if (sword.RecallTime > 0 || (owner.IsPractice && owner.PracticeSwordIntercepted)) return;
            sword.State = BossSwordState.Returning;
            StartBossSwordRecall(sword);
        }
        if (!sword.RecallInFlight)
        {
            if (!AdvanceBossRecallCircle(sword.RecallCircle, sword.Root.position, dt)) return;
            if (sword.RecallCircle != null) sword.RecallCircle.SetActive(false);
            sword.RecallInFlight = true;
            sword.View.SetBladeInactive(false);
            sword.View.ResetTrail(true);
        }
        if (!AdvanceBossRecallFlight(sword.View, BasicSwordHome(sword), 12, dt)) return;
        sword.State = BossSwordState.Ready;
        sword.View.ResetTrail(false);
        if (owner.BasicSwords.Length == 1) owner.BasicTimer = 1.1f;
        SetReadyBossSwordPose(sword);
    }

    public bool TryStealSword(Vector2 cursor, float radius, out EnemyState owner, out BulletView captured, out bool giant, out EnemyBullet stolenBasicSword)
    {
        owner = null;
        captured = null;
        giant = false;
        stolenBasicSword = null;
        float nearest = radius * radius;
        EnemyBullet nearestBasicSword = null;
        foreach (EnemyState enemy in enemies)
        {
            if (enemy.BasicSwords == null) continue;
            foreach (EnemyBullet sword in enemy.BasicSwords)
            {
                if (sword.State != BossSwordState.Falling && sword.State != BossSwordState.Planted)
                    continue;

                float distance = ((Vector2)sword.Root.position - cursor).sqrMagnitude;
                if (distance > nearest) continue;
                nearest = distance;
                nearestBasicSword = sword;
            }
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
        if (nearestBasicSword == null) return false;
        stolenBasicSword = nearestBasicSword;
        owner = stolenBasicSword.Owner;
        captured = stolenBasicSword.View;
        stolenBasicSword.State = BossSwordState.Stolen;
        captured.ResetTrail(false);
        captured.gameObject.SetActive(false);
        return true;
    }

    public void ReturnStolenSword(EnemyBullet sword, Vector2 position)
    {
        if (sword == null || sword.State != BossSwordState.Stolen) return;
        EnemyState owner = sword.Owner;
        if (owner.Health <= 0)
        {
            if (sword == owner.BossSword) ReturnUnclaimedLoot(owner, position);
            return;
        }
        sword.Root.position = position;
        sword.View.gameObject.SetActive(true);
        StartBossSwordRecall(sword);
    }

    void StartBossSwordRecall(EnemyBullet sword)
    {
        sword.State = BossSwordState.Returning;
        sword.RecallInFlight = false;
        OpenBossRecallCircle(ref sword.RecallCircle, sword.Owner.Position);
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

    static Vector2 BossSwordHome(EnemyState owner) =>
        owner.SwordVisual != null ? owner.SwordVisual.RecallAnchor : owner.Position + Vector2.up * 2.4f;

    static Vector2 BasicSwordHome(EnemyBullet sword)
    {
        EnemyState owner = sword.Owner;
        return owner.SwordVisual != null
            ? owner.SwordVisual.SwordPosition(sword.BossSlot, owner.BasicSwords.Length)
            : BossSwordHome(owner);
    }

    static void SetReadyBossSwordPose(EnemyBullet sword, float blend = 1)
    {
        var visual = sword.Owner.SwordVisual;
        Quaternion rotation = visual != null ? visual.SwordRotation : Quaternion.identity;
        sword.Root.SetPositionAndRotation(Vector3.Lerp(sword.Root.position, BasicSwordHome(sword), blend), rotation);
    }

    void ReleaseBossSword(EnemyState owner)
    {
        if (owner.BasicSwords == null) return;
        foreach (EnemyBullet sword in owner.BasicSwords)
            ReleaseBossSword(sword);
        owner.BasicSwords = null;
    }

    void ReleaseBossSword(EnemyBullet sword)
    {
        bullets.Remove(sword);
        sword.View.ResetTrail(false);
        sword.View.gameObject.SetActive(false);
        if (sword.RecallCircle != null) Destroy(sword.RecallCircle);
        sword.View.SetBladeInactive(false);
        swordProjectilePool.Push(sword.View);
        sword.State = BossSwordState.Ready;
    }
}
