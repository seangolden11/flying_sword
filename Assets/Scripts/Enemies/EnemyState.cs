using UnityEngine;

public sealed class EnemyState
{
    public EnemyView View;
    public SwordBossVisual SwordVisual;
    public int Health;
    public int MaxHealth;
    public bool IsBoss;
    public bool IsPractice;
    public bool PracticeSwordIntercepted;
    public bool PracticeSwordRelaunched;
    public EnemyBullet[] BasicSwords;
    public EnemyBullet BossSword => BasicSwords?[0];
    public float BurstTimer;
    public float ShotTimer;
    public float BasicTimer;
    public int BasicCurveSlot;
    public int ShotsRemaining;
    public float HitTimer;
    public float TheftCooldown;
    public float AreaAttackCooldown = 2;
    public int AreaAttacksInVolley;
    public bool UsedSwordPortal;
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

public enum BossSwordState { Ready, Flying, Falling, Planted, Returning, Stolen }

public sealed class EnemyBullet
{
    public BulletView View;
    public SwordInstance PlayerSword;
    public Vector2 Target;
    public bool Spent;
    public bool ReachedTarget;
    public Transform Root => PlayerSword != null ? PlayerSword.Root : View.transform;
    public Vector2 Previous;
    public Vector2 Velocity;
    public bool HomingSword;
    public bool IsPracticeShot;
    public bool IsPortalSword;
    public bool IsPortalCountershot;
    public float Lifetime;
    public EnemyState Owner;
    public EnemyState GiantChaseOwner;
    public float ContactRadius => GiantChaseOwner != null ? .85f : 0;
    public BossSwordState State;
    public float RecallTime;
    public SwordConvergence Convergence;
    public int BossSlot;
    public GameObject RecallCircle;
    public bool RecallInFlight;
}
