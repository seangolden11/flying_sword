using System.Collections.Generic;
using UnityEngine;

public sealed class EnemySystem : MonoBehaviour
{
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
        geometry = stageGeometry;
        player = target;
        view = camera;
    }

    public void ResetEnemies()
    {
        foreach (EnemyState enemy in enemies)
        {
            enemy.View.gameObject.SetActive(false);
            enemyPool.Push(enemy.View);
        }
        enemies.Clear();
        for (int i = bullets.Count - 1; i >= 0; i--)
            RemoveBullet(i);
        RespawnTime = respawnDelay;
        if (fixedEncounters)
            foreach (EnemyPlacement placement in placements)
                if (!placement.boss) SpawnPlaced(placement);
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
            UpdateFire(enemy, dt);
            enemy.HitTimer = Mathf.Max(0, enemy.HitTimer - dt);
            enemy.View.Refresh(enemy, this, player.Position, showDetectionRange, detectionRange, view);
        }
        for (int i = bullets.Count - 1; i >= 0; i--)
        {
            EnemyBullet bullet = bullets[i];
            bullet.Previous = bullet.Root.position;
            Vector2 next = bullet.Previous + bullet.Velocity * dt;
            if (geometry.ClipTerrain(bullet.Previous, next, Vector2.zero, out _))
                RemoveBullet(i);
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
        => SpawnEnemy(placement.transform.position, placement.health, placement.boss, placement.scale);

    EnemyState SpawnEnemy(Vector2 position, int health, bool boss, float scale)
    {
        EnemyView enemyView = enemyPool.Count > 0 ? enemyPool.Pop() : Instantiate(enemyPrefab, transform);
        enemyView.transform.position = position;
        enemyView.transform.localScale = Vector3.one * scale;
        enemyView.gameObject.SetActive(true);
        enemyView.SetLocked(false);
        var enemy = new EnemyState
        {
            View = enemyView, Health = health, MaxHealth = health, IsBoss = boss,
            BurstTimer = burstInterval, BasicTimer = Random.Range(.3f, basicInterval)
        };
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
        enemy.IsInRange = (enemy.Position - player.Position).sqrMagnitude <= detectionRange * detectionRange;
        enemy.HasLineOfSight = !geometry.ClipTerrain(enemy.Position, player.Position, Vector2.zero, out _);
        enemy.IsAware = enemy.AlertedByDamage || (enemy.IsInRange && enemy.HasLineOfSight);
        if (!enemy.IsAware)
            return;
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
            enemy.BasicTimer = basicInterval * (enemy.IsBoss ? .65f : 1);
            if (enemy.ShotTimer <= 0)
            {
                enemy.ShotsRemaining--;
                enemy.ShotTimer += burstShotInterval;
                SpawnBullet(enemy.Position);
            }
        }
        else
        {
            enemy.BasicTimer -= dt;
            if (enemy.BasicTimer <= 0)
            {
                enemy.BasicTimer += basicInterval * (enemy.IsBoss ? .65f : 1);
                SpawnBullet(enemy.Position);
            }
        }
    }

    void SpawnBullet(Vector2 position)
    {
        BulletView bulletView = bulletPool.Count > 0 ? bulletPool.Pop() : Instantiate(bulletPrefab, transform);
        bulletView.transform.position = position;
        bulletView.gameObject.SetActive(true);
        Vector2 direction = player.Position - position;
        if (direction.sqrMagnitude <= .000001f)
            direction = Vector2.left;
        bullets.Add(new EnemyBullet
        {
            View = bulletView, Previous = position, Velocity = direction.normalized * bulletSpeed
        });
    }

    public void RemoveBullet(int index)
    {
        EnemyBullet bullet = bullets[index];
        bullet.View.gameObject.SetActive(false);
        bulletPool.Push(bullet.View);
        bullets.RemoveAt(index);
    }

    public void Damage(EnemyState enemy, int damage)
    {
        if (enemy.Health <= 0 || damage <= 0)
            return;
        if (!enemy.AlertedByDamage)
            enemy.BasicTimer = 0;
        enemy.AlertedByDamage = true;
        enemy.IsAware = true;
        enemy.Health = Mathf.Max(0, enemy.Health - damage);
        enemy.HitTimer = .22f;
        if (enemy.Health > 0)
            return;
        enemy.View.gameObject.SetActive(false);
        enemyPool.Push(enemy.View);
        enemies.Remove(enemy);
        if (enemies.Count == 0)
            RespawnTime = respawnDelay;
    }
}
