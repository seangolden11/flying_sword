using System.Collections.Generic;
using UnityEngine;

public enum SwordState { Orbiting, Flying, Falling, Planted, Returning, Stored }

public sealed class SwordInstance
{
    public SwordView View;
    public Transform Root => View.transform;
    public Transform Visual => View.visual;
    public TrailRenderer Trail => View.trail;
    public SpriteRenderer Blade => View.blade;
    public SpriteRenderer GroundMark => View.groundMark;
    public Vector2 Target;
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
