using UnityEngine;

public sealed partial class SwordSystem
{
    float formationTime;

    void UpdateFormationMotion(float dt) => formationTime += dt;

    static Vector2 RestDirection => Vector2.right;

    Vector2 RestPosition(SwordInstance sword)
    {
        // Include flying and planted swords so their slots stay open until they return.
        int index = 0;
        int count = 0;
        foreach (SwordInstance equipped in swords)
        {
            if (equipped.State == SwordState.Stored) continue;
            if (equipped == sword) index = count;
            count++;
        }

        return RiderPosition + EllipticSwordFormation.Offset(index, Mathf.Max(1, count), formationTime, RestDirection.x);
    }

    Color ReadyBladeColor(SwordInstance sword)
        => sword.IsOrderSword ? OrderBladeColor : sword.IsEnemyWeapon ? sword.CapturedBladeColor : sword.FusionSize > 0 ? FusionBlade : SwordKindColor(sword.Kind);

    void AdvanceRestingSword(SwordInstance sword, float dt)
    {
        sword.Root.localPosition = Vector2.SmoothDamp(sword.Root.localPosition,RestPosition(sword),
            ref sword.RestVelocity,.13f/SwordSpeedMultiplier,30,dt);
        Quaternion angle = Quaternion.Euler(0,0,Vector2.SignedAngle(Vector2.down,RestDirection));
        sword.Root.localRotation = Quaternion.Slerp(sword.Root.localRotation,angle,1-Mathf.Exp(-12*dt));
        SetFusionSize(sword,orbitVisual:false);
        sword.Trail.emitting = false;
        Color color = ReadyBladeColor(sword);
        sword.Blade.color = new Color(color.r*.88f,color.g*.88f,color.b*.88f,color.a);
    }

    void PrepareLaunch(SwordInstance sword, Vector2 target)
    {
        sword.State = SwordState.Preparing;
        sword.Target = target;
        sword.TargetProjectile = null;
        sword.LaunchEnemy = null;
        sword.LaunchOffset = (Vector2)sword.Root.localPosition-RiderPosition;
        sword.LaunchPreparation = 0;
        sword.Trail.emitting = false;
        sword.Trail.Clear();
        sword.RestVelocity = Vector2.zero;
        if (sword.BorrowedFrom != null || sword.CapturedGiantSword) sword.BorrowedUsed = !sword.PermanentLoot;
        sword.Visual.localPosition = Vector3.zero;
        sword.Visual.localRotation = Quaternion.identity;
    }

    void AdvancePreparation(SwordInstance sword, float dt)
    {
        if (sword.CapturedGiantSword)
        {
            AdvanceGiantPreparation(sword, dt);
            return;
        }
        sword.LaunchPreparation += dt*SwordSpeedMultiplier;
        float progress = Mathf.Clamp01(sword.LaunchPreparation/.16f);
        Vector2 home = RiderPosition+sword.LaunchOffset;
        if (TryGetProjectileTarget(sword, out Vector2 projectilePosition))
            sword.Target = projectilePosition;
        else if (sword.LaunchEnemy is { Health: > 0 } enemy && !enemySystem.IsInvulnerable(enemy))
            sword.Target = transform.InverseTransformPoint(enemy.Position);
        Vector2 direction = (sword.Target-home).normalized;
        if (direction.sqrMagnitude < .001f) direction = RestDirection;
        sword.Root.localPosition = home-direction*(.2f*Mathf.SmoothStep(0,1,progress));
        Quaternion angle = Quaternion.Euler(0,0,Vector2.SignedAngle(Vector2.down,direction));
        sword.Root.localRotation = Quaternion.Slerp(sword.Root.localRotation,angle,1-Mathf.Exp(-35*dt));
        sword.Blade.color = Color.Lerp(ReadyBladeColor(sword),Color.white,progress*.22f);
        if (progress >= 1) BeginFlight(sword,sword.Target);
    }
}
