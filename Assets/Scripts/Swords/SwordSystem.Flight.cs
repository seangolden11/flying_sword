using System.Collections.Generic;
using UnityEngine;

public sealed partial class SwordSystem
{
    void BeginFlight(SwordInstance sword, Vector2 target)
    {
        sword.State = SwordState.Flying;
        sword.CurveEnemy = null;
        sword.Trail.Clear();
        sword.BlockRecoilTime = 0;
        sword.BlocksRemaining = Mathf.Max(1, sword.FusionSize);
        sword.ContactEnemy = null;
        sword.Trail.emitting = false;
        sword.Target = target;
        SetFusionSize(sword, orbitVisual: false);
    }

    void AdvanceFlight(SwordInstance sword, float dt)
    {
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
            sword.Trail.emitting = false;
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
            if (DistanceToSegmentSquared(Vector2.zero, from - bullet.Previous,
                    to - (Vector2)bullet.Root.localPosition) > InterceptContact * InterceptContact)
                continue;
            Vector2 knockback = BulletKnockback(bullet);
            InterceptBullet(i);
            sword.BlocksRemaining--;
            if (sword.BlocksRemaining <= 0)
            {
                BeginFall(sword);
                sword.FallVelocity = knockback;
            }
            else
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

        float releaseRadius = EnemyHitRadius + EnemyContactMargin;
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
            float discriminant = projection * projection - relative.sqrMagnitude + EnemyHitRadius * EnemyHitRadius;
            if (discriminant < 0 || (projection >= 0 && relative.sqrMagnitude > EnemyHitRadius * EnemyHitRadius))
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

    void InterceptBullet(int index)
    {
        SpawnHitBurst(bullets[index].Root.localPosition, new Color(.65f, 1, .96f));
        enemySystem.RemoveBullet(index);
    }

    static void BeginFall(SwordInstance sword)
    {
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
            if (DistanceToSegmentSquared(Vector2.zero, position - bullet.Previous,
                    next - (Vector2)bullet.Root.localPosition) <= InterceptContact * InterceptContact)
            {
                sword.FallVelocity = BulletKnockback(bullet);
                InterceptBullet(i);
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
        sword.RecallCooldown = Mathf.Max(0, sword.RecallCooldown - dt);
        float charge = 1 - sword.RecallCooldown / Mathf.Max(.1f, swordRecallCooldown);
        sword.GroundMark.color = Color.Lerp(new Color32(76, 101, 120, 255), ActiveBlade, charge);
        if (sword.RecallCooldown == 0)
            BeginRecall(sword);
    }

    void SetPlanted(SwordInstance sword, Vector2 direction, Vector2 normal)
    {
        sword.Trail.emitting = false;
        PointSword(sword, direction.sqrMagnitude > 0 ? direction : -normal);
        sword.GroundMark.transform.rotation = sword.Root.parent.rotation *
            Quaternion.Euler(0, 0, Vector2.SignedAngle(Vector2.up, normal));
        sword.State = SwordState.Planted;
        sword.RecallCooldown = Mathf.Max(.1f, swordRecallCooldown);
        sword.GroundMark.color = new Color32(76, 101, 120, 255);
        sword.Blade.color = PlantedBlade;
        sword.GroundMark.enabled = true;
    }

    void AdvanceReturn(SwordInstance sword, float dt)
    {
        Vector2 position = sword.Root.localPosition;
        Vector2 target = RiderPosition + ReturnTarget(position - RiderPosition);
        PointSword(sword, target - position);
        Vector2 next = Vector2.MoveTowards(position, target, EffectiveReturnSpeed * dt);
        sword.Root.localPosition = next;
        if (next != target)
            return;

        List<SwordInstance> returnedSwords = SeparateSword(sword);
        Vector2 offset = next - RiderPosition;
        float angle = Mathf.Atan2(offset.y, offset.x);
        for (int i = 0; i < returnedSwords.Count; i++)
        {
            SwordInstance returned = returnedSwords[i];
            float splitAngle = angle + (i - (returnedSwords.Count - 1) * .5f) * .1f;
            Vector2 outward = OrbitDirection(splitAngle);
            Vector2 orbitPosition = RiderPosition + outward * OrbitRadius;
            returned.State = SwordState.Orbiting;
            returned.Root.localPosition = orbitPosition;
            returned.PreviousPosition = orbitPosition;
            returned.MergeOrbitOffset = splitAngle - orbitAngle;
            PointSword(returned, outward);
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
            sword.Trail.time = .45f;
            sword.Trail.startWidth = .1f + sword.FusionSize * .012f;
            sword.Trail.startColor = new Color(1, .69f, .36f, .85f);
            sword.Trail.endColor = new Color(.44f, .86f, 1, 0);
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
