using System.Collections.Generic;
using UnityEngine;

public enum SwordState { Orbiting, Flying, Falling, Planted, Returning, Stored, EnemyHeld, EnemyFlying, Preparing }

public sealed class SwordInstance
{
    public SwordView View;
    public EnemyState BorrowedFrom;
    public bool BorrowedUsed;
    public bool PermanentLoot;
    public bool CapturedPortalSword;
    public bool CapturedGiantSword;
    public Vector2 GiantLaunchDirection;
    public Vector2 GiantLaunchOrigin;
    public EnemyBullet LastGiantIntercept;
    public bool IsEnemyWeapon => BorrowedFrom != null || PermanentLoot || CapturedPortalSword || CapturedGiantSword;
    public bool PendingRemoval;
    public float CapturedTimeRemaining;
    public float CapturedShakeTime;
    public Color CapturedBladeColor;
    public Transform Root => View.transform;
    public Transform Visual => View.visual;
    public TrailRenderer Trail => View.trail;
    public SpriteRenderer Blade => View.blade;
    public SpriteRenderer GroundMark => View.groundMark;
    public Vector2 Target;
    public Vector2 RestVelocity;
    public Vector2 LaunchOffset;
    public float LaunchPreparation;
    public int ReturnPhase;
    public float ReturnSettleTime;
    public Vector2 ReturnOvershoot;
    public Vector2 PreviousPosition;
    public SwordState State;
    public float MergeOrbitOffset;
    public SwordKind Kind;
    public readonly List<SwordKind> FusedKinds = new();
    public int FusionSize;
    public float RecallCooldown;
    public int BlocksRemaining = 1;
    public float BlockRecoilTime;
    public Vector2 BlockRecoilVelocity;
    public float ReturnAfterRecoil;
    public Vector2 FallVelocity;
    public EnemyState ContactEnemy;
    public EnemyState CurveEnemy;
    public Vector2 CurveOrigin;
    public Vector2 CurveControl;
    public float CurveProgress;
    public float CurveLength;
}
