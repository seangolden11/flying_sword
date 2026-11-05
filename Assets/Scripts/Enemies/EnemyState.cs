using UnityEngine;

public sealed class EnemyState
{
    public EnemyView View;
    public int Health;
    public int MaxHealth;
    public bool IsBoss;
    public float BurstTimer;
    public float ShotTimer;
    public float BasicTimer;
    public int ShotsRemaining;
    public float HitTimer;
    public bool IsAware;
    public bool AlertedByDamage;
    public bool IsInRange;
    public bool HasLineOfSight;
    public Vector2 Position => View.transform.position;
    public bool IsFiring => ShotsRemaining > 0;
    public SpriteRenderer Body => View.body;
    public SpriteRenderer Core => View.core;
    public float BurstCharge(float interval) => IsFiring ? 1 : 1 - Mathf.Clamp01(BurstTimer / interval);
}

public sealed class EnemyBullet
{
    public BulletView View;
    public Transform Root => View.transform;
    public Vector2 Previous;
    public Vector2 Velocity;
}
