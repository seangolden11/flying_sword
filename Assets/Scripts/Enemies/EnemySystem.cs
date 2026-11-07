using System.Collections.Generic;
using UnityEngine;

public sealed partial class EnemySystem : MonoBehaviour
{
    public const int BossMaxHealth = 50;
    [SerializeField] EnemyView enemyPrefab;
    [SerializeField] BulletView bulletPrefab;
    [SerializeField, Min(1)] int maxHealth = 30;
    [SerializeField, Min(1)] int waveSize = 1;
    [SerializeField, Min(.1f)] float respawnDelay = 5;
    [SerializeField, Min(.1f)] float detectionRange = 10;
    [SerializeField, Min(.1f)] float basicInterval = .55f;
    [SerializeField, Min(.1f)] float burstInterval = 5;
    [SerializeField, Min(1)] int burstCount = 10;
    [SerializeField, Min(.01f)] float burstShotInterval = .12f;
    [SerializeField, Min(.1f)] float bulletSpeed = 10.2f;
    [SerializeField] bool showDetectionRange = true;
    [SerializeField] bool fixedEncounters;
    [SerializeField] EnemyPlacement[] placements;
    public bool UsesFixedEncounters => fixedEncounters;
    readonly List<EnemyState> enemies = new();
    readonly List<EnemyBullet> bullets = new();
    readonly Stack<EnemyView> enemyPool = new();
    readonly Stack<BulletView> bulletPool = new();
    readonly Stack<EnemyView> bossPool = new();
    readonly Stack<BulletView> swordProjectilePool = new();
    EnemyView swordBossPrefab;
    BulletView swordProjectilePrefab;
    GameObject recallCirclePrefab;
    StageGeometry geometry;
    PlayerController player;
    Camera view;

    public IReadOnlyList<EnemyState> Enemies => enemies;
    public IReadOnlyList<EnemyBullet> Bullets => bullets;
    public int MaxHealth => maxHealth;
    public float RespawnTime { get; private set; }
    public float BurstInterval => burstInterval;

    public void Initialize(StageGeometry stageGeometry, PlayerController target, Camera camera)
    {
        swordBossPrefab = Resources.Load<EnemyView>("Enemies/SwordBoss");
        swordProjectilePrefab = Resources.Load<BulletView>("Enemies/HomingSword");
        areaWarningPrefab = Resources.Load<GameObject>("Effects/BossAreaWarning");
        recallCirclePrefab = Resources.Load<GameObject>("Effects/EnemyTheftCircle");
        geometry = stageGeometry;
        player = target;
        view = camera;
    }

    public void ResetEnemies()
    {
        PracticeShotIntercepted = false;
        ResetAreaAttacks();
        ResetGiantReward();
        EndSwordPortal();
        ResetBossLoot();
        foreach (EnemyState enemy in enemies)
        {
            ReleaseBossSword(enemy);
            enemy.View.gameObject.SetActive(false);
            (enemy.IsBoss ? bossPool : enemyPool).Push(enemy.View);
        }
        enemies.Clear();
        for (int i = bullets.Count - 1; i >= 0; i--)
            RemoveBullet(i);
        RespawnTime = respawnDelay;
        if (fixedEncounters)
            foreach (EnemyPlacement placement in placements)
                if (!placement.boss || placement.spawnOnStart) SpawnPlaced(placement);
    }

    public void Tick(float dt)
    {
        if (!fixedEncounters && enemies.Count == 0)
        {
            RespawnTime = Mathf.Max(0, RespawnTime - dt);
            if (RespawnTime == 0)
                SpawnWave();
        }
        foreach (EnemyState enemy in enemies)
        {
            if (enemy.SwordVisual != null) enemy.SwordVisual.Face(player.Position);
            TickBossSword(enemy, dt);
            UpdateFire(enemy, dt);
            enemy.HitTimer = Mathf.Max(0, enemy.HitTimer - dt);
            enemy.View.Refresh(enemy, this, player.Position, showDetectionRange, detectionRange, view);
        }
        TickBossLoot(dt);
        TickGiantReward(dt);
        AdvanceAreaAttacks(dt);
        for (int i = bullets.Count - 1; i >= 0; i--)
        {
            EnemyBullet bullet = bullets[i];
            bullet.Previous = bullet.Root.position;
            if (bullet.GiantChaseOwner != null)
            {
                AdvanceGiantChase(i, dt);
                continue;
            }
            if (bullet.PlayerSword != null)
            {
                AdvanceStolenPlayerSword(i,dt);
                continue;
            }
            if (bullet.HomingSword || bullet.IsPortalSword)
            {
                bullet.Lifetime -= dt;
                if (bullet.Lifetime <= 0)
                {
                    RemoveBullet(i);
                    continue;
                }
            }
            if (bullet.HomingSword || bullet.IsPortalCountershot)
            {
                float speed = bullet.IsPortalCountershot ? PortalCountershotSpeed : 7.5f;
                Vector2 desired = player.Position - bullet.Previous;
                if (desired.sqrMagnitude > .000001f)
                    bullet.Velocity = Vector3.RotateTowards(bullet.Velocity,
                        desired.normalized * speed, 110 * Mathf.Deg2Rad * dt, 0);
                bullet.Root.rotation = Quaternion.Euler(0, 0,
                    Vector2.SignedAngle(Vector2.down, bullet.Velocity));
            }
            Vector2 next = bullet.Previous + bullet.Velocity * dt;
            if (geometry.ClipTerrain(bullet.Previous, next, Vector2.zero, out Vector2 impact))
            {
                bullet.Root.position = impact;
                if (bullet.IsPortalSword && !bullet.IsPortalCountershot) PlantPortalSword(i);
                else RemoveBullet(i);
            }
            else
                bullet.Root.position = next;
        }
    }

    void SpawnWave()
    {
        var positions = new Vector2[waveSize];
        bool found = false;
        for (int attempt = 0; attempt < 80; attempt++)
        {
            Vector2 center = new(Random.Range(-15f, 15f), Random.Range(-4f, 4f));
            bool valid = true;
            for (int i = 0; i < waveSize; i++)
            {
                positions[i] = center + new Vector2((i - (waveSize - 1) * .5f) * 1.1f,
                    i % 2 == 1 ? .55f : -.55f);
                if ((positions[i] - player.Position).sqrMagnitude < 16 ||
                    geometry.OverlapsTerrain(positions[i], Vector2.one * .65f))
                {
                    valid = false;
                    break;
                }
            }
            if (!valid)
                continue;
            found = true;
            break;
        }
        if (!found)
        {
            RespawnTime = 1;
            return;
        }
        foreach (Vector2 position in positions)
        {
            SpawnEnemy(position, maxHealth, false, 1);
        }
    }

    public EnemyState SpawnPlaced(EnemyPlacement placement)
        => SpawnEnemy(placement.transform.position, placement.boss ? BossMaxHealth : placement.health, placement.boss, placement.scale);

    EnemyState SpawnEnemy(Vector2 position, int health, bool boss, float scale)
    {
        Stack<EnemyView> pool = boss ? bossPool : enemyPool;
        EnemyView prefab = boss && swordBossPrefab != null ? swordBossPrefab : enemyPrefab;
        EnemyView enemyView = pool.Count > 0 ? pool.Pop() : Instantiate(prefab, transform);
        enemyView.transform.position = position;
        enemyView.transform.localScale = Vector3.one * scale;
        enemyView.gameObject.SetActive(true);
        enemyView.SetLocked(false);
        var enemy = new EnemyState
        {
            View = enemyView, SwordVisual = enemyView.GetComponent<SwordBossVisual>(),
            Health = health, MaxHealth = health, IsBoss = boss,
            BurstTimer = burstInterval, BasicTimer = boss ? 1.1f : Random.Range(.3f, basicInterval)
        };
        if (boss) CreateBossSword(enemy);
        enemies.Add(enemy);
        enemyView.Refresh(enemy, this, player.Position, showDetectionRange, detectionRange, view);
        return enemy;
    }

    public void ClearBullets()
    {
        for (int i = bullets.Count - 1; i >= 0; i--)
            RemoveBullet(i);
    }

    void UpdateFire(EnemyState enemy, float dt)
    {
        enemy.IsInRange = enemy.IsBoss || (enemy.Position - player.Position).sqrMagnitude <= detectionRange * detectionRange;
        enemy.HasLineOfSight = enemy.IsBoss || !geometry.ClipTerrain(enemy.Position, player.Position, Vector2.zero, out _);
        enemy.IsAware = enemy.AlertedByDamage || (enemy.IsInRange && enemy.HasLineOfSight);
        if (!enemy.IsAware)
            return;
        if (enemy.IsBoss)
        {
            if (enemy == swordPortalOwner)
            {
                TickSwordPortal(enemy, dt);
                return;
            }
            TickBossAreaPattern(enemy,dt);
            if (chasingGiantSword?.GiantChaseOwner == enemy) return;
            if (enemy.BossSword != null && enemy.BossSword.State == BossSwordState.Ready)
            {
                enemy.BasicTimer -= dt;
                if (enemy.BasicTimer <= 0) LaunchBossSword(enemy);
            }
            return;
        }
        enemy.BurstTimer -= dt;
        enemy.ShotTimer -= dt;
        if (enemy.BurstTimer <= 0)
        {
            enemy.BurstTimer += burstInterval;
            enemy.ShotsRemaining = burstCount;
            enemy.ShotTimer = 0;
        }
        if (enemy.IsFiring)
        {
            enemy.BasicTimer = basicInterval;
            if (enemy.ShotTimer <= 0)
            {
                enemy.ShotsRemaining--;
                enemy.ShotTimer += burstShotInterval;
                SpawnAttack(enemy);
            }
        }
        else
        {
            enemy.BasicTimer -= dt;
            if (enemy.BasicTimer <= 0)
            {
                enemy.BasicTimer += basicInterval;
                SpawnAttack(enemy);
            }
        }
    }

    void SpawnAttack(EnemyState enemy)
    {
        Vector2 direction = (player.Position - enemy.Position).normalized;
        SpawnProjectile(enemy.Position + (enemy.IsBoss ? direction * 1.1f : Vector2.zero), enemy.IsBoss);
    }

    public bool PracticeShotIntercepted { get; private set; }
    public void FirePracticeShot(Vector2 position) => SpawnProjectile(position, false, practice: true);

    void SpawnProjectile(Vector2 position, bool homingSword, bool practice = false)
    {
        Stack<BulletView> pool = homingSword ? swordProjectilePool : bulletPool;
        BulletView prefab = homingSword && swordProjectilePrefab != null ? swordProjectilePrefab : bulletPrefab;
        BulletView bulletView = pool.Count > 0 ? pool.Pop() : Instantiate(prefab, transform);
        bulletView.transform.position = position;
        bulletView.gameObject.SetActive(true);
        Vector2 direction = player.Position - position;
        if (direction.sqrMagnitude <= .000001f)
            direction = Vector2.left;
        bulletView.transform.rotation = homingSword ? Quaternion.Euler(0, 0,
            Vector2.SignedAngle(Vector2.down, direction)) : Quaternion.identity;
        bulletView.ResetTrail(homingSword);
        bullets.Add(new EnemyBullet
        {
            View = bulletView, Previous = position, Velocity = direction.normalized * (practice ? 4 : homingSword ? 7.5f : bulletSpeed),
            HomingSword = homingSword, IsPracticeShot = practice, Lifetime = 7
        });
    }

    public void RemoveBullet(int index)
    {
        EnemyBullet bullet = bullets[index];
        if (bullet.GiantChaseOwner != null)
        {
            chasingGiantSword = null;
            bullet.View.gameObject.SetActive(false);
            Destroy(bullet.View.gameObject);
            bullets.RemoveAt(index);
            return;
        }
        if (bullet.PlayerSword != null)
        {
            bullet.Spent = true;
            bullet.PlayerSword.Trail.emitting = false;
            bullets.RemoveAt(index);
            return;
        }
        if (bullet.Owner != null)
        {
            bullets.RemoveAt(index);
            DisableBossSword(bullet);
            return;
        }
        bullet.View.ResetTrail(false);
        bullet.View.gameObject.SetActive(false);
        (bullet.HomingSword || bullet.IsPortalSword ? swordProjectilePool : bulletPool).Push(bullet.View);
        bullets.RemoveAt(index);
    }

    public void Damage(EnemyState enemy, int damage)
    {
        if (enemy.Health <= 0 || enemy == swordPortalOwner || damage <= 0)
            return;
        if (!enemy.AlertedByDamage)
            enemy.BasicTimer = 0;
        enemy.AlertedByDamage = true;
        enemy.IsAware = true;
        enemy.Health = Mathf.Max(0, enemy.Health - damage);
        int portalThreshold = Mathf.Max(1, enemy.MaxHealth / 10);
        if (enemy.IsBoss && !enemy.UsedSwordPortal && enemy.Health <= portalThreshold &&
            swordProjectilePrefab != null && areaWarningPrefab != null)
        {
            enemy.Health = portalThreshold;
            StartSwordPortal(enemy);
        }
        enemy.HitTimer = .22f;
        if (enemy.Health > 0)
            return;
        if (enemy.IsBoss) DropBossLoot(enemy);
        else ReleaseBossSword(enemy);
        enemy.View.gameObject.SetActive(false);
        (enemy.IsBoss ? bossPool : enemyPool).Push(enemy.View);
        enemies.Remove(enemy);
        if (enemies.Count == 0)
            RespawnTime = respawnDelay;
    }
}
