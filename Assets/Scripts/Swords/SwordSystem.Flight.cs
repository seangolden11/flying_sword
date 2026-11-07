using System.Collections.Generic;
using UnityEngine;

public sealed partial class SwordSystem
{
    void BeginFlight(SwordInstance sword, Vector2 target)
    {
        if (sword.IsEnemyWeapon)
        {
            sword.BorrowedUsed = (sword.BorrowedFrom != null || sword.CapturedGiantSword) && !sword.PermanentLoot;
            sword.Visual.localPosition = Vector3.zero;
            sword.Visual.localRotation = Quaternion.identity;
        }
        sword.State = SwordState.Flying;
        sword.LastGiantIntercept = null;
        sword.Blade.color = ReadyBladeColor(sword);
        sword.CurveEnemy = null;
        sword.Trail.Clear();
        sword.BlockRecoilTime = 0;
        sword.BlocksRemaining = sword.CapturedGiantSword ? 5 : Mathf.Max(1, sword.FusionSize);
        sword.ContactEnemy = null;
        sword.Trail.time = .20f;
        sword.Trail.startWidth = .065f;
        Color trailColor = SwordKindColor(sword.Kind);
        sword.Trail.startColor = new Color(trailColor.r,trailColor.g,trailColor.b,.65f);
        sword.Trail.endColor = new Color(trailColor.r,trailColor.g,trailColor.b,0);
        sword.Trail.emitting = true;
        sword.Target = target;
        SetFusionSize(sword, orbitVisual: false);
    }

    const float GiantDrawTime = .30f;
    const float GiantHoldTime = .18f;
    const float GiantDrawDistance = 1.25f;
    const float GiantInitialSpeed = 20;
    const float GiantLaunchSpeed = 64;
    const float GiantAccelerationTime = .10f;
    readonly HashSet<EnemyState> giantFlightHits = new();
    readonly LineRenderer[] giantWind = new LineRenderer[8];
    readonly Vector2[] giantWindOrigins = new Vector2[8];
    readonly float[] giantWindAges = new float[8];
    Vector2 giantWindDirection;
    float giantWindDistance;
    int giantWindIndex;

    void PrepareGiantLaunch(SwordInstance sword, Vector2 cursor)
    {
        PrepareLaunch(sword, cursor);
        sword.GiantLaunchOrigin = sword.Root.localPosition;
        Vector2 direction = cursor - sword.GiantLaunchOrigin;
        sword.GiantLaunchDirection = direction.sqrMagnitude > .001f
            ? direction.normalized : Vector2.right * formationFacing;
    }

    void AdvanceGiantPreparation(SwordInstance sword, float dt)
    {
        sword.LaunchPreparation += dt;
        float progress = Mathf.Clamp01(sword.LaunchPreparation / GiantDrawTime);
        Vector2 home = sword.GiantLaunchOrigin;
        Vector2 drawn = home - sword.GiantLaunchDirection * (GiantDrawDistance * Mathf.SmoothStep(0, 1, progress));
        stage.ClipTerrain(home, drawn, Vector2.zero, out drawn);
        sword.Root.localPosition = drawn;
        Quaternion angle = Quaternion.Euler(0, 0, Vector2.SignedAngle(Vector2.down, sword.GiantLaunchDirection));
        sword.Root.localRotation = Quaternion.Slerp(sword.Root.localRotation, angle, 1 - Mathf.Exp(-24 * dt));
        sword.Blade.color = Color.Lerp(ReadyBladeColor(sword), Color.white, progress * .35f);
        if (sword.LaunchPreparation < GiantDrawTime + GiantHoldTime) return;
        BeginFlight(sword, sword.Target);
        PointSword(sword, sword.GiantLaunchDirection);
        sword.LaunchPreparation = 0;
        sword.Trail.time = .14f;
        sword.Trail.startWidth = .48f;
        foreach (SpriteRenderer part in sword.Visual.GetComponentsInChildren<SpriteRenderer>())
        {
            Color color = part.color;
            color.a *= .16f;
            part.color = color;
        }
        StartGiantWind(sword);
    }

    void AdvanceGiantFlight(SwordInstance sword, float dt)
    {
        Vector2 position = sword.Root.localPosition;
        sword.LaunchPreparation += dt;
        float acceleration = Mathf.Clamp01((sword.LaunchPreparation - dt * .5f) / GiantAccelerationTime);
        float speed = Mathf.Lerp(GiantInitialSpeed, GiantLaunchSpeed, acceleration);
        Vector2 next = position + sword.GiantLaunchDirection * (speed * dt);
        bool terrainHit = stage.ClipTerrain(position, next, Vector2.zero, out next);
        sword.Root.localPosition = next;
        EmitGiantWind(position, next);
        HitEnemiesWithGiantSword(sword, position, next);
        while (!sword.PendingRemoval && TryHitBullet(sword, position, next))
        {
        }
        Vector2 edge = GridSize * .5f;
        if (terrainHit || Mathf.Abs(next.x) > edge.x || Mathf.Abs(next.y) > edge.y)
            ReturnBorrowedSword(sword);
    }

    void HitEnemiesWithGiantSword(SwordInstance sword, Vector2 from, Vector2 to)
    {
        var enemies = enemySystem.Enemies;
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            EnemyState enemy = enemies[i];
            float radius = EnemyHitRadius * Mathf.Abs(enemy.View.transform.lossyScale.x);
            if (DistanceToSegmentSquared(enemy.Position, from, to) > radius * radius ||
                !giantFlightHits.Add(enemy)) continue;
            enemySystem.Damage(enemy, SwordDamage(sword));
            if (enemy.Health == 0 && lockedEnemy == enemy) lockedEnemy = null;
            SpawnHitBurst(enemy.Position, new Color(.75f, 1, .94f));
        }
    }

    const float GiantWindSpacing = 3.2f;
    const float GiantWindLifetime = .28f;

    void StartGiantWind(SwordInstance sword)
    {
        ClearGiantWind();
        giantFlightHits.Clear();
        giantWindDirection = sword.GiantLaunchDirection;
        giantWindDistance = 0;
        for (int i = 0; i < giantWind.Length; i++)
        {
            if (giantWind[i] != null) continue;
            var arc = new GameObject("Giant Sword Pressure Arc").AddComponent<LineRenderer>();
            arc.transform.SetParent(transform, false);
            arc.sharedMaterial = sword.Trail.sharedMaterial;
            arc.sortingLayerID = sword.Trail.sortingLayerID;
            arc.sortingOrder = sword.Trail.sortingOrder - 1;
            arc.positionCount = 19;
            arc.widthCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(.5f, 1), new Keyframe(1, 0));
            arc.enabled = false;
            giantWind[i] = arc;
        }
    }

    void EmitGiantWind(Vector2 from, Vector2 to)
    {
        float length = Vector2.Distance(from, to);
        while (giantWindDistance < length)
        {
            int index = giantWindIndex++ % giantWind.Length;
            giantWindOrigins[index] = transform.TransformPoint(from + giantWindDirection * giantWindDistance);
            giantWindAges[index] = 0;
            giantWind[index].enabled = true;
            giantWindDistance += GiantWindSpacing;
        }
        giantWindDistance -= length;
    }

    void UpdateGiantWind(float dt)
    {
        Vector2 side = new(-giantWindDirection.y, giantWindDirection.x);
        for (int i = 0; i < giantWind.Length; i++)
        {
            LineRenderer arc = giantWind[i];
            if (arc == null || !arc.enabled) continue;
            float progress = (giantWindAges[i] += dt) / GiantWindLifetime;
            if (progress >= 1)
            {
                arc.enabled = false;
                continue;
            }
            Color color = new(.79f, 1, .96f, .80f * (1 - progress));
            arc.startColor = arc.endColor = color;
            arc.widthMultiplier = Mathf.Lerp(.18f, .07f, progress);
            Vector2 center = giantWindOrigins[i] - giantWindDirection * (.4f + progress * 1.6f);
            float spread = Mathf.Lerp(.55f, 2.65f, progress);
            for (int point = 0; point < arc.positionCount; point++)
            {
                float across = 2f * point / (arc.positionCount - 1) - 1;
                arc.SetPosition(point, center + side * (across * spread)
                    - giantWindDirection * (across * across * spread * .85f));
            }
        }
    }

    void ClearGiantWind()
    {
        foreach (LineRenderer arc in giantWind)
            if (arc != null) arc.enabled = false;
    }

    void AdvanceFlight(SwordInstance sword, float dt)
    {
        if (sword.CapturedGiantSword)
        {
            AdvanceGiantFlight(sword, dt);
            return;
        }
        Vector2 position = sword.Root.localPosition;
        bool chasingEnemy = lockedEnemy != null && lockedEnemy.Health > 0;
        Vector2 destination = chasingEnemy ? lockedEnemy.Position : sword.Target;
        float nearest = InterceptRange * InterceptRange;
        bool chasingBullet = false;
        foreach (EnemyBullet bullet in bullets)
        {
            Vector2 bulletPosition = bullet.Root.localPosition;
            float distance = (bulletPosition - position).sqrMagnitude;
            if (chasingEnemy || distance > nearest)
                continue;
            nearest = distance;
            destination = bulletPosition;
            chasingBullet = true;
        }
        bool recoiling = sword.BlockRecoilTime > 0;
        float recoilStep = Mathf.Min(dt, sword.BlockRecoilTime);
        sword.BlockRecoilTime = Mathf.Max(0, sword.BlockRecoilTime - dt);
        Vector2 next;
        if (chasingEnemy && !recoiling)
            next = AdvanceHomingCurve(sword, lockedEnemy, dt);
        else
        {
            sword.CurveEnemy = null;
            sword.Trail.emitting = true;
            next = recoiling ? position + sword.BlockRecoilVelocity * recoilStep :
                Vector2.MoveTowards(position, destination, LaunchSpeed * SwordSpeedMultiplier * dt);
        }
        PointSword(sword, next - position);
        if (stage.ClipTerrain(position, next, Vector2.zero, out Vector2 impact, out Vector2 normal))
        {
            sword.Root.localPosition = impact;
            SetPlanted(sword, next - position, normal);
            return;
        }
        sword.Root.localPosition = next;
        if (TryReboundFromEnemy(sword, position, next) || TryHitBullet(sword, position, next))
            return;

        if (!recoiling && !chasingEnemy && !chasingBullet && next == sword.Target)
            BeginFall(sword);
    }

    bool TryHitBullet(SwordInstance sword, Vector2 from, Vector2 to)
    {
        for (int i = 0; i < bullets.Count; i++)
        {
            EnemyBullet bullet = bullets[i];
            if (bullet == sword.LastGiantIntercept) continue;
            if (DistanceToSegmentSquared(Vector2.zero, from - bullet.Previous,
                    to - (Vector2)bullet.Root.localPosition) > Mathf.Pow(InterceptContact + bullet.ContactRadius, 2))
                continue;
            Vector2 knockback = BulletKnockback(bullet);
            InterceptBullet(i, sword);
            sword.BlocksRemaining--;
            if (sword.BlocksRemaining <= 0)
            {
                BeginFall(sword);
                sword.FallVelocity = knockback;
            }
            else if (!sword.CapturedGiantSword)
            {
                sword.BlockRecoilVelocity = knockback;
                sword.BlockRecoilTime = InterceptRecovery;
            }
            return true;
        }
        return false;
    }

    bool TryReboundFromEnemy(SwordInstance sword, Vector2 from, Vector2 to)
    {
        Vector2 travel = to - from;
        float length = travel.magnitude;
        if (length <= .000001f)
            return false;

        float releaseRadius = EnemyHitRadius * (sword.ContactEnemy != null ?
            Mathf.Abs(sword.ContactEnemy.View.transform.lossyScale.x) : 1) + EnemyContactMargin;
        if (sword.ContactEnemy != null && (sword.ContactEnemy.Health == 0 ||
            (from - sword.ContactEnemy.Position).sqrMagnitude > releaseRadius * releaseRadius))
            sword.ContactEnemy = null;

        Vector2 direction = travel / length;
        EnemyState target = null;
        float nearest = length;
        foreach (EnemyState enemy in enemySystem.Enemies)
        {
            if (enemy == sword.ContactEnemy)
                continue;
            Vector2 relative = from - enemy.Position;
            float projection = Vector2.Dot(relative, direction);
            float hitRadius = EnemyHitRadius * Mathf.Abs(enemy.View.transform.lossyScale.x);
            float discriminant = projection * projection - relative.sqrMagnitude + hitRadius * hitRadius;
            if (discriminant < 0 || (projection >= 0 && relative.sqrMagnitude > hitRadius * hitRadius))
                continue;
            float distance = Mathf.Max(0, -projection - Mathf.Sqrt(discriminant));
            if (distance > nearest)
                continue;
            nearest = distance;
            target = enemy;
        }
        if (target == null)
            return false;

        Vector2 impact = from + direction * nearest;
        Vector2 normal = (impact - target.Position).normalized;
        Vector2 rebound = normal.sqrMagnitude > 0 && Vector2.Dot(direction, normal) < 0
            ? Vector2.Reflect(direction, normal) : -direction;
        sword.Root.localPosition = impact;
        if (sword.CapturedPortalSword)
        {
            enemySystem.Damage(target, SwordDamage(sword));
            if (target.Health == 0 && lockedEnemy == target) lockedEnemy = null;
            ReturnBorrowedSword(sword);
            SpawnHitBurst(impact, new Color(1, .77f, .34f));
            return true;
        }
        BeginFall(sword);
        sword.ContactEnemy = target;
        sword.FallVelocity = rebound * ReboundSpeed;
        if (rebound.y > .8f)
        {
            float side = rebound.x < 0 || (rebound.x == 0 && direction.x < 0) ? -1f : 1f;
            sword.FallVelocity.x += side * ReboundSideSpeed;
        }
        PointSword(sword, sword.FallVelocity);
        enemySystem.Damage(target, SwordDamage(sword));
        if (target.Health > 0 && SwordMarksTarget(sword))
            lockedEnemy = target;
        if (target.Health == 0 && lockedEnemy == target)
            lockedEnemy = null;
        SpawnHitBurst(impact, new Color(1, .77f, .34f));
        return true;
    }

    static Vector2 BulletKnockback(EnemyBullet bullet)
    {
        Vector2 direction = (Vector2)bullet.Root.localPosition - bullet.Previous;
        return direction.normalized * BulletKnockbackSpeed + Vector2.up * 2f;
    }

    void InterceptBullet(int index, SwordInstance sword)
    {
        if (bullets[index].GiantChaseOwner != null) sword.LastGiantIntercept = bullets[index];
        SpawnHitBurst(bullets[index].Root.localPosition, new Color(.65f, 1, .96f));
        enemySystem.InterceptProjectile(index);
    }

    void BeginFall(SwordInstance sword)
    {
        if (sword.CapturedGiantSword || (sword.BorrowedFrom != null && sword.BorrowedUsed))
        {
            BeginRecall(sword);
            return;
        }
        sword.State = SwordState.Falling;
        sword.FallVelocity = Vector2.zero;
        sword.ReturnAfterRecoil = 0;
        sword.Trail.emitting = false;
        SetFusionSize(sword, orbitVisual: false);
        sword.Root.localRotation = Quaternion.identity;
        sword.GroundMark.enabled = false;
    }

    void AdvanceFall(SwordInstance sword, float dt)
    {
        float floor = -GridSize.y * .5f;
        Vector2 position = sword.Root.localPosition;
        Vector2 gravity = Vector2.down * SwordGravity;
        Vector2 next = position + (sword.FallVelocity + .5f * gravity * dt) * dt;
        sword.FallVelocity += gravity * dt;
        Vector2 direction = next - position;
        bool terrainHit = stage.ClipTerrain(position, next, Vector2.zero, out next, out Vector2 normal);
        if (TryReboundFromEnemy(sword, position, next))
            return;

        for (int i = bullets.Count - 1; i >= 0; i--)
        {
            EnemyBullet bullet = bullets[i];
            if (bullet == sword.LastGiantIntercept) continue;
            if (DistanceToSegmentSquared(Vector2.zero, position - bullet.Previous,
                    next - (Vector2)bullet.Root.localPosition) <= Mathf.Pow(InterceptContact + bullet.ContactRadius, 2))
            {
                sword.FallVelocity = BulletKnockback(bullet);
                InterceptBullet(i, sword);
            }
        }

        sword.Root.localPosition = next;
        if (sword.ReturnAfterRecoil > 0)
        {
            sword.ReturnAfterRecoil -= dt;
            if (sword.ReturnAfterRecoil <= 0 || next.y <= floor || terrainHit)
            {
                BeginRecall(sword);
                return;
            }
        }
        if (next.y > floor && !terrainHit)
        {
            PointSword(sword, sword.FallVelocity);
            return;
        }

        SetPlanted(sword, direction, normal);
    }

    void AdvancePlanted(SwordInstance sword, float dt)
    {
        if (sword.BorrowedFrom == null || sword.PermanentLoot)
            return;
        sword.RecallCooldown = Mathf.Max(0, sword.RecallCooldown - dt);
        float charge = 1 - sword.RecallCooldown / Mathf.Max(.1f, swordRecallCooldown);
        sword.GroundMark.color = Color.Lerp(new Color32(76, 101, 120, 255), ActiveBlade, charge);
        if (sword.RecallCooldown == 0)
            BeginRecall(sword);
    }

    void SetPlanted(SwordInstance sword, Vector2 direction, Vector2 normal)
    {
        if (sword.CapturedGiantSword || (sword.BorrowedFrom != null && sword.BorrowedUsed))
        {
            BeginRecall(sword);
            return;
        }
        sword.Trail.emitting = false;
        PointSword(sword, direction.sqrMagnitude > 0 ? direction : -normal);
        sword.GroundMark.transform.rotation = sword.Root.parent.rotation *
            Quaternion.Euler(0, 0, Vector2.SignedAngle(Vector2.up, normal));
        sword.State = SwordState.Planted;
        sword.RecallCooldown = Mathf.Max(.1f, swordRecallCooldown);
        sword.GroundMark.color = new Color32(76, 101, 120, 255);
        sword.Blade.color = sword.CapturedPortalSword || sword.CapturedGiantSword ? sword.CapturedBladeColor : PlantedBlade;
        sword.GroundMark.enabled = true;
    }

    void AdvanceReturn(SwordInstance sword, float dt)
    {
        Vector2 position = sword.Root.localPosition;
        Vector2 home = RestPosition(orbiting.Count,orbiting.Count+1);
        Vector2 target = home+(sword.ReturnPhase == 0 ? sword.ReturnOvershoot : Vector2.zero);
        PointSword(sword,target-position);
        Vector2 next = sword.ReturnPhase == 0 ? Vector2.MoveTowards(position,target,EffectiveReturnSpeed*dt) :
            Vector2.SmoothDamp(position,target,ref sword.RestVelocity,.10f,EffectiveReturnSpeed,dt);
        sword.Root.localPosition = next;
        if (sword.ReturnPhase == 1) sword.ReturnSettleTime += dt;
        if (Vector2.Distance(next,target) > .035f && sword.ReturnSettleTime < .2f) return;
        if (sword.ReturnPhase == 0)
        {
            sword.ReturnPhase = 1;
            sword.RestVelocity = Vector2.zero;
            sword.Trail.emitting = false;
            return;
        }
        foreach (SwordInstance returned in SeparateSword(sword))
        {
            returned.State = SwordState.Orbiting;
            returned.Trail.emitting = false;
            returned.Trail.Clear();
            returned.PreviousPosition = returned.Root.localPosition;
            returned.RestVelocity = Vector2.zero;
            orbiting.Add(returned);
        }
    }
    Vector2 AdvanceHomingCurve(SwordInstance sword, EnemyState enemy, float dt)
    {
        Vector2 position = sword.Root.localPosition;
        if (sword.CurveEnemy != enemy)
        {
            sword.CurveEnemy = enemy;
            sword.CurveOrigin = position;
            sword.CurveProgress = 0;
            Vector2 travel = enemy.Position - position;
            Vector2 perpendicular = new Vector2(-travel.y, travel.x).normalized;
            float bend = Mathf.Clamp(travel.magnitude * .32f, .5f, 3.5f);
            float side = Random.value < .5f ? -1 : 1;
            sword.CurveControl = (position + enemy.Position) * .5f + perpendicular * (bend * side);
            sword.CurveLength = Mathf.Max(.1f, Vector2.Distance(position, sword.CurveControl) +
                Vector2.Distance(sword.CurveControl, enemy.Position));
            if (!sword.Trail.emitting)
                sword.Trail.Clear();
            sword.Trail.time = .26f;
            sword.Trail.startWidth = .07f + sword.FusionSize * .008f;
            if (sword.IsEnemyWeapon)
                ApplyOwnedSwordColors(sword);
            else
            {
                sword.Trail.startColor = new Color(1, .69f, .36f, .85f);
                sword.Trail.endColor = new Color(.44f, .86f, 1, 0);
            }
            sword.Trail.emitting = true;
        }

        sword.CurveProgress = Mathf.Clamp01(sword.CurveProgress +
            LaunchSpeed * SwordSpeedMultiplier * dt / sword.CurveLength);
        float t = sword.CurveProgress;
        Vector2 first = Vector2.Lerp(sword.CurveOrigin, sword.CurveControl, t);
        Vector2 second = Vector2.Lerp(sword.CurveControl, enemy.Position, t);
        Vector2 next = Vector2.Lerp(first, second, t);
        if ((next - position).sqrMagnitude <= .000001f &&
            (position - enemy.Position).sqrMagnitude <= EnemyHitRadius * EnemyHitRadius)
            next = position + Vector2.down * .001f;
        return next;
    }
}
