using System.Collections.Generic;
using UnityEngine;

// Enemy and progression rules live here; existing sword state transitions stay in TurnActors.cs.
public sealed partial class TurnActors
{
    [Header("Survival test tuning")]
    [SerializeField, Min(.1f)] float spawnInterval = 2f;
    [SerializeField, Min(1)] int initialSpawnCount = 1;
    [SerializeField, Min(1)] float spawnCountGrowthInterval = 60f;
    [UnityEngine.Serialization.FormerlySerializedAs("additionalEnemiesPerLevel")]
    [SerializeField, Min(0)] int additionalEnemiesPerTimeStep = 1;
    public int CurrentSpawnCount => Mathf.Max(1, initialSpawnCount +
        Mathf.FloorToInt(combatTime / Mathf.Max(1, spawnCountGrowthInterval)) * additionalEnemiesPerTimeStep);
    [SerializeField, Range(0, 1)] float rangedEnemyChance = .35f;
    [SerializeField, Min(.1f)] float meleeSpeed = 1.1f;
    [SerializeField, Min(.1f)] float rangedSpeed = .8f;
    [SerializeField, Min(1)] float rangedAttackRange = 7f;
    [SerializeField, Min(.1f)] float rangedAttackInterval = 2.5f;
    [SerializeField, Min(.1f)] float rangedMoveDuration = .9f;
    [SerializeField, Min(.1f)] float projectileSpeed = 3.4f;
    [SerializeField, Min(0)] float knockbackSpeed = 6f;
    [SerializeField, Min(.01f)] float knockbackDuration = .2f;
    [SerializeField, Min(0)] float spawnIntervalReductionPerLevel = .06f;
    [SerializeField, Min(.05f)] float minimumSpawnInterval = .25f;
    public float CurrentSpawnInterval => Mathf.Max(Mathf.Max(.05f, minimumSpawnInterval), spawnInterval - (Level - 1) * spawnIntervalReductionPerLevel);

    [Header("Progression test tuning")]
    [SerializeField, Min(1)] int initialRequiredExperience = 3;
    [SerializeField, Min(1)] int requiredExperienceIncrease = 2;

    sealed class Enemy
    {
        public Transform Root;
        public SpriteRenderer Renderer;
        public bool Ranged;
        public int Health;
        public int MaxHealth;
        public Transform HealthFill;
        public float AttackTimer;
        public float MoveTimer;
        public float KnockbackTimer;
        public Vector2 KnockbackDirection;
        public float KnockbackSpeed;
        public float StrafeSign;
    }

    readonly List<Enemy> enemies = new();
    SpriteRenderer heroRenderer;
    float spawnTimer, invulnerability;
    int pendingSwords;
    bool hasCounter, hasKillHeal;
    int killsSinceHeal;
    float counterRemaining, combatTime;
    public int Health { get; private set; } = 10;
    public int Level { get; private set; } = 1;
    public double Experience { get; private set; }
    public double RequiredExperience { get; private set; } = 3;
    public int OwnedSwordCount => InventoryWeapons().Count;
    public int EnemyCount => enemies.Count;
    public bool IsGameOver => Health <= 0;

    void ResetSurvival()
    {
        ResetRewards();
        ResetWeaponEffects();
        hasCounter = hasKillHeal = false;
        killsSinceHeal = 0;
        counterRemaining = combatTime = 0;
        foreach (Enemy enemy in enemies)
            Destroy(enemy.Root.gameObject);
        enemies.Clear();
        Health = 10;
        Level = 1;
        Experience = 0;
        RequiredExperience = Mathf.Max(1, initialRequiredExperience);
        pendingSwords = 0;
        spawnTimer = .5f;
        invulnerability = 0;
        heroRenderer.color = Color.white;
    }

    void UpdateSurvival(float dt)
    {
        combatTime += dt;
        counterRemaining = Mathf.Max(0, counterRemaining - dt);
        invulnerability = Mathf.Max(0, invulnerability - dt);
        heroRenderer.color = invulnerability > 0 && Mathf.Sin(Time.time * 30) > 0
            ? new Color(1, .4f, .4f, .4f) : Color.white;
        spawnTimer -= dt;
        if (spawnTimer <= 0)
        {
            spawnTimer = CurrentSpawnInterval;
            for (int i = 0; i < CurrentSpawnCount; i++)
                SpawnEnemy();
        }

        for (int enemyIndex = enemies.Count - 1; enemyIndex >= 0; enemyIndex--)
        {
            Enemy enemy = enemies[enemyIndex];
            Vector2 position = enemy.Root.localPosition;
            enemy.AttackTimer -= dt;
            if (enemy.KnockbackTimer > 0)
            {
                float step = Mathf.Min(dt, enemy.KnockbackTimer);
                enemy.Root.localPosition = ClampTarget(position + enemy.KnockbackDirection * enemy.KnockbackSpeed * step);
                enemy.KnockbackTimer -= dt;
                enemy.Renderer.color = Color.white;
                continue;
            }
            enemy.Renderer.color = enemy.Ranged ? new Color32(245, 157, 67, 255) : new Color32(225, 77, 109, 255);
            if (!enemy.Ranged || position.magnitude > rangedAttackRange)
                position = Vector2.MoveTowards(position, Vector2.zero, (enemy.Ranged ? rangedSpeed : meleeSpeed) * dt);
            else if (enemy.AttackTimer <= 0)
            {
                SpawnBullet(position);
                enemy.AttackTimer = Mathf.Max(.1f, rangedAttackInterval);
                enemy.MoveTimer = rangedMoveDuration;
            }
            else if (enemy.MoveTimer > 0)
            {
                Vector2 tangent = new Vector2(-position.y, position.x).normalized * enemy.StrafeSign;
                position += tangent * rangedSpeed * Mathf.Min(dt, enemy.MoveTimer);
                enemy.MoveTimer -= dt;
            }
            enemy.Root.localPosition = ClampTarget(position);
            if (position.sqrMagnitude <= .65f * .65f)
                DamagePlayer(enemy);
            if (IsGameOver || IsChoosingReward)
                break;
        }
    }

    void SpawnEnemy()
    {
        // Sample the visible rectangle perimeter, rather than the distant map boundary.
        float halfY = view.orthographicSize - .5f;
        float halfX = view.orthographicSize * view.aspect - .5f;
        float perimeter = Random.Range(0f, 4 * (halfX + halfY));
        Vector2 position;
        if (perimeter < 2 * halfX)
            position = new Vector2(perimeter - halfX, halfY);
        else if ((perimeter -= 2 * halfX) < 2 * halfY)
            position = new Vector2(halfX, perimeter - halfY);
        else if ((perimeter -= 2 * halfY) < 2 * halfX)
            position = new Vector2(halfX - perimeter, -halfY);
        else
            position = new Vector2(-halfX, halfY - (perimeter - 2 * halfX));
        position += (Vector2)view.transform.localPosition;
        bool ranged = Random.value < rangedEnemyChance;
        var renderer = StageManager.Draw(transform, ranged ? "Ranged enemy" : "Melee enemy", square,
            ranged ? new Color32(245, 157, 67, 255) : new Color32(225, 77, 109, 255),
            ClampTarget(position), Vector2.one * (ranged ? .65f : .8f), 4);
        StageManager.Draw(renderer.transform, "HP background", square, new Color32(14, 21, 34, 255),
            new Vector2(0, .8f), new Vector2(1, .14f), 6);
        var fill = StageManager.Draw(renderer.transform, "HP fill", square, new Color32(114, 225, 132, 255),
            new Vector2(0, .8f), new Vector2(.94f, .08f), 7);
        int health = ranged ? 1 : 2;
        enemies.Add(new Enemy { Root = renderer.transform, Renderer = renderer, Ranged = ranged,
            Health = health, MaxHealth = health, HealthFill = fill.transform,
            StrafeSign = Random.value < .5f ? -1 : 1 });
    }

    Enemy FindFirstEnemyHit(Sword sword, Vector2 from, Vector2 to, out float hitDistance)
    {
        hitDistance = 0;
        Vector2 travel = to - from;
        float length = travel.magnitude;
        if (length <= .000001f)
            return null;
        Vector2 direction = travel / length;
        float bladeLength = BladeLength * SwordScale(sword);
        float radius = EnemyHitRadius + BladeHalfWidth * SwordScale(sword);
        Vector2 bladeBase = from - direction * bladeLength;
        Enemy result = null;
        float nearest = float.PositiveInfinity;
        foreach (Enemy enemy in enemies)
        {
            if (sword.FlightHits.Contains(enemy)) continue;
            Vector2 relative = bladeBase - (Vector2)enemy.Root.localPosition;
            float projection = Vector2.Dot(relative, direction);
            float discriminant = projection * projection - relative.sqrMagnitude + radius * radius;
            if (discriminant < 0 || (projection >= 0 && relative.sqrMagnitude > radius * radius))
                continue;
            float distance = Mathf.Max(0, -projection - Mathf.Sqrt(discriminant) - bladeLength);
            if (distance <= length && distance < nearest)
            {
                nearest = distance;
                hitDistance = distance;
                result = enemy;
            }
        }
        return result;
    }

    void DamageEnemiesOnReturn(Sword sword, Vector2 from, Vector2 to)
    {
        Vector2 travel = to - from;
        if (travel.sqrMagnitude <= .000001f)
            return;
        Vector2 bladeBase = from - travel.normalized * (BladeLength * SwordScale(sword));
        float radius = EnemyHitRadius + BladeHalfWidth * SwordScale(sword);
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            Enemy enemy = enemies[i];
            Vector2 position = enemy.Root.localPosition;
            if (sword.ReturnHits.Contains(enemy) ||
                DistanceToSegmentSquared(position, bladeBase, to) > radius * radius)
                continue;
            sword.ReturnHits.Add(enemy);
            SpawnHitBurst(position, new Color(1, .77f, .34f));
            DamageEnemy(enemy, SwordDamage(sword), travel.normalized, WeaponKnockback(sword));
        }
    }

    void DamageEnemy(Enemy enemy, int damage, Vector2 direction, float knockback = -1)
    {
        if (!enemies.Contains(enemy) || enemy.Health <= 0) return;
        enemy.Health -= damage;
        float healthRatio = Mathf.Clamp01((float)enemy.Health / enemy.MaxHealth);
        enemy.HealthFill.localScale = new Vector3(.94f * healthRatio, .08f, 1);
        enemy.HealthFill.localPosition = new Vector2(-.47f * (1 - healthRatio), .8f);
        enemy.KnockbackDirection = direction;
        enemy.KnockbackSpeed = knockback < 0 ? knockbackSpeed : knockback;
        enemy.KnockbackTimer = knockbackDuration;
        if (enemy.Health > 0)
            return;
        enemies.Remove(enemy);
        foreach (Sword weapon in swords) weapon.GuardHits.Remove(enemy);
        Destroy(enemy.Root.gameObject);
        if (hasKillHeal && ++killsSinceHeal >= 10)
        {
            killsSinceHeal -= 10;
            Health = Mathf.Min(10, Health + 1);
        }
        GainExperience(1);
    }

    void GainExperience(double amount)
    {
        Experience += amount;
        while (Experience >= RequiredExperience)
        {
            Experience -= RequiredExperience;
            RequiredExperience += Mathf.Max(1, requiredExperienceIncrease);
            Level++;
            spawnTimer = Mathf.Min(spawnTimer, CurrentSpawnInterval);
            QueueLevelReward();
            SpawnHitBurst(Vector2.zero, ActiveBlade);
        }
    }

    void FlushPendingSwords()
    {
        bool changed = pendingSwords > 0 || pendingSplits.Count > 0;
        foreach (Sword compound in pendingSplits) SplitReturnedWeapon(compound);
        pendingSplits.Clear();
        while (pendingSwords > 0)
        {
            pendingSwords--;
            AddWeapon(WeaponKind.Sword);
        }
        if (changed) CheckSynergies();
    }

    void DamagePlayer(Enemy contact = null)
    {
        if (IsGameOver || invulnerability > 0)
            return;
        Health--;
        invulnerability = 2f;
        SpawnHitBurst(Vector2.zero, new Color(1, .3f, .3f));
        shakeTimer = ShakeDuration;
        if (!IsGameOver && contact != null && hasCounter && counterRemaining <= 0 && enemies.Contains(contact))
        {
            counterRemaining = 10;
            Vector2 direction = ((Vector2)contact.Root.localPosition).normalized;
            SpawnHitBurst(contact.Root.localPosition, new Color(1, .7f, .3f));
            DamageEnemy(contact, 1, direction);
        }
    }
}
