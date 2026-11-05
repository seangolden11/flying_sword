using System.Collections.Generic;
using UnityEngine;

public sealed partial class TurnActors
{
    sealed class Fragment
    {
        public Sword Owner;
        public Transform Root;
        public Enemy Target;
        public float AttackCooldown;
    }
    sealed class WeaponEffect
    {
        public SpriteRenderer Renderer;
        public float Remaining, Duration, Size;
    }
    readonly List<Fragment> fragments = new();
    readonly List<WeaponEffect> weaponEffects = new();

    void SpawnWeaponEffect(string art, Vector2 position, float size, float duration)
    {
        Sprite sprite = GameArt.Get(art);
        var renderer = StageManager.Draw(transform, art, sprite != null ? sprite : square,
            sprite != null ? Color.white : new Color(0, 1, 1, .4f), position, Vector2.one * GameArt.WorldScale(sprite, size), 8);
        weaponEffects.Add(new WeaponEffect { Renderer = renderer, Remaining = duration, Duration = duration, Size = size });
    }
    Enemy NearestEnemy(Vector2 position, float range, Enemy excluded, HashSet<Enemy> hit = null)
    {
        Enemy target = null;
        float nearest = range * range;
        foreach (Enemy enemy in enemies)
        {
            if (enemy == excluded || (hit != null && hit.Contains(enemy))) continue;
            float distance = ((Vector2)enemy.Root.localPosition - position).sqrMagnitude;
            if (distance > nearest) continue;
            nearest = distance;
            target = enemy;
        }
        return target;
    }
    void ForwardSpecialEffects(Sword sword, Enemy directTarget, Vector2 impact)
    {
        Ability ability = Abilities(sword);
        if ((ability & Ability.Fragments) != 0)
        {
            int existing = 0;
            foreach (Fragment clone in fragments)
                if (clone.Owner == sword) existing++;
            Enemy previousTarget = null;
            for (int i = existing; i < 2; i++)
            {
                Enemy target = NearestEnemy(impact, fragmentRange, previousTarget) ?? NearestEnemy(impact, fragmentRange, null);
                previousTarget = target;
                Sprite sprite = GameArt.Weapon(sword.Kind);
                Vector2 position = ClampSwordPosition(sword, impact + Vector2.up * (i == 0 ? .2f : -.2f));
                var renderer = StageManager.Draw(transform, "Weapon clone", sprite != null ? sprite : bladeSprite,
                    new Color(.6f, 1, 1, .8f), position, Vector2.one * GameArt.WorldScale(sprite != null ? sprite : bladeSprite, fragmentVisualSize), 6);
                fragments.Add(new Fragment { Owner = sword, Root = renderer.transform, Target = target,
                    AttackCooldown = 0 });
            }
            if (existing < 2) SpawnWeaponEffect("fragment_fx", impact, .8f, .2f);
        }
        if ((ability & Ability.Electric) != 0)
        {
            SpawnWeaponEffect("electric", impact, electricRadius * 2, .3f);
            foreach (Enemy enemy in enemies.ToArray())
            {
                if (enemy == directTarget || ((Vector2)enemy.Root.localPosition - impact).sqrMagnitude > electricRadius * electricRadius)
                    continue;
                Vector2 direction = ((Vector2)enemy.Root.localPosition - impact).normalized;
                DamageEnemy(enemy, Mathf.Max(1, electricDamage), direction, WeaponKnockback(sword));
            }
        }
    }
    bool StartChain(Sword sword, Enemy first, Vector2 impact)
    {
        if ((Abilities(sword) & Ability.Chain) == 0 || sword.ChainUsed) return false;
        sword.ChainUsed = true;
        Enemy target = NearestEnemy(impact, chainRange, first, sword.FlightHits);
        if (target == null) return false;
        sword.ChainTarget = target;
        sword.ChainRemaining = Mathf.Max(.1f, chainLifetime);
        sword.IsChainFlight = true;
        sword.Target = target.Root.localPosition;
        sword.FlightDistanceRemaining = Vector2.Distance(impact, sword.Target) + 1;
        sword.FlightDirection = (sword.Target - impact).normalized;
        sword.State = SwordState.Flying;
        return true;
    }
    void OnWeaponPlanted(Sword sword)
    {
        if ((Abilities(sword) & Ability.KnockbackBurst) == 0) return;
        Vector2 center = sword.Root.localPosition;
        foreach (Enemy enemy in enemies)
        {
            Vector2 offset = (Vector2)enemy.Root.localPosition - center;
            if (offset.sqrMagnitude > washerKnockbackRadius * washerKnockbackRadius) continue;
            enemy.KnockbackDirection = offset.sqrMagnitude > .000001f ? offset.normalized : Vector2.up;
            enemy.KnockbackSpeed = washerKnockbackSpeed;
            enemy.KnockbackTimer = washerKnockbackDuration;
        }
        SpawnWeaponEffect("synergy_fx", center, washerKnockbackRadius * 2, .35f);
    }
    void ClearFragments(Sword owner)
    {
        for (int i = fragments.Count - 1; i >= 0; i--)
        {
            if (fragments[i].Owner != owner) continue;
            Destroy(fragments[i].Root.gameObject);
            fragments.RemoveAt(i);
        }
    }
    void ClearOwnedEffects(Sword owner)
    {
        ClearFragments(owner);
    }
    void ResetWeaponEffects()
    {
        foreach (Fragment fragment in fragments) Destroy(fragment.Root.gameObject);
        foreach (WeaponEffect effect in weaponEffects) Destroy(effect.Renderer.gameObject);
        fragments.Clear(); weaponEffects.Clear(); pendingSplits.Clear();
    }
    void UpdateWeaponEffects(float dt)
    {
        for (int i = weaponEffects.Count - 1; i >= 0; i--)
        {
            var effect = weaponEffects[i];
            effect.Remaining -= dt;
            if (effect.Remaining <= 0)
            {
                Destroy(effect.Renderer.gameObject);
                weaponEffects.RemoveAt(i);
                continue;
            }
            float fraction = effect.Remaining / effect.Duration;
            effect.Renderer.color = new Color(1, 1, 1, fraction);
            effect.Renderer.transform.localScale = Vector3.one * GameArt.WorldScale(effect.Renderer.sprite, effect.Size) * Mathf.Lerp(1, .65f, fraction);
        }
        for (int i = fragments.Count - 1; i >= 0; i--)
        {
            Fragment fragment = fragments[i];
            fragment.AttackCooldown = Mathf.Max(0, fragment.AttackCooldown - dt);
            bool remove = fragment.Owner.Root == null;
            if (!enemies.Contains(fragment.Target) ||
                ((Vector2)fragment.Target.Root.localPosition - (Vector2)fragment.Root.localPosition).sqrMagnitude > fragmentRange * fragmentRange)
                fragment.Target = NearestEnemy(fragment.Root.localPosition, fragmentRange, null);
            if (!remove && fragment.Target != null && fragment.AttackCooldown <= 0)
            {
                Vector2 from = fragment.Root.localPosition;
                Vector2 target = fragment.Target.Root.localPosition;
                Vector2 next = ClampSwordPosition(fragment.Owner, Vector2.MoveTowards(from, target, fragmentSpeed * dt));
                fragment.Root.localPosition = next;
                Vector2 direction = next - from;
                if (direction.sqrMagnitude > .000001f)
                    fragment.Root.localRotation = Quaternion.Euler(0, 0, Vector2.SignedAngle(Vector2.down, direction));
                if (DistanceToSegmentSquared(target, from, next) <= EnemyHitRadius * EnemyHitRadius)
                {
                    SpawnHitBurst(target, ActiveBlade);
                    DamageEnemy(fragment.Target, Mathf.Max(1, fragmentDamage), (target - from).normalized);
                    fragment.AttackCooldown = Mathf.Max(.05f, fragmentAttackInterval);
                    fragment.Target = null;
                }
            }
            if (remove)
            {
                Destroy(fragment.Root.gameObject);
                fragments.RemoveAt(i);
            }
            if (IsChoosingReward || IsGameOver) break;
        }
    }
    void UpdateGuardDamage()
    {
        foreach (Sword weapon in orbiting)
        {
            Vector2 position = weapon.Root.localPosition;
            Vector2 bladeBase = weapon.Visual.TransformPoint(new Vector3(0, BladeLength));
            bladeBase = transform.InverseTransformPoint(bladeBase);
            float radius = EnemyHitRadius + BladeHalfWidth * SwordScale(weapon);
            foreach (Enemy enemy in enemies.ToArray())
            {
                if (weapon.GuardHits.TryGetValue(enemy, out float nextHit) && combatTime < nextHit) continue;
                Vector2 target = enemy.Root.localPosition;
                if (DistanceToSegmentSquared(target, weapon.PreviousPosition, position) > radius * radius &&
                    DistanceToSegmentSquared(target, position, bladeBase) > radius * radius) continue;
                weapon.GuardHits[enemy] = combatTime + Mathf.Max(.05f, guardHitInterval);
                SpawnHitBurst(target, ActiveBlade);
                DamageEnemy(enemy, SwordDamage(weapon), (target - position).normalized, WeaponKnockback(weapon));
                if (IsChoosingReward || IsGameOver) return;
            }
        }
    }
}
