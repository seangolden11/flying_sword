using UnityEngine;

public sealed partial class SwordSystem
{
    float formationTime;
    float formationFacing = 1;
    Vector2 formationVelocity;

    void UpdateFormationMotion(Vector2 cursor, float dt)
    {
        formationTime += dt;
        Vector2 velocity = (player.Position-player.PreviousPosition)/Mathf.Max(.001f,dt);
        formationVelocity = Vector2.Lerp(formationVelocity,velocity,1-Mathf.Exp(-8*dt));
        float look = cursor.x-RiderPosition.x;
        if (Mathf.Abs(look) <= .2f) look = velocity.x;
        if (Mathf.Abs(look) > .2f) formationFacing = Mathf.Sign(look);
    }

    Vector2 RestDirection(int index, int count)
        => Vector2.right*formationFacing;

    Vector2 RestPosition(int index, int count)
    {
        int column = index/5;
        int rows = Mathf.Min(5,Mathf.Max(1,count-column*5));
        float height = index%5-(rows-1)*.5f;
        float phase = formationTime*(1.5f+(index%3)*.12f)+index*1.73f;
        Vector2 bob = new(Mathf.Sin(phase*.73f)*.018f,Mathf.Sin(phase)*.035f);
        Vector2 lag = Vector2.ClampMagnitude(formationVelocity*.045f,.28f);
        return RiderPosition+new Vector2(-formationFacing*(.45f+column*.8f),
            .45f+height*.24f)+bob-lag;
    }

    Color ReadyBladeColor(SwordInstance sword)
        => sword.IsEnemyWeapon ? sword.CapturedBladeColor : sword.FusionSize > 0 ? FusionBlade : SwordKindColor(sword.Kind);

    void AdvanceRestingSword(SwordInstance sword, int index, int count, float dt)
    {
        sword.Root.localPosition = Vector2.SmoothDamp(sword.Root.localPosition,RestPosition(index,count),
            ref sword.RestVelocity,.13f/SwordSpeedMultiplier,30,dt);
        Quaternion angle = Quaternion.Euler(0,0,Vector2.SignedAngle(Vector2.down,RestDirection(index,count)));
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
        sword.LaunchOffset = (Vector2)sword.Root.localPosition-RiderPosition;
        sword.LaunchPreparation = 0;
        sword.Trail.emitting = false;
        sword.Trail.Clear();
        sword.RestVelocity = Vector2.zero;
        if (sword.BorrowedFrom != null) sword.BorrowedUsed = !sword.PermanentLoot;
        sword.Visual.localPosition = Vector3.zero;
        sword.Visual.localRotation = Quaternion.identity;
    }

    void AdvancePreparation(SwordInstance sword, float dt)
    {
        sword.LaunchPreparation += dt*SwordSpeedMultiplier;
        float progress = Mathf.Clamp01(sword.LaunchPreparation/.16f);
        Vector2 home = RiderPosition+sword.LaunchOffset;
        Vector2 direction = (sword.Target-home).normalized;
        if (direction.sqrMagnitude < .001f) direction = Vector2.right*formationFacing;
        sword.Root.localPosition = home-direction*(.2f*Mathf.SmoothStep(0,1,progress));
        Quaternion angle = Quaternion.Euler(0,0,Vector2.SignedAngle(Vector2.down,direction));
        sword.Root.localRotation = Quaternion.Slerp(sword.Root.localRotation,angle,1-Mathf.Exp(-35*dt));
        sword.Blade.color = Color.Lerp(ReadyBladeColor(sword),Color.white,progress*.22f);
        if (progress >= 1) BeginFlight(sword,sword.Target);
    }
}
